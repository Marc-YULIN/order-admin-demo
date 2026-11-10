namespace OrderAdminDemo.Data;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";

    public static string DisplayName(string role) => role == Admin ? "管理員" : "店員";
}

public class AppUser
{
    public int Id { get; set; }
    public required string UserName { get; set; }
    public required string DisplayName { get; set; }
    public string PasswordHash { get; set; } = "";
    public required string Role { get; set; }
}

public class Member
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Phone { get; set; }
    public string? Email { get; set; }
    public string? City { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<Order> Orders { get; set; } = [];
}

public class Product
{
    public int Id { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public int Price { get; set; }
    public int Stock { get; set; }

    /// <summary>庫存低於此數量時，列表標紅並列入儀表板。</summary>
    public int SafetyStock { get; set; }

    public bool IsLowStock => Stock < SafetyStock;
}

public enum OrderStatus
{
    PendingPayment = 0,
    Paid = 1,
    Shipped = 2,
    Cancelled = 3,
}

public static class OrderStatusText
{
    public static string Of(OrderStatus s) => s switch
    {
        OrderStatus.PendingPayment => "待付款",
        OrderStatus.Paid => "已付款",
        OrderStatus.Shipped => "已出貨",
        OrderStatus.Cancelled => "已取消",
        _ => s.ToString(),
    };

    public static string BadgeClass(OrderStatus s) => s switch
    {
        OrderStatus.PendingPayment => "text-bg-warning",
        OrderStatus.Paid => "text-bg-success",
        OrderStatus.Shipped => "text-bg-primary",
        _ => "text-bg-secondary",
    };
}

public class Order
{
    public int Id { get; set; }
    public required string OrderNo { get; set; }
    public int MemberId { get; set; }
    public Member? Member { get; set; }
    public DateTime CreatedAt { get; set; }
    public OrderStatus Status { get; set; }
    public int Total { get; set; }
    public List<OrderItem> Items { get; set; } = [];

    /// <summary>最近一次送去綠界的特店交易編號（每次付款嘗試都不同，綠界不允許重複）。</summary>
    public string? MerchantTradeNo { get; set; }

    /// <summary>綠界交易編號，付款成功後由通知寫入。</summary>
    public string? PaymentTradeNo { get; set; }
    public string? PaymentType { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? ShippedAt { get; set; }
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public required string ProductName { get; set; }
    public int UnitPrice { get; set; }
    public int Quantity { get; set; }
    public int Subtotal => UnitPrice * Quantity;
}

/// <summary>每一筆收到的綠界通知都留紀錄，方便對帳與追查。</summary>
public class PaymentNotification
{
    public int Id { get; set; }
    public DateTime ReceivedAt { get; set; }
    public required string Source { get; set; }
    public string? MerchantTradeNo { get; set; }
    public required string RawBody { get; set; }
    public required string Result { get; set; }
}
