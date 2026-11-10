using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrderAdminDemo.Data;
using OrderAdminDemo.Services;

namespace OrderAdminDemo.Pages.Reports;

public record MonthRow(string Month, int Orders, int Revenue);
public record ProductRow(string Category, string Name, int Quantity, int Revenue);

/// <summary>營收報表：整個 /Reports 資料夾在 Program.cs 設為只有管理員能看。</summary>
public class IndexModel(AppDbContext db, TimeProvider clock) : PageModel
{
    public List<MonthRow> Months { get; private set; } = [];
    public List<ProductRow> Products { get; private set; } = [];
    public List<(string Category, int Revenue)> Categories { get; private set; } = [];

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnGetExportAsync()
    {
        await LoadAsync();
        var bytes = ExcelExporter.Export("商品銷售", [
            new ExcelColumn<ProductRow>("分類", r => r.Category),
            new("品名", r => r.Name),
            new("銷售數量", r => r.Quantity),
            new("營收", r => r.Revenue, "#,##0"),
        ], Products);
        return File(bytes, ExcelExporter.ContentType, $"營收報表_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    private async Task LoadAsync()
    {
        var now = clock.GetLocalNow().DateTime;
        var from = new DateTime(now.Year, now.Month, 1).AddMonths(-2);

        var paid = db.Orders.Where(o => o.CreatedAt >= from && (o.Status == OrderStatus.Paid || o.Status == OrderStatus.Shipped));
        var orders = await paid.Select(o => new { o.CreatedAt, o.Total }).ToListAsync();
        Months = orders.GroupBy(o => o.CreatedAt.ToString("yyyy/MM"))
            .OrderBy(g => g.Key)
            .Select(g => new MonthRow(g.Key, g.Count(), g.Sum(x => x.Total))).ToList();

        var items = await db.OrderItems
            .Where(i => paid.Select(o => o.Id).Contains(i.OrderId))
            .Join(db.Products, i => i.ProductId, p => p.Id, (i, p) => new { p.Category, i.ProductName, i.Quantity, i.UnitPrice })
            .ToListAsync();
        Products = items.GroupBy(i => (i.Category, i.ProductName))
            .Select(g => new ProductRow(g.Key.Category, g.Key.ProductName, g.Sum(x => x.Quantity), g.Sum(x => x.Quantity * x.UnitPrice)))
            .OrderByDescending(r => r.Revenue).ToList();
        Categories = Products.GroupBy(p => p.Category)
            .Select(g => (g.Key, g.Sum(x => x.Revenue))).OrderByDescending(x => x.Item2).ToList();
    }
}
