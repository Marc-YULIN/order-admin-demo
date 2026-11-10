using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OrderAdminDemo.Pages.Account;

public class AccessDeniedModel : PageModel
{
    // 回 403 而不是 200，讓 API 呼叫端與測試能分辨「沒權限」
    public void OnGet() => Response.StatusCode = StatusCodes.Status403Forbidden;
}
