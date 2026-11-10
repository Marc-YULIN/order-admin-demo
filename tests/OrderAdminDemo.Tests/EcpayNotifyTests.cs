using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OrderAdminDemo.Data;
using OrderAdminDemo.Services.Ecpay;

namespace OrderAdminDemo.Tests;

/// <summary>模擬綠界打 ReturnURL／OrderResultURL（本機收不到真的通知，所以用測試驗證整條流程）。</summary>
public class EcpayNotifyTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;

    public EcpayNotifyTests(TestApp app) => _app = app;

    /// <summary>建立一筆待付款訂單，並走一次「前往付款」拿到送綠界的欄位。</summary>
    private async Task<(int OrderId, Dictionary<string, string> Checkout)> NewPendingOrderAsync()
    {
        var client = await _app.LoginAsync(SeedData.StaffUser, SeedData.StaffPassword);
        var (memberId, productId) = _app.WithDb(db => (
            db.Members.First().Id,
            db.Products.First(p => p.Stock >= 2).Id));

        var token = await TestApp.GetTokenAsync(client, "/Orders/Create");
        var created = await client.PostAsync("/Orders/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["MemberId"] = memberId.ToString(),
            [$"Quantities[{productId}]"] = "2",
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var orderId = int.Parse(created.Headers.Location!.OriginalString.Split('/').Last());

        var payToken = await TestApp.GetTokenAsync(client, $"/Orders/Details/{orderId}");
        var payPage = await client.PostAsync($"/Orders/Pay/{orderId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = payToken,
        }));
        var html = await payPage.Content.ReadAsStringAsync();
        var fields = Regex.Matches(html, "<input type=\"hidden\" name=\"([^\"]+)\" value=\"([^\"]*)\"")
            .Where(m => m.Groups[1].Value != "__RequestVerificationToken")
            .ToDictionary(m => m.Groups[1].Value, m => WebUtility.HtmlDecode(m.Groups[2].Value));
        return (orderId, fields);
    }

    /// <summary>照綠界通知的欄位組一份並簽章。</summary>
    private static Dictionary<string, string> PaidNotification(Dictionary<string, string> checkout, string? amount = null)
    {
        var n = new Dictionary<string, string>
        {
            ["MerchantID"] = checkout["MerchantID"],
            ["MerchantTradeNo"] = checkout["MerchantTradeNo"],
            ["StoreID"] = "",
            ["RtnCode"] = "1",
            ["RtnMsg"] = "交易成功",
            ["TradeNo"] = "2611101530123456",
            ["TradeAmt"] = amount ?? checkout["TotalAmount"],
            ["PaymentDate"] = "2026/11/10 15:31:02",
            ["PaymentType"] = "Credit_CreditCard",
            ["PaymentTypeChargeFee"] = "10",
            ["TradeDate"] = "2026/11/10 15:30:12",
            ["SimulatePaid"] = "0",
            ["CustomField1"] = checkout["CustomField1"],
            ["CustomField2"] = "",
            ["CustomField3"] = "",
            ["CustomField4"] = "",
        };
        n["CheckMacValue"] = EcpayCheckMac.Compute(n, TestApp.HashKey, TestApp.HashIv);
        return n;
    }

    private async Task<string> NotifyAsync(Dictionary<string, string> form)
    {
        var res = await _app.NewClient().PostAsync(EcpayService.NotifyPath, new FormUrlEncodedContent(form));
        return await res.Content.ReadAsStringAsync();
    }

    private Order LoadOrder(int id) => _app.WithDb(db => db.Orders.AsNoTracking().First(o => o.Id == id));

    [Fact]
    public async Task 付款表單的CheckMacValue正確且導向測試環境()
    {
        var (_, checkout) = await NewPendingOrderAsync();
        Assert.True(EcpayCheckMac.Verify(checkout, TestApp.HashKey, TestApp.HashIv));
        Assert.Equal(20, checkout["MerchantTradeNo"].Length);
        Assert.EndsWith(EcpayService.NotifyPath, checkout["ReturnURL"]);
    }

    [Fact]
    public async Task 合法通知把訂單改成已付款並回1OK()
    {
        var (orderId, checkout) = await NewPendingOrderAsync();

        Assert.Equal("1|OK", await NotifyAsync(PaidNotification(checkout)));

        var order = LoadOrder(orderId);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal("2611101530123456", order.PaymentTradeNo);
        Assert.Equal("Credit_CreditCard", order.PaymentType);
    }

    [Fact]
    public async Task 同一筆通知重送只處理一次()
    {
        var (orderId, checkout) = await NewPendingOrderAsync();
        var n = PaidNotification(checkout);

        Assert.Equal("1|OK", await NotifyAsync(n));
        Assert.Equal("1|OK", await NotifyAsync(n)); // 綠界沒收到 1|OK 會重送，重送也要回 1|OK

        var results = _app.WithDb(db => db.PaymentNotifications
            .Where(x => x.MerchantTradeNo == checkout["MerchantTradeNo"]).Select(x => x.Result).ToList());
        Assert.Equal(2, results.Count);
        Assert.Single(results, r => r.StartsWith(nameof(NotifyOutcome.Paid)));
        Assert.Single(results, r => r.StartsWith(nameof(NotifyOutcome.AlreadyProcessed)));
    }

    [Fact]
    public async Task 竄改金額的通知被拒絕且訂單不變()
    {
        var (orderId, checkout) = await NewPendingOrderAsync();
        var n = PaidNotification(checkout);
        n["TradeAmt"] = "1"; // 改了金額但沒重新簽章

        Assert.StartsWith("0|", await NotifyAsync(n));
        Assert.Equal(OrderStatus.PendingPayment, LoadOrder(orderId).Status);
    }

    [Fact]
    public async Task 簽章正確但金額與訂單不符也拒絕()
    {
        var (orderId, checkout) = await NewPendingOrderAsync();

        Assert.StartsWith("0|", await NotifyAsync(PaidNotification(checkout, amount: "1")));
        Assert.Equal(OrderStatus.PendingPayment, LoadOrder(orderId).Status);
    }

    [Fact]
    public async Task 模擬付款不改訂單狀態()
    {
        var (orderId, checkout) = await NewPendingOrderAsync();
        var n = PaidNotification(checkout);
        n["SimulatePaid"] = "1";
        n["CheckMacValue"] = EcpayCheckMac.Compute(n, TestApp.HashKey, TestApp.HashIv);

        Assert.Equal("1|OK", await NotifyAsync(n));
        Assert.Equal(OrderStatus.PendingPayment, LoadOrder(orderId).Status);
    }

    [Fact]
    public async Task 付款完成頁驗證後導回訂單頁()
    {
        var (orderId, checkout) = await NewPendingOrderAsync();

        var res = await _app.NewClient().PostAsync(EcpayService.ResultPath, new FormUrlEncodedContent(PaidNotification(checkout)));

        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Equal($"/Orders/Details/{orderId}?payment=Paid", res.Headers.Location!.OriginalString);
        Assert.Equal(OrderStatus.Paid, LoadOrder(orderId).Status);
    }
}
