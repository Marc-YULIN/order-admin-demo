using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrderAdminDemo.Data;
using OrderAdminDemo.Services;

namespace OrderAdminDemo.Pages.Products;

public class IndexModel(AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Category { get; set; }
    [BindProperty(SupportsGet = true)] public bool LowOnly { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNo { get; set; } = 1;
    public PagedList<Product> Result { get; private set; } = null!;
    public List<string> Categories { get; private set; } = [];

    private IQueryable<Product> Query()
    {
        var q = db.Products.AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q)) q = q.Where(p => p.Name.Contains(Q.Trim()) || p.Sku.Contains(Q.Trim()));
        if (!string.IsNullOrWhiteSpace(Category)) q = q.Where(p => p.Category == Category);
        if (LowOnly) q = q.Where(p => p.Stock < p.SafetyStock);
        return q.OrderBy(p => p.Sku);
    }

    public async Task OnGetAsync()
    {
        Categories = await db.Products.Select(p => p.Category).Distinct().OrderBy(c => c).ToListAsync();
        Result = await PagedList<Product>.CreateAsync(Query(), PageNo, 30);
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var rows = await Query().ToListAsync();
        var bytes = ExcelExporter.Export("商品庫存", [
            new ExcelColumn<Product>("料號", p => p.Sku),
            new("品名", p => p.Name),
            new("分類", p => p.Category),
            new("售價", p => p.Price, "#,##0"),
            new("庫存", p => p.Stock),
            new("安全庫存", p => p.SafetyStock),
            new("需補貨", p => p.IsLowStock ? "是" : ""),
        ], rows);
        return File(bytes, ExcelExporter.ContentType, $"商品庫存_{DateTime.Now:yyyyMMdd}.xlsx");
    }
}
