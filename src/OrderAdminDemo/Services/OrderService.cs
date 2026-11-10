using Microsoft.EntityFrameworkCore;
using OrderAdminDemo.Data;

namespace OrderAdminDemo.Services;

public class OrderException(string message) : Exception(message);

public record OrderLine(int ProductId, int Quantity);

/// <summary>訂單的狀態轉換與庫存異動都集中在這裡，頁面只負責呼叫。</summary>
public class OrderService(AppDbContext db, TimeProvider clock)
{
    public async Task<Order> CreateAsync(int memberId, IReadOnlyList<OrderLine> lines)
    {
        var wanted = lines.Where(l => l.Quantity > 0)
            .GroupBy(l => l.ProductId)
            .Select(g => new OrderLine(g.Key, g.Sum(x => x.Quantity)))
            .ToList();
        if (wanted.Count == 0) throw new OrderException("請至少選擇一項商品。");
        if (!await db.Members.AnyAsync(m => m.Id == memberId)) throw new OrderException("找不到這位會員。");

        var ids = wanted.Select(w => w.ProductId).ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        // 先全部檢查完再扣，避免扣到一半才發現某項不足
        var shortages = wanted
            .Where(w => !products.TryGetValue(w.ProductId, out var p) || p.Stock < w.Quantity)
            .Select(w => products.TryGetValue(w.ProductId, out var p) ? $"{p.Name}（剩 {p.Stock}）" : $"商品 #{w.ProductId}")
            .ToList();
        if (shortages.Count > 0) throw new OrderException("庫存不足：" + string.Join("、", shortages));

        var now = clock.GetLocalNow().DateTime;
        var order = new Order
        {
            OrderNo = $"OD{now:yyMMddHHmmss}{Random.Shared.Next(10, 99)}",
            MemberId = memberId,
            CreatedAt = now,
            Status = OrderStatus.PendingPayment,
        };
        foreach (var w in wanted)
        {
            var p = products[w.ProductId];
            p.Stock -= w.Quantity;
            order.Items.Add(new OrderItem { ProductId = p.Id, ProductName = p.Name, UnitPrice = p.Price, Quantity = w.Quantity });
        }
        order.Total = order.Items.Sum(i => i.UnitPrice * i.Quantity);
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    /// <summary>取消待付款訂單，並把庫存加回去。</summary>
    public async Task CancelAsync(int orderId)
    {
        var order = await LoadAsync(orderId);
        if (order.Status != OrderStatus.PendingPayment)
            throw new OrderException($"只有待付款的訂單可以取消（目前：{OrderStatusText.Of(order.Status)}）。");

        var ids = order.Items.Select(i => i.ProductId).ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        foreach (var item in order.Items)
            if (products.TryGetValue(item.ProductId, out var p)) p.Stock += item.Quantity;

        order.Status = OrderStatus.Cancelled;
        await db.SaveChangesAsync();
    }

    public async Task MarkShippedAsync(int orderId)
    {
        var order = await LoadAsync(orderId);
        if (order.Status != OrderStatus.Paid)
            throw new OrderException($"只有已付款的訂單可以出貨（目前：{OrderStatusText.Of(order.Status)}）。");
        order.Status = OrderStatus.Shipped;
        order.ShippedAt = clock.GetLocalNow().DateTime;
        await db.SaveChangesAsync();
    }

    private async Task<Order> LoadAsync(int orderId) =>
        await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId)
        ?? throw new OrderException("找不到這筆訂單。");
}
