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
    public const string DefaultPosServiceAction = "com.persianswitch.smartpos.aidl.IPosService";
    public const int DefaultTransactionCodePurchase = 1502;
    public const int DefaultTransactionCodeServiceBill = 1503;
    public const int DefaultHostId = 1;
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

    /// <summary>اکشن اتصال به سرویس IPosService</summary>
    public static string PosServiceAction
    {
        get => Preferences.Get(Key(nameof(PosServiceAction)), DefaultPosServiceAction);
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

    /// <summary>زبان SDK دستگاه</summary>
    public static string Lang
    {
        get => Preferences.Get(Key(nameof(Lang)), DefaultLang);
        set => Preferences.Set(Key(nameof(Lang)), value);
    }

    /// <summary>بازگرداندن همه‌ی تنظیمات به مقادیر پیش‌فرض (حذف از حافظه‌ی گوشی)</summary>
    public static void ResetToDefaults()
    {
        ApiPort = DefaultApiPort;
        ApiBasePath = DefaultApiBasePath;
        DeviceApiHost = DefaultDeviceApiHost;
        EmulatorApiHost = DefaultEmulatorApiHost;
        PosServicePackage = DefaultPosServicePackage;
        PosServiceAction = DefaultPosServiceAction;
        TransactionCodePurchase = DefaultTransactionCodePurchase;
        TransactionCodeServiceBill = DefaultTransactionCodeServiceBill;
        HostId = DefaultHostId;
        Lang = DefaultLang;
    }
}