using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrderAdminDemo.Data;
using OrderAdminDemo.Services;

namespace OrderAdminDemo.Pages.Orders;

public class CreateModel(AppDbContext db, OrderService orders) : PageModel
{
    [BindProperty] public int MemberId { get; set; }

    /// <summary>key = 商品編號，value = 數量</summary>
    [BindProperty] public Dictionary<int, int> Quantities { get; set; } = [];

    public List<Member> Members { get; private set; } = [];
    public List<Product> Products { get; private set; } = [];
    public string? Error { get; private set; }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            var order = await orders.CreateAsync(MemberId, Quantities.Select(kv => new OrderLine(kv.Key, kv.Value)).ToList());
            TempData["Message"] = $"已建立訂單 {order.OrderNo}，金額 NT${order.Total:N0}。";
            return RedirectToPage("Details", new { id = order.Id });
        }
        catch (OrderException ex)
        {
            Error = ex.Message;
            await LoadAsync();
            return Page();
        }
    }

    private async Task LoadAsync()
    {
        Members = await db.Members.OrderBy(m => m.Name).ToListAsync();
        Products = await db.Products.OrderBy(p => p.Sku).ToListAsync();
    }
}
