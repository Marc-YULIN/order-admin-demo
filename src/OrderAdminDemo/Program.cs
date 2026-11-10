using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OrderAdminDemo.Data;
using OrderAdminDemo.Services;
using OrderAdminDemo.Services.Ecpay;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=App_Data/orderadmin.db"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<EcpayService>();
builder.Services.Configure<EcpayOptions>(builder.Configuration.GetSection(EcpayOptions.Section));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/AccessDenied";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin))
    // 預設所有頁面都要登入，例外頁面在下面明確開放
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/Reports", Policies.AdminOnly);
    o.Conventions.AllowAnonymousToPage("/Account/Login");
    o.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
    o.Conventions.AllowAnonymousToPage("/Error");
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var csb = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(db.Database.GetConnectionString());
    if (Path.GetDirectoryName(Path.GetFullPath(csb.DataSource)) is { } dir) Directory.CreateDirectory(dir);
    db.Database.EnsureCreated();
    SeedData.Run(db, DateTime.Now);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}
app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets().AllowAnonymous();
app.MapRazorPages().WithStaticAssets();

// 綠界背景通知：由綠界伺服器呼叫，沒有登入 cookie，也沒有 antiforgery token，靠 CheckMacValue 驗證
app.MapPost(EcpayService.NotifyPath, async (HttpRequest req, EcpayService ecpay) =>
{
    var form = await ReadFormAsync(req);
    var result = await ecpay.HandleResultAsync(form, "ReturnURL");
    return Results.Text(result.ResponseText);
}).AllowAnonymous().DisableAntiforgery();

// 付款完成頁：綠界把使用者的瀏覽器 POST 回來（跨站 POST 不會帶 Lax cookie），
// 驗證後轉回訂單頁，讓瀏覽器以一般 GET 帶著登入狀態回來
app.MapPost(EcpayService.ResultPath, async (HttpRequest req, EcpayService ecpay) =>
{
    var form = await ReadFormAsync(req);
    var result = await ecpay.HandleResultAsync(form, "OrderResultURL");
    var target = result.OrderId is { } id
        ? $"/Orders/Details/{id}?payment={result.Outcome}"
        : $"/Orders?payment={result.Outcome}";
    return Results.Redirect(target);
}).AllowAnonymous().DisableAntiforgery();

app.Run();

static async Task<IReadOnlyDictionary<string, string>> ReadFormAsync(HttpRequest req)
{
    if (!req.HasFormContentType) return new Dictionary<string, string>();
    var form = await req.ReadFormAsync();
    return form.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
}

public static class Policies
{
    public const string AdminOnly = nameof(AdminOnly);
}

public partial class Program;
