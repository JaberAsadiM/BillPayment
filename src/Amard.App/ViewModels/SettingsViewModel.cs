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
    [ObservableProperty]
    private int apiPort;

    [ObservableProperty]
    private string apiBasePath = string.Empty;

    [ObservableProperty]
    private string deviceApiHost = string.Empty;

    [ObservableProperty]
    private string emulatorApiHost = string.Empty;

    [ObservableProperty]
    private string posServicePackage = string.Empty;

    [ObservableProperty]
    private string posServiceAction = string.Empty;

    [ObservableProperty]
    private int transactionCodePurchase;

    [ObservableProperty]
    private int transactionCodeServiceBill;

    [ObservableProperty]
    private int hostId;

    [ObservableProperty]
    private string lang = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    /// <summary>آدرس کامل فعلی سرور API (بعد از ذخیره به‌روز می‌شود)</summary>
    public string CurrentApiUrl => AppConstants.ApiBaseUrl;

    public SettingsViewModel()
    {
        LoadFromSettings();
    }

    /// <summary>بارگذاری مقادیر ذخیره‌شده (یا پیش‌فرض) از حافظه‌ی گوشی</summary>
    private void LoadFromSettings()
    {
        ApiPort = AppSettingsService.ApiPort;
        ApiBasePath = AppSettingsService.ApiBasePath;
        DeviceApiHost = AppSettingsService.DeviceApiHost;
        EmulatorApiHost = AppSettingsService.EmulatorApiHost;
        PosServicePackage = AppSettingsService.PosServicePackage;
        PosServiceAction = AppSettingsService.PosServiceAction;
        TransactionCodePurchase = AppSettingsService.TransactionCodePurchase;
        TransactionCodeServiceBill = AppSettingsService.TransactionCodeServiceBill;
        HostId = AppSettingsService.HostId;
        Lang = AppSettingsService.Lang;
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
            AppSettingsService.ApiPort = ApiPort;
            AppSettingsService.ApiBasePath = ApiBasePath.Trim();
            AppSettingsService.DeviceApiHost = DeviceApiHost.Trim();
            AppSettingsService.EmulatorApiHost = EmulatorApiHost.Trim();
            AppSettingsService.PosServicePackage = PosServicePackage.Trim();
            AppSettingsService.PosServiceAction = PosServiceAction.Trim();
            AppSettingsService.TransactionCodePurchase = TransactionCodePurchase;
            AppSettingsService.TransactionCodeServiceBill = TransactionCodeServiceBill;
            AppSettingsService.HostId = HostId;
            AppSettingsService.Lang = Lang.Trim();

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

        if (ApiPort is < 1 or > 65535)
            sb.AppendLine("• پورت سرور API باید عددی بین 1 تا 65535 باشد.");

        if (string.IsNullOrWhiteSpace(DeviceApiHost))
            sb.AppendLine("• آدرس سرور برای دستگاه واقعی نمی‌تواند خالی باشد.");

        if (string.IsNullOrWhiteSpace(EmulatorApiHost))
            sb.AppendLine("• آدرس سرور برای شبیه‌ساز نمی‌تواند خالی باشد.");

        if (string.IsNullOrWhiteSpace(ApiBasePath))
            sb.AppendLine("• مسیر پایه سرور API نمی‌تواند خالی باشد.");

        if (string.IsNullOrWhiteSpace(PosServicePackage))
            sb.AppendLine("• شناسه پکیج سرویس POS نمی‌تواند خالی باشد.");

        if (string.IsNullOrWhiteSpace(PosServiceAction))
            sb.AppendLine("• اکشن سرویس POS نمی‌تواند خالی باشد.");

        if (TransactionCodePurchase <= 0)
            sb.AppendLine("• کد تراکنش خرید باید عددی بزرگ‌تر از صفر باشد.");

        if (TransactionCodeServiceBill <= 0)
            sb.AppendLine("• کد تراکنش قبض خدماتی باید عددی بزرگ‌تر از صفر باشد.");

        if (HostId <= 0)
            sb.AppendLine("• شناسه host باید عددی بزرگ‌تر از صفر باشد.");

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
}