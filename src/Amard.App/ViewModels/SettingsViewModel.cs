using System.Text;
using Amard.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Amard.App.ViewModels;

/// <summary>
/// ViewModel صفحه‌ی تنظیمات: ویرایش همه‌ی تنظیمات AppConstants و ذخیره روی حافظه‌ی گوشی.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    /// <summary>
    /// پورت سرور به‌صورت متن (بایند به Entry).
    /// عمداً string است نه int: اگر int بود، پاک کردن فیلد توسط کاربر به int تبدیل
    /// نمی‌شد و مقدار قبلی (مثلاً 4) برای همیشه گیر می‌کرد. خالی = پیش‌فرض (8008)
    /// که برای https اصلاً در آدرس نوشته نمی‌شود.
    /// </summary>
    [ObservableProperty]
    private string apiPort = string.Empty;

    [ObservableProperty]
    private string apiBasePath = string.Empty;

    [ObservableProperty]
    private string deviceApiHost = string.Empty;

    [ObservableProperty]
    private string emulatorApiHost = string.Empty;

    [ObservableProperty]
    private string posServicePackage = string.Empty;

    /// <summary>نام کلاس سرویس POS (اتصال مستقیم) — با «شناسایی خودکار» پر می‌شود</summary>
    [ObservableProperty]
    private string posServiceClass = string.Empty;

    [ObservableProperty]
    private string posServiceAction = string.Empty;

    /// <summary>کد تراکنش خرید (متن برای بایند به Entry؛ خالی مجاز نیست)</summary>
    [ObservableProperty]
    private string transactionCodePurchase = string.Empty;

    /// <summary>کد تراکنش قبض خدماتی (متن برای بایند به Entry؛ خالی مجاز نیست)</summary>
    [ObservableProperty]
    private string transactionCodeServiceBill = string.Empty;

    /// <summary>شناسه host (متن برای بایند به Entry؛ خالی مجاز نیست)</summary>
    [ObservableProperty]
    private string hostId = string.Empty;

    [ObservableProperty]
    private string hostApiKey = string.Empty;

    [ObservableProperty]
    private string lang = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    // ----- وضعیت اتصال -----
    /// <summary>آیا دستگاه به اینترنت متصل است؟</summary>
    [ObservableProperty]
    private bool isConnected;

    /// <summary>متن فارسی وضعیت اتصال اینترنت دستگاه</summary>
    [ObservableProperty]
    private string connectionStatusText = string.Empty;

    /// <summary>نتیجه آخرین تست اتصال به سرور (پس از زدن دکمه تست پر می‌شود)</summary>
    [ObservableProperty]
    private string apiTestResult = string.Empty;

    /// <summary>سرویس‌های پرداخت یافت‌شده با «شناسایی خودکار» روی همین دستگاه</summary>
    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<PosServiceCandidate> discoveredServices = new();

    /// <summary>سرویس انتخاب‌شده از لیست کشف‌شده برای اعمال در تنظیمات</summary>
    [ObservableProperty]
    private PosServiceCandidate? selectedCandidate;

    /// <summary>متن وضعیت آخرین شناسایی خودکار</summary>
    [ObservableProperty]
    private string discoveryStatus = string.Empty;

    /// <summary>آیا نتیجه‌ای از شناسایی وجود دارد (کنترل نمایش لیست)</summary>
    public bool HasDiscoveredServices => DiscoveredServices.Count > 0;

    /// <summary>آیا پیام وضعیت شناسایی وجود دارد (کنترل نمایش لیبل وضعیت)</summary>
    public bool HasDiscoveryStatus => !string.IsNullOrWhiteSpace(DiscoveryStatus);

    partial void OnDiscoveryStatusChanged(string value) => OnPropertyChanged(nameof(HasDiscoveryStatus));

    /// <summary>آیا نتیجه‌ای از تست اتصال وجود دارد (کنترل نمایش لیبل نتیجه)</summary>
    public bool HasApiTestResult => !string.IsNullOrWhiteSpace(ApiTestResult);

    partial void OnApiTestResultChanged(string value) => OnPropertyChanged(nameof(HasApiTestResult));

    // ----- به‌روزرسانی خودکار -----
    /// <summary>آخرین نتایج بررسی GitHub Releases (پس از اولین بررسی پر می‌شود)</summary>
    private UpdateCheckResult? _lastCheck;

    [ObservableProperty]
    private string latestVersion = "—";

    [ObservableProperty]
    private string updateStatus = "هنوز بررسی نشده";

    [ObservableProperty]
    private Color updateStatusColor = Colors.Gray;

    [ObservableProperty]
    private string releaseNotes = string.Empty;

    [ObservableProperty]
    private bool hasUpdate;

    [ObservableProperty]
    private double updateProgress;

    [ObservableProperty]
    private bool isBusy;

    /// <summary>معکوس IsBusy برای فعال/غیرفعال کردن دکمه‌ها</summary>
    public bool IsNotBusy => !IsBusy;

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    private bool HasReleaseNotes => !string.IsNullOrWhiteSpace(ReleaseNotes);

    partial void OnReleaseNotesChanged(string value) => OnPropertyChanged(nameof(HasReleaseNotes));

    /// <summary>آدرس کامل فعلی سرور API (بعد از ذخیره به‌روز می‌شود)</summary>
    public string CurrentApiUrl => AppConstants.TryGetApiBaseUrl(out var url, out var error)
        ? url
        : $"⚠ {error}";

    /// <summary>نسخه نصب‌شده روی دستگاه</summary>
    public string CurrentAppVersion => UpdateService.CurrentVersion;

    private readonly UpdateService _updateService = new();

    /// <summary>زمان آخرین بررسی خودکار (برای جلوگیری از بررسی‌های مکرر)</summary>
    private static DateTime _lastAutoCheck = DateTime.MinValue;

    public SettingsViewModel()
    {
        LoadFromSettings();
        RefreshConnectionState();
    }

    /// <summary>بررسی خودکار هنگام باز شدن صفحه تنظیمات (حداکثر هر ۱۰ دقیقه یک‌بار)</summary>
    public void CheckOnAppearing()
    {
        RefreshConnectionState();

        if (!AppSettingsService.UpdateCheckOnStartup)
            return;

        if ((DateTime.UtcNow - _lastAutoCheck).TotalMinutes < 10)
            return;

        _lastAutoCheck = DateTime.UtcNow;
        CheckForUpdateCommand.Execute(null);
    }

    /// <summary>بارگذاری مقادیر ذخیره‌شده (یا پیش‌فرض) از حافظه‌ی گوشی</summary>
    private void LoadFromSettings()
    {
        // اگر پورت ذخیره‌شده همان پیش‌فرض (8008) است، فیلد خالی نمایش داده می‌شود
        // تا کاربر https بفهمد نیازی به وارد کردن پورت نیست
        ApiPort = AppSettingsService.ApiPort == AppSettingsService.DefaultApiPort
            ? string.Empty
            : AppSettingsService.ApiPort.ToString();
        ApiBasePath = AppSettingsService.ApiBasePath;
        DeviceApiHost = AppSettingsService.DeviceApiHost;
        EmulatorApiHost = AppSettingsService.EmulatorApiHost;
        PosServicePackage = AppSettingsService.PosServicePackage;
        PosServiceClass = AppSettingsService.PosServiceClass;
        PosServiceAction = AppSettingsService.PosServiceAction;
        TransactionCodePurchase = AppSettingsService.TransactionCodePurchase.ToString();
        TransactionCodeServiceBill = AppSettingsService.TransactionCodeServiceBill.ToString();
        HostId = AppSettingsService.HostId.ToString();
        HostApiKey = AppSettingsService.HostApiKey;
        Lang = AppSettingsService.Lang;
    }

    /// <summary>
    /// تبدیل متن فیلد عددی به int؛ خالی فقط برای پورت مجاز است (برمی‌گردد به پیش‌فرض).
    /// ناموفق بودن تبدیل = null.
    /// </summary>
    private static int? ParseNumber(string text, bool emptyMeansDefault)
    {
        text = (text ?? string.Empty).Trim();
        if (text.Length == 0)
            return emptyMeansDefault ? AppSettingsService.DefaultApiPort : null;

        // ارقام فارسی/عربی را به انگلیسی تبدیل کن (کیبورد فارسی!)
        var normalized = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch is >= '۰' and <= '۹')
                normalized.Append((char)('0' + (ch - '۰')));
            else if (ch is >= '٠' and <= '٩')
                normalized.Append((char)('0' + (ch - '٠')));
            else
                normalized.Append(ch);
        }

        return int.TryParse(normalized.ToString(), out var value) ? value : null;
    }

    /// <summary>دکمه ذخیره: اعتبارسنجی و نوشتن تنظیمات روی حافظه‌ی دائمی گوشی</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        var errors = Validate();
        if (errors.Length > 0)
        {
            StatusMessage = errors;
            return;
        }

        try
        {
            AppSettingsService.ApiPort = ParseNumber(ApiPort, emptyMeansDefault: true)!.Value;
            AppSettingsService.ApiBasePath = ApiBasePath.Trim();
            AppSettingsService.DeviceApiHost = DeviceApiHost.Trim();
            AppSettingsService.EmulatorApiHost = EmulatorApiHost.Trim();
            AppSettingsService.PosServicePackage = PosServicePackage.Trim();
            AppSettingsService.PosServiceClass = PosServiceClass.Trim();
            AppSettingsService.PosServiceAction = PosServiceAction.Trim();
            AppSettingsService.TransactionCodePurchase = ParseNumber(TransactionCodePurchase, emptyMeansDefault: false)!.Value;
            AppSettingsService.TransactionCodeServiceBill = ParseNumber(TransactionCodeServiceBill, emptyMeansDefault: false)!.Value;
            AppSettingsService.HostId = ParseNumber(HostId, emptyMeansDefault: false)!.Value;
            AppSettingsService.HostApiKey = HostApiKey.Trim();
            AppSettingsService.Lang = Lang.Trim();

            // فیلد پورت را با مقدار واقعیِ ذخیره‌شده همگام کن (خالی = پیش‌فرض)
            LoadFromSettings();

            StatusMessage = "✅ تنظیمات با موفقیت ذخیره شد.";
            OnPropertyChanged(nameof(CurrentApiUrl));

            await ShowAlertAsync("تنظیمات", StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا در ذخیره تنظیمات: {ex.Message}";
        }
    }

    /// <summary>دکمه بازگردانی پیش‌فرض‌ها</summary>
    [RelayCommand]
    private async Task ResetToDefaultsAsync()
    {
        var confirmed = await ShowConfirmAsync(
            "بازگردانی پیش‌فرض‌ها",
            "همه‌ی تنظیمات به مقادیر پیش‌فرض بازگردانده و ذخیره شوند؟",
            "بله", "انصراف");

        if (!confirmed)
            return;

        AppSettingsService.ResetToDefaults();
        LoadFromSettings();
        StatusMessage = "تنظیمات به حالت پیش‌فرض بازگشت و ذخیره شد.";
        OnPropertyChanged(nameof(CurrentApiUrl));
    }

    /// <summary>اعتبارسنجی مقادیر ورودی</summary>
    private string Validate()
    {
        var sb = new StringBuilder();

        var port = ParseNumber(ApiPort, emptyMeansDefault: true);
        if (port is not >= 1 and <= 65535)
            sb.AppendLine("• پورت سرور API باید عددی بین 1 تا 65535 باشد (برای سرور https خالی بگذارید).");

        if (string.IsNullOrWhiteSpace(DeviceApiHost))
            sb.AppendLine("• آدرس سرور برای دستگاه واقعی نمی‌تواند خالی باشد.");

        if (string.IsNullOrWhiteSpace(EmulatorApiHost))
            sb.AppendLine("• آدرس سرور برای شبیه‌ساز نمی‌تواند خالی باشد.");

        if (string.IsNullOrWhiteSpace(ApiBasePath))
            sb.AppendLine("• مسیر پایه سرور API نمی‌تواند خالی باشد.");

        if (string.IsNullOrWhiteSpace(PosServicePackage))
            sb.AppendLine("• شناسه پکیج سرویس POS نمی‌تواند خالی باشد.");
        else if (!PosDiscoveryService.LooksLikePackageName(PosServicePackage.Trim()))
            sb.AppendLine("• شناسه پکیج معتبر نیست (مثل «posservis» کافی نیست؛ باید چندبخشی باشد مثل com.persianswitch.smartpos). اگر نام دقیق را نمی‌دانید از «شناسایی خودکار سرویس پرداخت» استفاده کنید.");

        if (string.IsNullOrWhiteSpace(PosServiceClass))
            sb.AppendLine("• نام کلاس سرویس POS نمی‌تواند خالی باشد (با «شناسایی خودکار» پر می‌شود).");

        if (string.IsNullOrWhiteSpace(PosServiceAction))
            sb.AppendLine("• اکشن سرویس POS نمی‌تواند خالی باشد.");

        if (ParseNumber(TransactionCodePurchase, emptyMeansDefault: false) is not > 0)
            sb.AppendLine("• کد تراکنش خرید باید عددی بزرگ‌تر از صفر باشد.");

        if (ParseNumber(TransactionCodeServiceBill, emptyMeansDefault: false) is not > 0)
            sb.AppendLine("• کد تراکنش قبض خدماتی باید عددی بزرگ‌تر از صفر باشد.");

        if (ParseNumber(HostId, emptyMeansDefault: false) is not > 0)
            sb.AppendLine("• شناسه host باید عددی بزرگ‌تر از صفر باشد.");

        if (string.IsNullOrWhiteSpace(HostApiKey))
            sb.AppendLine("• کلید API میزبان (host api key) نمی‌تواند خالی باشد.");

        if (string.IsNullOrWhiteSpace(Lang))
            sb.AppendLine("• زبان SDK نمی‌تواند خالی باشد.");

        return sb.ToString().TrimEnd();
    }

    private static async Task ShowAlertAsync(string title, string message)
    {
        var page = Microsoft.Maui.Controls.Application.Current?.MainPage?.Navigation?.NavigationStack?.LastOrDefault()
                   ?? Microsoft.Maui.Controls.Application.Current?.MainPage;
        if (page is not null)
            await page.DisplayAlert(title, message, "باشه");
    }

    private static async Task<bool> ShowConfirmAsync(string title, string message, string accept, string cancel)
    {
        var page = Microsoft.Maui.Controls.Application.Current?.MainPage?.Navigation?.NavigationStack?.LastOrDefault()
                   ?? Microsoft.Maui.Controls.Application.Current?.MainPage;
        if (page is null)
            return false;

        return await page.DisplayAlert(title, message, accept, cancel);
    }

    /// <summary>خواندن وضعیت فعلی اینترنت دستگاه برای نمایش در صفحه تنظیمات</summary>
    private void RefreshConnectionState()
    {
        IsConnected = ConnectivityService.IsConnected;
        ConnectionStatusText = ConnectivityService.GetStatusText();
    }

    /// <summary>دکمه تست اتصال: بررسی اینترنت دستگاه + رسیدن واقعی به سرور API</summary>
    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        RefreshConnectionState();
        ApiTestResult = "در حال تست اتصال به سرور ...";

        try
        {
            var (ok, message, _) = await ConnectivityService.TestApiConnectionAsync();
            ApiTestResult = (ok ? "✅ " : "❌ ") + message;
        }
        catch (Exception ex)
        {
            ApiTestResult = $"❌ خطا در تست اتصال: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ================= شناسایی خودکار سرویس پرداخت =================

    /// <summary>
    /// دکمه «شناسایی خودکار»: اسکن سرویس‌های پرداخت نصب‌شده روی همین دستگاه
    /// و نمایش آن‌ها برای انتخاب. چون اپ تست آسان‌پرداخت روی دستگاه کار می‌کند،
    /// سرویس حتماً هست و اینجا پیدا می‌شود.
    /// </summary>
    [RelayCommand]
    private async Task DiscoverPosServiceAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        DiscoveryStatus = "در حال اسکن سرویس‌های نصب‌شده روی دستگاه ...";

        try
        {
            var found = await Task.Run(PosDiscoveryService.Discover);

            DiscoveredServices.Clear();
            foreach (var c in found)
                DiscoveredServices.Add(c);
            OnPropertyChanged(nameof(HasDiscoveredServices));
            SelectedCandidate = null;

            DiscoveryStatus = found.Count == 0
                ? "❌ هیچ سرویس پرداختی روی دستگاه یافت نشد. مطمئن شوید برنامه پرداخت آسان‌پرداخت نصب و فعال است."
                : $"✅ {found.Count} سرویس یافت شد. یکی را انتخاب و «اعمال» کنید، سپس ذخیره را بزنید.";
        }
        catch (Exception ex)
        {
            DiscoveryStatus = $"❌ خطا در شناسایی: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>اعمال سرویس انتخاب‌شده در فیلدهای تنظیمات (پکیج + کلاس + اکشن)</summary>
    [RelayCommand]
    private void ApplyCandidate()
    {
        if (SelectedCandidate is null)
        {
            DiscoveryStatus = "اول یک سرویس را از لیست انتخاب کنید.";
            return;
        }

        PosServicePackage = SelectedCandidate.PackageName;
        PosServiceClass = SelectedCandidate.ClassName;
        if (!string.IsNullOrWhiteSpace(SelectedCandidate.Action))
            PosServiceAction = SelectedCandidate.Action;

        DiscoveryStatus = $"✅ «{SelectedCandidate.PackageName}» در تنظیمات قرار گرفت. حالا «ذخیره تنظیمات» را بزنید و پرداخت را امتحان کنید.";
    }

    // ================= به‌روزرسانی خودکار =================

    /// <summary>بررسی نسخه جدید از GitHub Releases</summary>
    [RelayCommand]
    private async Task CheckForUpdateAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        UpdateStatus = "در حال بررسی نسخه جدید...";
        UpdateStatusColor = Colors.Gray;

        try
        {
            var result = await _updateService.CheckForUpdateAsync();
            _lastCheck = result;
            LatestVersion = result.LatestVersion;
            ReleaseNotes = result.ReleaseNotes ?? string.Empty;

            if (result.UpdateAvailable)
            {
                HasUpdate = true;
                UpdateStatus = $"نسخه جدید {result.LatestVersion} موجود است!";
                UpdateStatusColor = Colors.OrangeRed;
            }
            else
            {
                HasUpdate = false;
                UpdateStatus = "✅ برنامه به‌روز است";
                UpdateStatusColor = Colors.Green;
            }
        }
        catch (Exception ex)
        {
            UpdateStatus = ex.Message;
            UpdateStatusColor = Colors.Red;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>دانلود APK نسخه جدید و شروع نصب آن</summary>
    [RelayCommand]
    private async Task DownloadAndInstallAsync()
    {
        if (IsBusy || _lastCheck is not { } update || !update.UpdateAvailable)
            return;

        // بررسی مجوز نصب از منابع ناشناس (اندروید 8+)
        if (!UpdateService.CanInstallPackages())
        {
            var granted = await ShowConfirmAsync(
                "مجوز نصب",
                "برای نصب نسخه جدید، باید به برنامه اجازه «نصب برنامه‌های ناشناس» بدهید. صفحه تنظیمات باز شود؟",
                "باز کردن تنظیمات", "انصراف");

            if (granted)
                UpdateService.OpenInstallPermissionSettings();

            return;
        }

        if (!await ShowConfirmAsync(
                "به‌روزرسانی برنامه",
                $"نسخه {update.LatestVersion} دانلود و نصب شود؟ (حجم: {update.SizeBytes / 1024.0 / 1024.0:F1} مگابایت)",
                "دانلود و نصب", "انصراف"))
            return;

        IsBusy = true;
        UpdateProgress = 0;
        UpdateStatus = "در حال دانلود نسخه جدید... ۰٪";
        UpdateStatusColor = Colors.Gray;

        try
        {
            var progress = new Progress<double>(p =>
            {
                UpdateProgress = p;
                UpdateStatus = $"در حال دانلود نسخه جدید... {(int)(p * 100)}٪";
            });

            var apkPath = await _updateService.DownloadApkAsync(update, progress);

            UpdateStatus = "آماده نصب — لطفاً تأیید کنید...";
            UpdateService.InstallApk(apkPath);
        }
        catch (Exception ex)
        {
            UpdateStatus = $"خطا در دانلود: {ex.Message}";
            UpdateStatusColor = Colors.Red;
        }
        finally
        {
            IsBusy = false;
        }
    }
}