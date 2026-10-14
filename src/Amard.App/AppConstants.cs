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
    ///  - دستگاه واقعی (POS): حتماً IP شبکه‌ی کامپیوتر یا آدرس اینترنتی سرور
    ///    (مثلاً 192.168.1.5 یا https://api.example.com) را در تنظیمات
    ///    «آدرس سرور برای دستگاه واقعی» قرار دهید. مقدار «localhost» روی دستگاه
    ///    واقعی به خودِ دستگاه POS اشاره می‌کند و هرگز به سرور نمی‌رسد!
    /// ساختار آدرس (scheme، پورت و اسلش انتهایی) به‌صورت خودکار نرمال می‌شود.
    /// </summary>
    public static string ApiBaseUrl
    {
        get
        {
            if (IsRunningOnEmulator())
                return BuildBaseUrl(AppSettingsService.EmulatorApiHost, ApiPort, ApiBasePath);

            return BuildBaseUrl(AppSettingsService.DeviceApiHost, ApiPort, ApiBasePath);
        }
    }

    /// <summary>
    /// تلاش برای ساخت آدرس پایه بدون پرتاب خطا (برای نمایش در UI).
    /// در صورت نامعتبر بودن تنظیمات، به‌جای آدرس، متن خطای فارسی برمی‌گرداند.
    /// </summary>
    public static bool TryGetApiBaseUrl(out string url, out string error)
    {
        try
        {
            url = ApiBaseUrl;
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            url = string.Empty;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// هشدار درباره بخش‌های عددی مسیر که شبیه پورت‌اند (مثل ‎/443‎).
    /// پورت باید بعد از دامنه با دو نقطه نوشته شود (دامنه:443) نه داخل مسیر؛
    /// 443 پورت پیش‌فرض https است و اصلاً نیازی به نوشتن ندارد.
    /// اگر سرور واقعاً همین مسیر را می‌خواهد، این هشدار را نادیده بگیرید.
    /// خروجی null یعنی هشداری نیست.
    /// </summary>
    public static string? GetPortInPathWarning()
    {
        if (!TryGetApiBaseUrl(out var baseUrl, out _))
            return null;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            return null;

        foreach (var seg in uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if ((seg is "80" or "443" or "8008" or "8443" or "8000" or "5000") &&
                int.TryParse(seg, out _))
                return $"⚠ در مسیر آدرس، بخش «/{seg}» دیده می‌شود. اگر منظورتان پورت {seg} است، " +
                       $"باید بعد از دامنه با دو نقطه نوشته شود (مثل myserver.com:{seg}) نه داخل مسیر. " +
                       $"پورت 443 پورت پیش‌فرض https است و معمولاً اصلاً نباید نوشته شود.";
        }

        return null;
    }

    /// <summary>پورت پیش‌فرض scheme (443 برای https و 80 برای http)</summary>
    private static int GetSchemeDefaultPort(string scheme) =>
        string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80;

    /// <summary>
    /// ساخت آدرس پایه‌ی نرمال‌شده از (هاست، پورت، مسیر پایه):
    ///  - اگر scheme (http/https) نباشد، http:// اضافه می‌شود
    ///    (علت اصلی خطای connection روی دستگاه: آدرس «localhost:8008/...» بدون scheme نامعتبر است).
    ///  - اگر هاست خودش پورت صریح داشته باشد (مثل http://192.168.1.5:9000)، همان استفاده می‌شود.
    ///  - اگر آدرس https بدون پورت داده شود (API خارجی مثل https://api.example.com)،
    ///    پورت توسعه (8008) به آن اضافه نمی‌شود چون سرور خارجی روی پورت استاندارد 443 است.
    ///  - اگر هاست خودش مسیر (path) داشته باشد (URL کامل)، با مسیر پایه ترکیب می‌شود.
    /// </summary>
    /// <exception cref="InvalidOperationException">در صورت نامعتبر بودن هاست یا پورت، با پیام فارسی</exception>
    public static string BuildBaseUrl(string host, int port, string basePath)
    {
        var rawHost = (host ?? string.Empty).Trim().Replace(" ", string.Empty);
        if (string.IsNullOrWhiteSpace(rawHost))
            throw new InvalidOperationException(
                "آدرس سرور تنظیم نشده است. در صفحه «تنظیمات»، فیلد «آدرس سرور برای دستگاه واقعی» را وارد کنید.");

        if (!rawHost.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !rawHost.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            rawHost = "http://" + rawHost;

        if (!Uri.TryCreate(rawHost, UriKind.Absolute, out var hostUri) ||
            (hostUri.Scheme != Uri.UriSchemeHttp && hostUri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(hostUri.Host))
            throw new InvalidOperationException(
                $"آدرس سرور نامعتبر است: «{host}». مثال معتبر: http://192.168.1.5 یا https://api.example.com");

        bool isHttps = string.Equals(hostUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

        string authority;
        if (!hostUri.IsDefaultPort)
        {
            // هاست خودش پورت صریح دارد (مثل http://192.168.1.5:9000) — همان استفاده می‌شود
            authority = hostUri.Authority;
        }
        else if (port == GetSchemeDefaultPort(hostUri.Scheme) ||
                 (isHttps && port == AppSettingsService.DefaultApiPort))
        {
            // بدون پورت صریح، در این دو حالت پورتی نوشته نمی‌شود:
            //  1) پورتِ برابرِ پیش‌فرض scheme (443 برای https و 80 برای http)؛
            //     پس اگر کاربر «443» را در فیلد پورت وارد کند هم آدرس تمیز می‌ماند.
            //  2) مقدار نگهبانِ «خالی» (8008) روی https — چون سرور https خارجی
            //     روی 443 است نه پورت محیط توسعه.
            // توجه: http + 8008 عمداً حذف نمی‌شود چون پورت واقعی سرور توسعه است.
            authority = hostUri.Authority; // بدون پورت (براکت IPv6 هم حفظ می‌شود)
        }
        else
        {
            if (port is < 1 or > 65535)
                throw new InvalidOperationException($"پورت سرور نامعتبر است: {port}");
            // DnsSafeHost براکت IPv6 را حذف می‌کند؛ برای ساخت authority معتبر دوباره براکت می‌گذاریم
            var hostPart = hostUri.HostNameType == UriHostNameType.IPv6 ? $"[{hostUri.DnsSafeHost}]" : hostUri.DnsSafeHost;
            authority = $"{hostPart}:{port}";
        }

        var cleanBase = (basePath ?? string.Empty).Trim().Trim('/');
        var existingPath = hostUri.AbsolutePath.Trim('/');
        string combinedPath;
        if (!string.IsNullOrEmpty(existingPath) && !string.IsNullOrEmpty(cleanBase))
        {
            // اگر کاربر URL کامل (شامل مسیر) وارد کرده و انتهای آن همان مسیر پایه است، تکرار نکن
            combinedPath = existingPath.EndsWith(cleanBase, StringComparison.OrdinalIgnoreCase)
                ? existingPath
                : $"{existingPath}/{cleanBase}";
        }
        else if (!string.IsNullOrEmpty(existingPath))
        {
            combinedPath = existingPath;
        }
        else
        {
            combinedPath = cleanBase;
        }

        // اسلش‌های تکراری داخل مسیر (مثل 443//api) عملاً همیشه اشتباه تایپی‌اند و
        // روی بیشتر سرورها 404 می‌دهند؛ پس به تک‌اسلش تبدیل می‌شوند
        while (combinedPath.Contains("//", StringComparison.Ordinal))
            combinedPath = combinedPath.Replace("//", "/", StringComparison.Ordinal);

        return string.IsNullOrEmpty(combinedPath)
            ? $"{hostUri.Scheme}://{authority}/"
            : $"{hostUri.Scheme}://{authority}/{combinedPath}/";
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

    /// <summary>
    /// نام کلاس سرویس POS (com.persianswitch.smartpos.coreservice.ApplicationService).
    /// در صورت پاسخ ندادن اکشن، با ComponentName مستقیم به همین کلاس متصل می‌شویم.
    /// از تنظیمات خوانده می‌شود تا «شناسایی خودکار» بتواند مقدار واقعی دستگاه را ذخیره کند.
    /// </summary>
    public static string PosServiceClass => AppSettingsService.PosServiceClass;

    /// <summary>کد تراکنش خرید (POSServiceTransactionCode.PURCHASE)</summary>
    public static int TransactionCodePurchase => AppSettingsService.TransactionCodePurchase;

    /// <summary>کد تراکنش قبض خدماتی (SERVICE_BILL)</summary>
    public static int TransactionCodeServiceBill => AppSettingsService.TransactionCodeServiceBill;

    /// <summary>شناسه host - از اسان پرداخت دریافت می‌شود</summary>
    public static int HostId => AppSettingsService.HostId;

    /// <summary>
    /// کلید API میزبان (Host Api Key) - از اسان پرداخت دریافت می‌شود.
    /// طبق مستندات «اتصال به پایانه‌های فروش اندرویدی آسان پرداخت» این مقدار در فیلد «hak» داخل
    /// HostRequest ارسال می‌شود.
    /// </summary>
    public static string HostApiKey => AppSettingsService.HostApiKey;

    /// <summary>
    /// فرمت زمان درخواست (فیلد hrt) مطابق مستندات و نمونه رسمی اسان پرداخت.
    /// نکته: در الگوی Java («yyyy-MM-dd'T'HH:mm:ss.SSS-Z») کاراکتر Z به‌صورت RFC822
    /// (مثلاً +0330) تولید می‌شود.
    /// </summary>
    public const string HostReqTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff";

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
