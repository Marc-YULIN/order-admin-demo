using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OrderAdminDemo.Data;

namespace OrderAdminDemo.Tests;

/// <summary>
/// 每個測試類別各用一個暫存 SQLite 檔，互不影響。
/// 綠界金鑰用官方文件公開的測試商店值（同 README），只用於測試。
/// </summary>
public class TestApp : WebApplicationFactory<Program>
{
    public const string MerchantId = "3002607";
    public const string HashKey = "pwFHCqoQZGmho4w6";
    public const string HashIv = "EkRm7iFT261dpevs";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"orderadmin-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath};Pooling=False");
        builder.UseSetting("Ecpay:MerchantId", MerchantId);
        builder.UseSetting("Ecpay:HashKey", HashKey);
        builder.UseSetting("Ecpay:HashIv", HashIv);
    }

    public T WithDb<T>(Func<AppDbContext, T> work)
    {
        using var scope = Services.CreateScope();
        return work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public HttpClient NewClient() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public async Task<HttpClient> LoginAsync(string user, string password)
    {
        var client = NewClient();
        var token = await GetTokenAsync(client, "/Account/Login");
        var res = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserName"] = user,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token,
        }));
        if (res.StatusCode != System.Net.HttpStatusCode.Redirect)
            throw new InvalidOperationException($"登入失敗：{(int)res.StatusCode}");
        return client;
    }

    public static async Task<string> GetTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var m = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        if (!m.Success) throw new InvalidOperationException($"{url} 找不到 antiforgery token");
        return m.Groups[1].Value;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { File.Delete(_dbPath); } catch (IOException) { }
    }
}
