using Bogus;
using Microsoft.AspNetCore.Identity;

namespace OrderAdminDemo.Data;

/// <summary>
/// 第一次啟動時產生示範資料（固定亂數種子，每次結果相同）。
/// 示範帳號只用於本機展示，正式環境請改成由管理員建立帳號。
/// </summary>
public static class SeedData
{
    public const string AdminUser = "admin";
    public const string AdminPassword = "Demo@Admin1";
    public const string StaffUser = "staff";
    public const string StaffPassword = "Demo@Staff1";

    private static readonly (string Category, string Name, int Price)[] Catalog =
    [
        ("咖啡豆", "衣索比亞 耶加雪菲 半磅", 480),
        ("咖啡豆", "哥倫比亞 慧蘭 半磅", 420),
        ("咖啡豆", "瓜地馬拉 安提瓜 半磅", 450),
        ("咖啡豆", "肯亞 AA 半磅", 520),
        ("咖啡豆", "巴西 喜拉朵 半磅", 380),
        ("咖啡豆", "曼特寧 黃金 半磅", 460),
        ("咖啡豆", "招牌綜合豆 一磅", 650),
        ("咖啡豆", "低咖啡因 綜合 半磅", 440),
        ("掛耳包", "經典綜合掛耳 10 入", 250),
        ("掛耳包", "淺焙果香掛耳 10 入", 280),
        ("掛耳包", "深焙醇厚掛耳 10 入", 260),
        ("掛耳包", "單品綜合掛耳禮盒 20 入", 520),
        ("器材", "手沖濾杯 V60 陶瓷", 690),
        ("器材", "細口手沖壺 600ml", 1280),
        ("器材", "電子秤（附計時）", 890),
        ("器材", "手搖磨豆機", 1580),
        ("器材", "濾紙 100 張", 120),
        ("器材", "玻璃分享壺 500ml", 450),
        ("周邊", "品牌馬克杯", 380),
        ("周邊", "保溫隨行杯 470ml", 690),
        ("周邊", "帆布托特包", 450),
        ("周邊", "密封咖啡罐", 320),
        ("周邊", "禮品卡 NT$500", 500),
        ("周邊", "禮品卡 NT$1000", 1000),
    ];

    public static void Run(AppDbContext db, DateTime now)
    {
        if (db.Users.Any()) return;

        var hasher = new PasswordHasher<AppUser>();
        var admin = new AppUser { UserName = AdminUser, DisplayName = "王店長", Role = Roles.Admin };
        admin.PasswordHash = hasher.HashPassword(admin, AdminPassword);
        var staff = new AppUser { UserName = StaffUser, DisplayName = "林小美", Role = Roles.Staff };
        staff.PasswordHash = hasher.HashPassword(staff, StaffPassword);
        db.Users.AddRange(admin, staff);

        Randomizer.Seed = new Random(20261110);
        var faker = new Faker("zh_TW");

        var products = Catalog.Select((c, i) => new Product
        {
            Sku = $"P{i + 1:000}",
            Name = c.Name,
            Category = c.Category,
            Price = c.Price,
            SafetyStock = c.Category == "器材" ? 5 : 15,
            Stock = faker.Random.Int(0, 80),
        }).ToList();
        // 固定幾個低庫存商品，讓儀表板有東西可看
        products[3].Stock = 4;
        products[13].Stock = 2;
        products[17].Stock = 0;
        db.Products.AddRange(products);

        string[] cities = ["台北市", "新北市", "桃園市", "台中市", "台南市", "高雄市", "新竹市", "基隆市"];
        var members = Enumerable.Range(1, 60).Select(_ =>
        {
            var name = faker.Name.LastName() + faker.Name.FirstName();
            return new Member
            {
                Name = name,
                Phone = "09" + faker.Random.ReplaceNumbers("########"),
                Email = $"member{faker.Random.Int(1000, 9999)}@example.com",
                City = faker.PickRandom(cities),
                CreatedAt = now.AddDays(-faker.Random.Int(30, 400)).Date,
            };
        }).ToList();
        db.Members.AddRange(members);
        db.SaveChanges();

        var orders = new List<Order>();
        for (var i = 0; i < 320; i++)
        {
            var created = now.AddDays(-faker.Random.Double(0, 90));
            var picked = faker.PickRandom(products, faker.Random.Int(1, 3)).ToList();
            var order = new Order
            {
                OrderNo = $"OD{created:yyMMdd}{i + 1:0000}",
                MemberId = faker.PickRandom(members).Id,
                CreatedAt = created,
                Items = picked.Select(p => new OrderItem
                {
                    ProductId = p.Id,
                    ProductName = p.Name,
                    UnitPrice = p.Price,
                    Quantity = faker.Random.Int(1, 3),
                }).ToList(),
            };
            order.Total = order.Items.Sum(x => x.UnitPrice * x.Quantity);

            // 越舊的訂單越可能已出貨；最近幾天的多半還在待付款
            var age = (now - created).TotalDays;
            order.Status = age switch
            {
                < 2 => faker.Random.WeightedRandom([OrderStatus.PendingPayment, OrderStatus.Paid], [0.6f, 0.4f]),
                < 7 => faker.Random.WeightedRandom([OrderStatus.PendingPayment, OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Cancelled], [0.15f, 0.35f, 0.45f, 0.05f]),
                _ => faker.Random.WeightedRandom([OrderStatus.Shipped, OrderStatus.Cancelled], [0.92f, 0.08f]),
            };
            if (order.Status is OrderStatus.Paid or OrderStatus.Shipped)
            {
                order.PaidAt = created.AddMinutes(faker.Random.Int(3, 120));
                order.PaymentType = faker.PickRandom("Credit_CreditCard", "ATM_LAND", "WebATM_TAISHIN");
                order.PaymentTradeNo = faker.Random.ReplaceNumbers("2#############")[..14];
            }
            if (order.Status == OrderStatus.Shipped)
                order.ShippedAt = order.PaidAt!.Value.AddDays(faker.Random.Int(1, 3));
            orders.Add(order);
        }
        db.Orders.AddRange(orders);
        db.SaveChanges();
    }
}

