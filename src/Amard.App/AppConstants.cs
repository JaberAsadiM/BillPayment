namespace Amard.App;

using Amard.App.Services;

/// <summary>
/// تنظیمات برنامه.
/// مقادیر از تنظیمات ذخیره‌شده روی گوشی (<see cref="AppSettingsService"/>) خوانده می‌شوند؛
/// در نبود مقدار ذخیره‌شده، مقدار پیش‌فرض (مقدار اولیه) استفاده می‌شود.
/// کاربر می‌تواند این مقادیر را از صفحه‌ی «تنظیمات» داخل برنامه تغییر دهد.
/// </summary>
public static class AppConstants
{
    /// <summary>پورت سرور API روی کامپیوتر توسعه</summary>
    public static int ApiPort => AppSettingsService.ApiPort;

    /// <summary>مسیر پایه سرور API</summary>
    public static string ApiBasePath => AppSettingsService.ApiBasePath;

    /// <summary>
    /// آدرس پایه سرور API (بر اساس تنظیمات ذخیره‌شده).
    /// توجه: در اندروید «localhost» به خودِ دستگاه اندروید اشاره می‌کند نه کامپیوتر توسعه!
    ///  - شبیه‌ساز اندروید: باید از 10.0.2.2 استفاده شود (alias ویژه برای loopback کامپیوتر میزبان).
    ///  - دستگاه واقعی: یا «adb reverse tcp:PORT tcp:PORT» اجرا کنید (آنگاه localhost کار می‌کند)
    ///    یا IP شبکه‌ی کامپیوتر را در تنظیمات «آدرس سرور برای دستگاه واقعی» قرار دهید.
    /// </summary>
    public static string ApiBaseUrl
    {
        get
        {
            if (IsRunningOnEmulator())
                return $"{AppSettingsService.EmulatorApiHost}:{ApiPort}/{ApiBasePath}";

            return $"{DeviceApiHost}:{ApiPort}/{ApiBasePath}";
        }
    }

    /// <summary>
    /// آدرس کامپیوتر توسعه برای دستگاه‌های واقعی.
    /// اگر از «adb reverse tcp:28627 tcp:28627» استفاده می‌کنید همان "localhost" بماند؛
    /// در غیر این صورت IP شبکه‌ی کامپیوتر (مثلاً 192.168.1.5) را در صفحه‌ی تنظیمات وارد کنید.
    /// </summary>
    public static string DeviceApiHost => AppSettingsService.DeviceApiHost;

    /// <summary>آدرس سرور API هنگام اجرای روی شبیه‌ساز اندروید</summary>
    public static string EmulatorApiHost => AppSettingsService.EmulatorApiHost;

    /// <summary>شناسه پکیج سرویس POS اسان پرداخت</summary>
    public static string PosServicePackage => AppSettingsService.PosServicePackage;

    /// <summary>اکشن اتصال به سرویس IPosService</summary>
    public static string PosServiceAction => AppSettingsService.PosServiceAction;

    /// <summary>کد تراکنش خرید (POSServiceTransactionCode.PURCHASE)</summary>
    public static int TransactionCodePurchase => AppSettingsService.TransactionCodePurchase;

    /// <summary>کد تراکنش قبض خدماتی (SERVICE_BILL)</summary>
    public static int TransactionCodeServiceBill => AppSettingsService.TransactionCodeServiceBill;

    /// <summary>شناسه host - از اسان پرداخت دریافت می‌شود</summary>
    public static int HostId => AppSettingsService.HostId;

    /// <summary>زبان SDK دستگاه</summary>
    public static string Lang => AppSettingsService.Lang;

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
}
