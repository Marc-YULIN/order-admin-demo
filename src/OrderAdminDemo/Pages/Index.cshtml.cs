using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrderAdminDemo.Data;

namespace OrderAdminDemo.Pages;

public class IndexModel(AppDbContext db, TimeProvider clock) : PageModel
{
    public int MonthRevenue { get; private set; }
    public int MonthOrders { get; private set; }
    public int PendingPayment { get; private set; }
    public int ToShip { get; private set; }
    public List<Product> LowStock { get; private set; } = [];
    public List<Order> Recent { get; private set; } = [];
    public List<(DateTime Day, int Count)> Last14Days { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var now = clock.GetLocalNow().DateTime;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var month = db.Orders.Where(o => o.CreatedAt >= monthStart && o.Status != OrderStatus.Cancelled);
        MonthOrders = await month.CountAsync();
        // 營收只算已付款、已出貨；店員看不到這個數字（頁面上也不顯示）
        if (User.IsInRole(Roles.Admin))
            MonthRevenue = await month.Where(o => o.Status == OrderStatus.Paid || o.Status == OrderStatus.Shipped).SumAsync(o => o.Total);

        PendingPayment = await db.Orders.CountAsync(o => o.Status == OrderStatus.PendingPayment);
        ToShip = await db.Orders.CountAsync(o => o.Status == OrderStatus.Paid);
        LowStock = await db.Products.Where(p => p.Stock < p.SafetyStock).OrderBy(p => p.Stock).ToListAsync();
        Recent = await db.Orders.Include(o => o.Member).OrderByDescending(o => o.CreatedAt).Take(8).ToListAsync();

        var from = now.Date.AddDays(-13);
        var days = await db.Orders.Where(o => o.CreatedAt >= from && o.Status != OrderStatus.Cancelled)
            .Select(o => o.CreatedAt).ToListAsync();
        Last14Days = Enumerable.Range(0, 14).Select(i => from.AddDays(i))
            .Select(d => (d, days.Count(x => x.Date == d))).ToList();
    }
}
