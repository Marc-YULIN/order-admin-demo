using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrderAdminDemo.Services;
using OrderAdminDemo.Services.Ecpay;

namespace OrderAdminDemo.Pages.Orders;

/// <summary>產生綠界付款表單並自動送出（綠界 AIO 規定用瀏覽器 POST 表單過去）。</summary>
public class PayModel(EcpayService ecpay) : PageModel
{
    public EcpayCheckout Checkout { get; private set; } = null!;

    public IActionResult OnGet(int id) => RedirectToPage("Details", new { id });

    // 用 POST：每次都會產生新的交易編號，屬於會改資料的動作
    public async Task<IActionResult> OnPostAsync(int id)
    {
        try
        {
            Checkout = await ecpay.CreateCheckoutAsync(id, $"{Request.Scheme}://{Request.Host}");
            return Page();
        }
        catch (OrderException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToPage("Details", new { id });
        }
    }
}
