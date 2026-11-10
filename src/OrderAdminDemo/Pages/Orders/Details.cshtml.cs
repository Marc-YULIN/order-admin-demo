using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrderAdminDemo.Data;
using OrderAdminDemo.Services;
using OrderAdminDemo.Services.Ecpay;

namespace OrderAdminDemo.Pages.Orders;

public class DetailsModel(AppDbContext db, OrderService orders, EcpayService ecpay) : PageModel
{
    public Order Order { get; private set; } = null!;
    public List<PaymentNotification> Notifications { get; private set; } = [];
    public bool EcpayReady => ecpay.IsConfigured;

    [BindProperty(SupportsGet = true)] public NotifyOutcome? Payment { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var order = await db.Orders.Include(o => o.Member).Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();
        Order = order;
        Notifications = await db.PaymentNotifications
            .Where(n => n.MerchantTradeNo != null && n.MerchantTradeNo.StartsWith($"OA{id:000000}"))
            .OrderByDescending(n => n.ReceivedAt).Take(10).ToListAsync();
        return Page();
    }

    public Task<IActionResult> OnPostCancelAsync(int id) => Run(id, () => orders.CancelAsync(id), "訂單已取消，庫存已加回。");

    public Task<IActionResult> OnPostShipAsync(int id) => Run(id, () => orders.MarkShippedAsync(id), "已標記為出貨。");

    private async Task<IActionResult> Run(int id, Func<Task> action, string ok)
    {
        try
        {
            await action();
            TempData["Message"] = ok;
        }
        catch (OrderException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage(new { id });
    }
}
