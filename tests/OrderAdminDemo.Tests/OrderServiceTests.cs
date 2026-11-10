using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderAdminDemo.Data;
using OrderAdminDemo.Services;

namespace OrderAdminDemo.Tests;

public class OrderServiceTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;

    public OrderServiceTests(TestApp app) => _app = app;

    private async Task<T> UseService<T>(Func<OrderService, AppDbContext, Task<T>> work)
    {
        using var scope = _app.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<OrderService>(), scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private (int MemberId, int ProductId, int Stock) PickProduct(int minStock) => _app.WithDb(db =>
    {
        var p = db.Products.AsNoTracking().First(x => x.Stock >= minStock);
        return (db.Members.First().Id, p.Id, p.Stock);
    });

    private int StockOf(int productId) => _app.WithDb(db => db.Products.AsNoTracking().First(p => p.Id == productId).Stock);

    [Fact]
    public async Task 建立訂單會扣庫存並算出金額()
    {
        var (memberId, productId, stock) = PickProduct(3);

        var order = await UseService((s, _) => s.CreateAsync(memberId, [new OrderLine(productId, 2), new OrderLine(productId, 1)]));

        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Single(order.Items); // 同一商品合併成一列
        Assert.Equal(3, order.Items[0].Quantity);
        Assert.Equal(order.Items[0].UnitPrice * 3, order.Total);
        Assert.Equal(stock - 3, StockOf(productId));
    }

    [Fact]
    public async Task 庫存不足時整筆不成立且庫存不變()
    {
        var (memberId, productId, stock) = PickProduct(1);
        var other = _app.WithDb(db => db.Products.AsNoTracking().First(p => p.Id != productId && p.Stock >= 1));

        var ex = await Assert.ThrowsAsync<OrderException>(() => UseService((s, _) =>
            s.CreateAsync(memberId, [new OrderLine(other.Id, 1), new OrderLine(productId, stock + 1)])));

        Assert.Contains("庫存不足", ex.Message);
        Assert.Equal(stock, StockOf(productId));
        Assert.Equal(other.Stock, StockOf(other.Id)); // 另一項也沒被扣
    }

    [Fact]
    public async Task 沒選商品不能建立()
    {
        var (memberId, productId, _) = PickProduct(1);
        await Assert.ThrowsAsync<OrderException>(() => UseService((s, _) => s.CreateAsync(memberId, [new OrderLine(productId, 0)])));
    }

    [Fact]
    public async Task 取消待付款訂單會把庫存加回()
    {
        var (memberId, productId, stock) = PickProduct(2);
        var order = await UseService((s, _) => s.CreateAsync(memberId, [new OrderLine(productId, 2)]));

        await UseService(async (s, _) => { await s.CancelAsync(order.Id); return 0; });

        Assert.Equal(stock, StockOf(productId));
        Assert.Equal(OrderStatus.Cancelled, _app.WithDb(db => db.Orders.AsNoTracking().First(o => o.Id == order.Id).Status));
    }

    [Fact]
    public async Task 未付款不能出貨_已出貨不能取消()
    {
        var (memberId, productId, _) = PickProduct(1);
        var order = await UseService((s, _) => s.CreateAsync(memberId, [new OrderLine(productId, 1)]));

        await Assert.ThrowsAsync<OrderException>(() => UseService(async (s, _) => { await s.MarkShippedAsync(order.Id); return 0; }));

        var shipped = _app.WithDb(db => db.Orders.AsNoTracking().First(o => o.Status == OrderStatus.Shipped).Id);
        await Assert.ThrowsAsync<OrderException>(() => UseService(async (s, _) => { await s.CancelAsync(shipped); return 0; }));
    }
}
