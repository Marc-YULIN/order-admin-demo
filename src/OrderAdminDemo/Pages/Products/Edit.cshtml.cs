using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrderAdminDemo.Data;

namespace OrderAdminDemo.Pages.Products;

public class ProductInput
{
    [Required(ErrorMessage = "請輸入品名"), StringLength(60)]
    [Display(Name = "品名")] public string Name { get; set; } = "";

    [Range(1, 1_000_000, ErrorMessage = "售價需介於 1 到 1,000,000")]
    [Display(Name = "售價")] public int Price { get; set; }

    [Range(0, 100_000, ErrorMessage = "庫存不能是負數")]
    [Display(Name = "庫存")] public int Stock { get; set; }

    [Range(0, 100_000, ErrorMessage = "安全庫存不能是負數")]
    [Display(Name = "安全庫存")] public int SafetyStock { get; set; }
}

public class EditModel(AppDbContext db) : PageModel
{
    [BindProperty] public ProductInput Input { get; set; } = new();
    public Product Product { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var p = await db.Products.FindAsync(id);
        if (p is null) return NotFound();
        Product = p;
        Input = new ProductInput { Name = p.Name, Price = p.Price, Stock = p.Stock, SafetyStock = p.SafetyStock };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var p = await db.Products.FindAsync(id);
        if (p is null) return NotFound();
        Product = p;
        if (!ModelState.IsValid) return Page();

        // 店員可以調整庫存，但售價只有管理員能改。畫面上欄位唯讀之外，伺服器端也直接忽略店員送來的售價
        if (User.IsInRole(Roles.Admin)) p.Price = Input.Price;
        else if (Input.Price != p.Price) TempData["Error"] = "售價只有管理員能修改，這次的售價變更已忽略。";

        p.Name = Input.Name.Trim();
        p.Stock = Input.Stock;
        p.SafetyStock = Input.SafetyStock;
        await db.SaveChangesAsync();

        TempData["Message"] = $"已儲存「{p.Name}」，目前庫存 {p.Stock}。";
        return RedirectToPage("Index");
    }
}
