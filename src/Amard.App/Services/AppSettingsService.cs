namespace Amard.App.Services;

/// <summary>
/// ذخیره و بازیابی تنظیمات برنامه روی حافظه‌ی دائمی گوشی (MAUI Preferences).
/// مقادیر پیش‌فرض همان مقادیر اولیه‌ی AppConstants هستند؛
/// تا زمانی که کاربر مقداری وارد و ذخیره نکند، همان پیش‌فرض‌ها استفاده می‌شوند.
/// </summary>
public static class AppSettingsService
{
    private const string Prefix = "AppSettings.";

    private static string Key(string name) => Prefix + name;

    // ----- مقادیر پیش‌فرض (مطابق مقادیر اولیه‌ی AppConstants) -----
    public const int DefaultApiPort = 8008;
    public const string DefaultApiBasePath = "api/PaymentAvarez/";
    public const string DefaultDeviceApiHost = "localhost";
    public const string DefaultEmulatorApiHost = "http://192.168.1.2";
    public const string DefaultPosServicePackage = "com.persianswitch.smartpos";

    /// <summary>
    /// اکشن سرویس POS اسان پرداخت. طبق نمونه رسمی اسان پرداخت (پروژه smartposclientexample)
    /// برای bind شدن به «نرم افزار پرداخت پایانه» از همین اکشن استفاده می‌شود.
    /// </summary>
    public const string DefaultPosServiceAction = "com.persianswitch.smartpos.service";

    /// <summary>
    /// اکشن اشتباه نسخه‌های قبلی برنامه (نام اینترفیس AIDL به‌جای اکشن سرویس) که در مهاجرت اصلاح می‌شود.
    /// </summary>
    public const string LegacyPosServiceAction = "com.persianswitch.smartpos.aidl.IPosService";

    /// <summary>کلاس سرویس (ComponentName) برای اتصال مستقیم در صورت عدم پاسخ اکشن</summary>
    public const string DefaultPosServiceClass = "com.persianswitch.smartpos.coreservice.ApplicationService";

    public const int DefaultTransactionCodePurchase = 1502;
    public const int DefaultTransactionCodeServiceBill = 1503;

    /// <summary>شناسه host (Host Id) دریافتی از اسان پرداخت برای این اپلیکیشن میزبان</summary>
    public const int DefaultHostId = 2040;

    /// <summary>کلید API (Host Api Key) دریافتی از اسان پرداخت برای این اپلیکیشن میزبان</summary>
    public const string DefaultHostApiKey = "k7m2x9p4q8v1n6t3r5w0z8c2d9f4h1j6";

    public const string DefaultLang = "fa";

    /// <summary>پورت سرور API</summary>
    public static int ApiPort
    {
        get => Preferences.Get(Key(nameof(ApiPort)), DefaultApiPort);
        set => Preferences.Set(Key(nameof(ApiPort)), value);
    }

    /// <summary>مسیر پایه سرور API</summary>
    public static string ApiBasePath
    {
        get => Preferences.Get(Key(nameof(ApiBasePath)), DefaultApiBasePath);
        set => Preferences.Set(Key(nameof(ApiBasePath)), value);
    }

    /// <summary>آدرس (IP یا localhost) سرور API برای دستگاه واقعی</summary>
    public static string DeviceApiHost
    {
        get => Preferences.Get(Key(nameof(DeviceApiHost)), DefaultDeviceApiHost);
        set => Preferences.Set(Key(nameof(DeviceApiHost)), value);
    }

    /// <summary>آدرس سرور API هنگام اجرای روی شبیه‌ساز</summary>
    public static string EmulatorApiHost
    {
        get => Preferences.Get(Key(nameof(EmulatorApiHost)), DefaultEmulatorApiHost);
        set => Preferences.Set(Key(nameof(EmulatorApiHost)), value);
    }

    /// <summary>شناسه پکیج سرویس POS اسان پرداخت</summary>
    public static string PosServicePackage
    {
        get => Preferences.Get(Key(nameof(PosServicePackage)), DefaultPosServicePackage);
        set => Preferences.Set(Key(nameof(PosServicePackage)), value);
    }

    /// <summary>
    /// نام کلاس سرویس POS (برای اتصال مستقیم). با «شناسایی خودکار» در تنظیمات
    /// مقدار واقعی روی همین دستگاه پیدا و ذخیره می‌شود.
    /// </summary>
    public static string PosServiceClass
    {
        get => Preferences.Get(Key(nameof(PosServiceClass)), DefaultPosServiceClass);
        set => Preferences.Set(Key(nameof(PosServiceClass)), value);
    }

