using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace OrderAdminDemo.Services.Ecpay;

/// <summary>
/// 綠界 CheckMacValue（SHA256）。步驟依官方文件：
/// 參數依名稱排序 → 前加 HashKey、後加 HashIV → URL encode → 轉小寫 → SHA256 → 轉大寫。
/// https://developers.ecpay.com.tw/?p=2902
/// </summary>
public static class EcpayCheckMac
{
    public static string Compute(IEnumerable<KeyValuePair<string, string>> parameters, string hashKey, string hashIv)
    {
        var joined = string.Join("&", parameters
            .Where(p => !string.Equals(p.Key, "CheckMacValue", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .Select(p => $"{p.Key}={p.Value}"));
        var raw = $"HashKey={hashKey}&{joined}&HashIV={hashIv}";

        var encoded = HttpUtility.UrlEncode(raw).ToLowerInvariant();
        // 綠界的 .NET 編碼對照表：這幾個字元要維持原樣（HttpUtility 本來就不編碼，保留以防不同編碼器）
        encoded = encoded
            .Replace("%2d", "-").Replace("%5f", "_").Replace("%2e", ".")
            .Replace("%21", "!").Replace("%2a", "*").Replace("%28", "(").Replace("%29", ")");

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(encoded));
        return Convert.ToHexString(hash); // 大寫
    }

    /// <summary>用固定時間比較，避免從回應時間猜出正確值。</summary>
    public static bool Verify(IEnumerable<KeyValuePair<string, string>> parameters, string hashKey, string hashIv)
    {
        var list = parameters.ToList();
        var received = list.FirstOrDefault(p => string.Equals(p.Key, "CheckMacValue", StringComparison.OrdinalIgnoreCase)).Value;
        if (string.IsNullOrEmpty(received)) return false;

        var expected = Compute(list, hashKey, hashIv);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(received.ToUpperInvariant()));
    }
}
