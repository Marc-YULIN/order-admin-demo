using OrderAdminDemo.Services.Ecpay;

namespace OrderAdminDemo.Tests;

public class EcpayCheckMacTests
{
    // 綠界官方文件的計算範例：https://developers.ecpay.com.tw/?p=2902
    private static readonly Dictionary<string, string> DocExample = new()
    {
        ["TradeDesc"] = "促銷方案",
        ["PaymentType"] = "aio",
        ["MerchantTradeDate"] = "2023/03/12 15:30:23",
        ["MerchantTradeNo"] = "ecpay20230312153023",
        ["MerchantID"] = "3002607",
        ["ReturnURL"] = "https://www.ecpay.com.tw/receive.php",
        ["ItemName"] = "Apple iphone 15",
        ["TotalAmount"] = "30000",
        ["ChoosePayment"] = "ALL",
        ["EncryptType"] = "1",
    };

    private const string DocExpected = "6C51C9E6888DE861FD62FB1DD17029FC742634498FD813DC43D4243B5685B840";

    [Fact]
    public void 與官方文件範例結果相同()
    {
        Assert.Equal(DocExpected, EcpayCheckMac.Compute(DocExample, TestApp.HashKey, TestApp.HashIv));
    }

    [Fact]
    public void 參數順序不影響結果()
    {
        var reversed = DocExample.Reverse().ToDictionary();
        Assert.Equal(DocExpected, EcpayCheckMac.Compute(reversed, TestApp.HashKey, TestApp.HashIv));
    }

    [Fact]
    public void 驗證時忽略CheckMacValue本身且不分大小寫()
    {
        var signed = new Dictionary<string, string>(DocExample) { ["CheckMacValue"] = DocExpected.ToLowerInvariant() };
        Assert.True(EcpayCheckMac.Verify(signed, TestApp.HashKey, TestApp.HashIv));
    }

    [Fact]
    public void 任何欄位被竄改都驗證失敗()
    {
        var tampered = new Dictionary<string, string>(DocExample) { ["TotalAmount"] = "1", ["CheckMacValue"] = DocExpected };
        Assert.False(EcpayCheckMac.Verify(tampered, TestApp.HashKey, TestApp.HashIv));
    }

    [Fact]
    public void 缺少CheckMacValue驗證失敗()
    {
        Assert.False(EcpayCheckMac.Verify(DocExample, TestApp.HashKey, TestApp.HashIv));
    }
}
