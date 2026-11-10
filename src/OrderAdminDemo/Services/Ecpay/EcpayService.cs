using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrderAdminDemo.Data;

namespace OrderAdminDemo.Services.Ecpay;

public record EcpayCheckout(string ActionUrl, IReadOnlyDictionary<string, string> Fields);

public enum NotifyOutcome
{
    Paid,
    AlreadyProcessed,
    NotPaid,
    Simulated,
    Rejected,
}

public record NotifyResult(NotifyOutcome Outcome, string Message, int? OrderId)
{
    /// <summary>綠界要求收到通知後回 1|OK，否則會一直重送。驗證失敗回錯誤訊息。</summary>
    public string ResponseText => Outcome == NotifyOutcome.Rejected ? "0|" + Message : "1|OK";
}

public class EcpayService(AppDbContext db, IOptions<EcpayOptions> options, TimeProvider clock, ILogger<EcpayService> logger)
{
    public const string NotifyPath = "/payment/ecpay/notify";
    public const string ResultPath = "/payment/ecpay/result";

    private readonly EcpayOptions _opt = options.Value;

    public bool IsConfigured => _opt.IsConfigured;

    /// <summary>產生送往綠界的付款表單。每次都換新的 MerchantTradeNo（綠界不接受重複編號）。</summary>
    public async Task<EcpayCheckout> CreateCheckoutAsync(int orderId, string requestBaseUrl)
    {
        if (!_opt.IsConfigured) throw new OrderException("尚未設定綠界測試金鑰，請見 README「綠界測試環境」。");

        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new OrderException("找不到這筆訂單。");
        if (order.Status != OrderStatus.PendingPayment)
            throw new OrderException($"這筆訂單目前是「{OrderStatusText.Of(order.Status)}」，不能再付款。");

        var now = clock.GetLocalNow().DateTime;
        order.MerchantTradeNo = $"OA{order.Id:000000}{now:yyMMddHHmmss}"; // 20 碼英數
        await db.SaveChangesAsync();

        var baseUrl = (_opt.PublicBaseUrl ?? requestBaseUrl).TrimEnd('/');
        var itemName = string.Join("#", order.Items.Select(i => $"{i.ProductName} x{i.Quantity}"));
        if (itemName.Length > 200) itemName = itemName[..197] + "...";

        var fields = new Dictionary<string, string>
        {
            ["MerchantID"] = _opt.MerchantId,
            ["MerchantTradeNo"] = order.MerchantTradeNo,
            ["MerchantTradeDate"] = now.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture),
            ["PaymentType"] = "aio",
            ["TotalAmount"] = order.Total.ToString(CultureInfo.InvariantCulture),
            ["TradeDesc"] = "咖啡選物線上訂單",
            ["ItemName"] = itemName,
            ["ReturnURL"] = baseUrl + NotifyPath,
            ["OrderResultURL"] = baseUrl + ResultPath,
            ["ChoosePayment"] = "ALL",
            ["EncryptType"] = "1",
            ["CustomField1"] = order.OrderNo,
        };
        fields["CheckMacValue"] = EcpayCheckMac.Compute(fields, _opt.HashKey, _opt.HashIv);
        return new EcpayCheckout(_opt.CheckoutUrl, fields);
    }

    /// <summary>
    /// 處理綠界的付款結果（背景通知 ReturnURL 與付款完成頁 OrderResultURL 共用）。
    /// 驗證順序：CheckMacValue → 特店編號 → 找得到訂單 → 金額相符；全部通過才改狀態。
    /// 同一筆通知重送時只處理一次。
    /// </summary>
    public async Task<NotifyResult> HandleResultAsync(IReadOnlyDictionary<string, string> form, string source)
    {
        var result = await EvaluateAsync(form);
        db.PaymentNotifications.Add(new PaymentNotification
        {
            ReceivedAt = clock.GetLocalNow().DateTime,
            Source = source,
            MerchantTradeNo = form.GetValueOrDefault("MerchantTradeNo"),
            RawBody = string.Join("&", form.Where(kv => kv.Key != "CheckMacValue").Select(kv => $"{kv.Key}={kv.Value}")),
            Result = $"{result.Outcome}: {result.Message}",
        });
        await db.SaveChangesAsync();

        if (result.Outcome == NotifyOutcome.Rejected)
            logger.LogWarning("綠界通知被拒絕（{Source}）：{Message}", source, result.Message);
        else
            logger.LogInformation("綠界通知（{Source}）：{Outcome} {Message}", source, result.Outcome, result.Message);
        return result;
    }

    private async Task<NotifyResult> EvaluateAsync(IReadOnlyDictionary<string, string> form)
    {
        if (!_opt.IsConfigured)
            return new(NotifyOutcome.Rejected, "未設定綠界金鑰", null);
        if (!EcpayCheckMac.Verify(form, _opt.HashKey, _opt.HashIv))
            return new(NotifyOutcome.Rejected, "CheckMacValue Error", null);
        if (form.GetValueOrDefault("MerchantID") != _opt.MerchantId)
            return new(NotifyOutcome.Rejected, "MerchantID 不符", null);

        var tradeNo = form.GetValueOrDefault("MerchantTradeNo");
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.MerchantTradeNo == tradeNo);
        if (order is null)
            return new(NotifyOutcome.Rejected, $"找不到交易編號 {tradeNo}", null);

        if (!int.TryParse(form.GetValueOrDefault("TradeAmt"), out var amount) || amount != order.Total)
            return new(NotifyOutcome.Rejected, $"金額不符（通知 {form.GetValueOrDefault("TradeAmt")}，訂單 {order.Total}）", order.Id);

        if (form.GetValueOrDefault("RtnCode") != "1")
            return new(NotifyOutcome.NotPaid, $"付款未完成：{form.GetValueOrDefault("RtnMsg")}", order.Id);

        // 綠界後台「模擬付款」會帶 SimulatePaid=1：不是真的收到錢，不能出貨
        if (form.GetValueOrDefault("SimulatePaid") == "1")
            return new(NotifyOutcome.Simulated, "模擬付款，不更新訂單狀態", order.Id);

        // 只有「待付款」才會被改成「已付款」：用條件式更新，兩個通知同時到也只會成功一次
        var paidAt = DateTime.TryParse(form.GetValueOrDefault("PaymentDate"), CultureInfo.InvariantCulture, out var d)
            ? d : clock.GetLocalNow().DateTime;
        var updated = await db.Orders
            .Where(o => o.Id == order.Id && o.Status == OrderStatus.PendingPayment)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderStatus.Paid)
                .SetProperty(o => o.PaidAt, paidAt)
                .SetProperty(o => o.PaymentTradeNo, form.GetValueOrDefault("TradeNo"))
                .SetProperty(o => o.PaymentType, form.GetValueOrDefault("PaymentType")));

        if (updated == 1) return new(NotifyOutcome.Paid, $"訂單 {order.OrderNo} 已付款", order.Id);
        return order.Status == OrderStatus.Cancelled
            ? new(NotifyOutcome.AlreadyProcessed, $"訂單 {order.OrderNo} 已取消卻收到付款，需人工退款", order.Id)
            : new(NotifyOutcome.AlreadyProcessed, $"訂單 {order.OrderNo} 先前已處理", order.Id);
    }
}
