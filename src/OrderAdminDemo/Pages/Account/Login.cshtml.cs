using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrderAdminDemo.Data;

namespace OrderAdminDemo.Pages.Account;

public class LoginModel(AppDbContext db) : PageModel
{
    [BindProperty, Required] public string UserName { get; set; } = "";
    [BindProperty, Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public string? Error { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == UserName);
        var ok = user is not null &&
                 new PasswordHasher<AppUser>().VerifyHashedPassword(user, user.PasswordHash, Password) != PasswordVerificationResult.Failed;
        if (!ok)
        {
            // 不說是帳號錯還是密碼錯，避免被用來猜帳號
            Error = "帳號或密碼不正確。";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user!.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, user.Role),
            new("DisplayName", user.DisplayName),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        // 只允許站內網址，避免登入後被導到外部釣魚網站
        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/");
    }
}
