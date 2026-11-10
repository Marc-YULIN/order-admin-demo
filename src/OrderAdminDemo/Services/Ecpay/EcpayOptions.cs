namespace OrderAdminDemo.Services.Ecpay;

/// <summary>
/// 綠界設定。MerchantId／HashKey／HashIV 請用 user-secrets 或環境變數設定，不要寫進 appsettings。
/// 本機示範用綠界官方文件公開的測試商店值（見 README）。
/// </summary>
public class EcpayOptions
{
    public const string Section = "Ecpay";

    public string MerchantId { get; set; } = "";
    public string HashKey { get; set; } = "";
    public string HashIv { get; set; } = "";

    /// <summary>預設是綠界測試環境；正式上線改成 https://payment.ecpay.com.tw/Cashier/AioCheckOut/V5。</summary>
    public string CheckoutUrl { get; set; } = "https://payment-stage.ecpay.com.tw/Cashier/AioCheckOut/V5";

    /// <summary>
    /// 綠界伺服器要能連到的網址（例如 dev tunnel 網址），用來組 ReturnURL。
    /// 沒設定時用目前請求的網址：本機 localhost 收不到綠界的背景通知，但付款完成頁仍會回來。
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(MerchantId) && !string.IsNullOrWhiteSpace(HashKey) && !string.IsNullOrWhiteSpace(HashIv);
}
