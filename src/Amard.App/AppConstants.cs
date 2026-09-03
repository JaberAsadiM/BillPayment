namespace Amard.App;

/// <summary>تنظیمات ثابت برنامه - این مقادیر را بر اساس محیط واقعی تغییر دهید</summary>
public static class AppConstants
{
    /// <summary>پورت سرور API روی کامپیوتر توسعه</summary>
    public const int ApiPort = 8008;

    /// <summary>مسیر پایه سرور API</summary>
    public const string ApiBasePath = "api/PaymentAvarez/";

    /// <summary>
    /// آدرس پایه سرور API.
    /// توجه: در اندروید «localhost» به خودِ دستگاه اندروید اشاره می‌کند نه کامپیوتر توسعه!
    ///  - شبیه‌ساز اندروید: باید از 10.0.2.2 استفاده شود (alias ویژه برای loopback کامپیوتر میزبان).
    ///  - دستگاه واقعی: یا «adb reverse tcp:PORT tcp:PORT» اجرا کنید (آنگاه localhost کار می‌کند)
    ///    یا IP شبکه‌ی کامپیوتر را در <see cref="DeviceApiHost"/> قرار دهید.
    /// </summary>
    public static string ApiBaseUrl
    {
        get
        {
            if (IsRunningOnEmulator())
                return $"http://192.168.1.2:{ApiPort}/{ApiBasePath}";

            return $"http://{DeviceApiHost}:{ApiPort}/{ApiBasePath}";
        }
    }

    /// <summary>
    /// آدرس کامپیوتر توسعه برای دستگاه‌های واقعی.
    /// اگر از «adb reverse tcp:28627 tcp:28627» استفاده می‌کنید همان "localhost" بماند؛
    /// در غیر این صورت IP شبکه‌ی کامپیوتر (مثلاً 192.168.1.5) را بگذارید.
    /// </summary>
    public const string DeviceApiHost = "localhost";

    /// <summary>تشخیص اجرای برنامه روی شبیه‌ساز اندروید</summary>
    private static bool IsRunningOnEmulator()
    {
        var fingerprint = Android.OS.Build.Fingerprint ?? string.Empty;
        var model = Android.OS.Build.Model ?? string.Empty;
        var product = Android.OS.Build.Product ?? string.Empty;
        var hardware = Android.OS.Build.Hardware ?? string.Empty;

        return fingerprint.StartsWith("generic", StringComparison.OrdinalIgnoreCase)
            || fingerprint.Contains("emulator", StringComparison.OrdinalIgnoreCase)
            || fingerprint.Contains("sdk_gphone", StringComparison.OrdinalIgnoreCase)
            || model.Contains("Emulator", StringComparison.OrdinalIgnoreCase)
            || model.Contains("sdk_gphone", StringComparison.OrdinalIgnoreCase)
            || product.Contains("sdk", StringComparison.OrdinalIgnoreCase)
            || hardware.Contains("ranchu", StringComparison.OrdinalIgnoreCase)
            || hardware.Contains("goldfish", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>شناسه پکیج سرویس POS اسان پرداخت</summary>
    public const string PosServicePackage = "com.persianswitch.smartpos";

    /// <summary>اکشن اتصال به سرویس IPosService</summary>
    public const string PosServiceAction = "com.persianswitch.smartpos.aidl.IPosService";

    /// <summary>کد تراکنش خرید (POSServiceTransactionCode.PURCHASE)</summary>
    public const int TransactionCodePurchase = 1502;

    /// <summary>کد تراکنش قبض خدماتی (SERVICE_BILL)</summary>
    public const int TransactionCodeServiceBill = 1503;

    /// <summary>شناسه host - از اسان پرداخت دریافت می‌شود</summary>
    public const int HostId = 1;

    /// <summary>زبان SDK دستگاه</summary>
    public const string Lang = "fa";
}