    /// <summary>اکشن اتصال به سرویس IPosService</summary>
    public static string PosServiceAction
    {
        get
        {
            var value = Preferences.Get(Key(nameof(PosServiceAction)), DefaultPosServiceAction);

            // مهاجرت: در نسخه‌های قبلی به‌اشتباه نام اینترفیس AIDL به عنوان اکشن ذخیره شده بود
            if (string.Equals(value, LegacyPosServiceAction, StringComparison.OrdinalIgnoreCase))
            {
                value = DefaultPosServiceAction;
                Preferences.Set(Key(nameof(PosServiceAction)), value);
            }

            return value;
        }
        set => Preferences.Set(Key(nameof(PosServiceAction)), value);
    }

    /// <summary>کد تراکنش خرید</summary>
    public static int TransactionCodePurchase
    {
        get => Preferences.Get(Key(nameof(TransactionCodePurchase)), DefaultTransactionCodePurchase);
        set => Preferences.Set(Key(nameof(TransactionCodePurchase)), value);
    }

    /// <summary>کد تراکنش قبض خدماتی</summary>
    public static int TransactionCodeServiceBill
    {
        get => Preferences.Get(Key(nameof(TransactionCodeServiceBill)), DefaultTransactionCodeServiceBill);
        set => Preferences.Set(Key(nameof(TransactionCodeServiceBill)), value);
    }

    /// <summary>شناسه host دریافتی از اسان پرداخت</summary>
    public static int HostId
    {
        get => Preferences.Get(Key(nameof(HostId)), DefaultHostId);
        set => Preferences.Set(Key(nameof(HostId)), value);
    }

    /// <summary>
    /// کلید API (Host Api Key) دریافتی از اسان پرداخت.
    /// این مقدار در فیلد «hak» داخل HostRequest (JSON درخواست پرداخت) ارسال می‌شود.
    /// </summary>
    public static string HostApiKey
    {
        get => Preferences.Get(Key(nameof(HostApiKey)), DefaultHostApiKey);
        set => Preferences.Set(Key(nameof(HostApiKey)), value);
    }

    /// <summary>زبان SDK دستگاه</summary>
    public static string Lang
    {
        get => Preferences.Get(Key(nameof(Lang)), DefaultLang);
        set => Preferences.Set(Key(nameof(Lang)), value);
    }

    // ----- تنظیمات به‌روزرسانی خودکار (GitHub Releases) -----
    public const string DefaultUpdateRepoOwner = "JaberAsadiM";
    public const string DefaultUpdateRepoName = "BillPayment";
    public const bool DefaultUpdateCheckOnStartup = true;
    public const string DefaultUpdateGitHubToken = "";

    /// <summary>مالک مخزن GitHub برای بررسی به‌روزرسانی</summary>
    public static string UpdateRepoOwner
    {
        get => Preferences.Get(Key(nameof(UpdateRepoOwner)), DefaultUpdateRepoOwner);
        set => Preferences.Set(Key(nameof(UpdateRepoOwner)), value);
    }

    /// <summary>نام مخزن GitHub برای بررسی به‌روزرسانی</summary>
    public static string UpdateRepoName
    {
        get => Preferences.Get(Key(nameof(UpdateRepoName)), DefaultUpdateRepoName);
        set => Preferences.Set(Key(nameof(UpdateRepoName)), value);
    }

    /// <summary>بررسی خودکار نسخه جدید هنگام شروع برنامه؟</summary>
    public static bool UpdateCheckOnStartup
    {
        get => Preferences.Get(Key(nameof(UpdateCheckOnStartup)), DefaultUpdateCheckOnStartup);
        set => Preferences.Set(Key(nameof(UpdateCheckOnStartup)), value);
    }

    /// <summary>توکن GitHub (اختیاری — برای مخازن خصوصی یا جلوگیری از rate limit)</summary>
    public static string UpdateGitHubToken
    {
        get => Preferences.Get(Key(nameof(UpdateGitHubToken)), DefaultUpdateGitHubToken);
        set => Preferences.Set(Key(nameof(UpdateGitHubToken)), value);
    }

    /// <summary>بازگرداندن همه‌ی تنظیمات به مقادیر پیش‌فرض (حذف از حافظه‌ی گوشی)</summary>
    public static void ResetToDefaults()
    {
        ApiPort = DefaultApiPort;
        ApiBasePath = DefaultApiBasePath;
        DeviceApiHost = DefaultDeviceApiHost;
        EmulatorApiHost = DefaultEmulatorApiHost;
        PosServicePackage = DefaultPosServicePackage;
        PosServiceClass = DefaultPosServiceClass;
        PosServiceAction = DefaultPosServiceAction;
        TransactionCodePurchase = DefaultTransactionCodePurchase;
        TransactionCodeServiceBill = DefaultTransactionCodeServiceBill;
        HostId = DefaultHostId;
        HostApiKey = DefaultHostApiKey;
        Lang = DefaultLang;
        UpdateRepoOwner = DefaultUpdateRepoOwner;
        UpdateRepoName = DefaultUpdateRepoName;
        UpdateCheckOnStartup = DefaultUpdateCheckOnStartup;
        UpdateGitHubToken = DefaultUpdateGitHubToken;
    }
}