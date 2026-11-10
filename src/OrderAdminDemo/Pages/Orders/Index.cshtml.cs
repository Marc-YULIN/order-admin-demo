using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrderAdminDemo.Data;
using OrderAdminDemo.Services;

namespace OrderAdminDemo.Pages.Orders;

public class IndexModel(AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public OrderStatus? Status { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNo { get; set; } = 1;
    public PagedList<Order> Result { get; private set; } = null!;

    private IQueryable<Order> Query()
    {
        var q = db.Orders.Include(o => o.Member).AsQueryable();
        if (Status is { } s) q = q.Where(o => o.Status == s);
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var k = Q.Trim();
            q = q.Where(o => o.OrderNo.Contains(k) || o.Member!.Name.Contains(k) || o.Member!.Phone.Contains(k));
        }
        return q.OrderByDescending(o => o.CreatedAt);
    }

    public async Task OnGetAsync() => Result = await PagedList<Order>.CreateAsync(Query(), PageNo);

    public async Task<IActionResult> OnGetExportAsync()
    {
        var rows = await Query().ToListAsync();
        var bytes = ExcelExporter.Export("訂單", [
            new ExcelColumn<Order>("訂單編號", o => o.OrderNo),
            new("下單時間", o => o.CreatedAt, "yyyy/mm/dd hh:mm"),
            new("會員", o => o.Member?.Name),
            new("手機", o => o.Member?.Phone),
            new("金額", o => o.Total, "#,##0"),
            new("狀態", o => OrderStatusText.Of(o.Status)),
            new("付款方式", o => o.PaymentType),
            new("付款時間", o => o.PaidAt, "yyyy/mm/dd hh:mm"),
            new("綠界交易編號", o => o.PaymentTradeNo),
        ], rows);
        return File(bytes, ExcelExporter.ContentType, $"訂單_{DateTime.Now:yyyyMMdd}.xlsx");
    }
}
