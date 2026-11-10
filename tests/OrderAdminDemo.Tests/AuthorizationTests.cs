using System.Net;
using OrderAdminDemo.Data;

namespace OrderAdminDemo.Tests;

public class AuthorizationTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;

    public AuthorizationTests(TestApp app) => _app = app;

    [Theory]
    [InlineData("/")]
    [InlineData("/Orders")]
    [InlineData("/Members")]
    [InlineData("/Reports")]
    public async Task 未登入一律導向登入頁(string url)
    {
        var res = await _app.NewClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Contains("/Account/Login", res.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task 密碼錯誤不能登入()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _app.LoginAsync(SeedData.AdminUser, "wrong-password"));
    }

    [Fact]
    public async Task 登入後不會被導到外部網址()
    {
        var client = _app.NewClient();
        var token = await TestApp.GetTokenAsync(client, "/Account/Login");
        var res = await client.PostAsync("/Account/Login?ReturnUrl=https%3A%2F%2Fevil.example.com", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserName"] = SeedData.StaffUser,
            ["Password"] = SeedData.StaffPassword,
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal("/", res.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task 店員看不到營收報表()
    {
        var staff = await _app.LoginAsync(SeedData.StaffUser, SeedData.StaffPassword);
        var res = await staff.GetAsync("/Reports");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Contains("/Account/AccessDenied", res.Headers.Location!.OriginalString);

        var denied = await staff.GetAsync(res.Headers.Location);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task 管理員可以看營收報表與匯出()
    {
        var admin = await _app.LoginAsync(SeedData.AdminUser, SeedData.AdminPassword);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/Reports")).StatusCode);

        var export = await admin.GetAsync("/Reports?handler=Export");
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", export.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task 店員儀表板不顯示營收()
    {
        var staff = await _app.LoginAsync(SeedData.StaffUser, SeedData.StaffPassword);
        var html = await staff.GetStringAsync("/");
        Assert.DoesNotContain("本月營收", html);

        var admin = await _app.LoginAsync(SeedData.AdminUser, SeedData.AdminPassword);
        Assert.Contains("本月營收", await admin.GetStringAsync("/"));
    }

    [Fact]
    public async Task 店員繞過畫面直接送刪除請求也會被擋()
    {
        var memberId = _app.WithDb(db => db.Members.First().Id);
        var staff = await _app.LoginAsync(SeedData.StaffUser, SeedData.StaffPassword);
        var token = await TestApp.GetTokenAsync(staff, "/Members/Edit");

        var res = await staff.PostAsync($"/Members?handler=Delete&id={memberId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
        }));

        Assert.Contains("/Account/AccessDenied", res.Headers.Location!.OriginalString);
        Assert.True(_app.WithDb(db => db.Members.Any(m => m.Id == memberId)));
    }

    [Fact]
    public async Task 店員改售價會被忽略()
    {
        var (id, price) = _app.WithDb(db => db.Products.Select(p => new { p.Id, p.Price }).First() is var p ? (p.Id, p.Price) : default);
        var staff = await _app.LoginAsync(SeedData.StaffUser, SeedData.StaffPassword);
        var token = await TestApp.GetTokenAsync(staff, $"/Products/Edit/{id}");

        await staff.PostAsync($"/Products/Edit/{id}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Name"] = "測試商品",
            ["Input.Price"] = "1",
            ["Input.Stock"] = "99",
            ["Input.SafetyStock"] = "5",
            ["__RequestVerificationToken"] = token,
        }));

        var after = _app.WithDb(db => db.Products.First(p => p.Id == id));
        Assert.Equal(price, after.Price);
        Assert.Equal(99, after.Stock);
    }
}
