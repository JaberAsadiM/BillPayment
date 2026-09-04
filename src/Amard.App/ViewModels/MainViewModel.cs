using System.Windows.Input;
using Amard.App.Models;
using Amard.App.Platforms.Android;
using Amard.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Amard.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IBillApiService _api;
    private readonly PosService _pos;

    [ObservableProperty]
    private BillSearchType searchType = BillSearchType.OwnerName;

    [ObservableProperty]
    private string searchValue = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private BillInfo? currentBill;

    public bool HasBill => CurrentBill is not null;
    public bool HasNoBill => CurrentBill is null;

    public string AmountText => CurrentBill is null ? "-" : $"{CurrentBill.AmountRials:N0} ریال";

    public MainViewModel()
    {
        _api = new BillApiService(new HttpClient
        {
            // آدرس پایه در BillApiService لحظه‌ای از AppConstants خوانده می‌شود
            // تا تغییر تنظیمات در صفحه‌ی «تنظیمات» بلافاصله اعمال شود.
            Timeout = TimeSpan.FromSeconds(30)
        });
        _pos = new PosService();

        // نتیجه پرداخت از Activity دستگاه POS
        MainActivity.PaymentResultReceived += OnPaymentResultReceived;
    }

    /// <summary>دکمه جستجوی قبض: ارسال اطلاعات به سرور و دریافت مبلغ</summary>
    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync()
    {
        StatusMessage = string.Empty;
        CurrentBill = null;
        IsBusy = true;

        try
        {
            var request = new BillSearchRequest
            {
                SearchType = SearchType,
                SearchValue = SearchValue?.Trim() ?? string.Empty
            };

            CurrentBill = await _api.SearchBillAsync(request);
            OnPropertyChanged(nameof(AmountText));
            StatusMessage = "قبض یافت شد. در صورت تمایل پرداخت کنید.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا در جستجو: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSearch() =>
        !IsBusy && !string.IsNullOrWhiteSpace(SearchValue);

    partial void OnSearchValueChanged(string value) => SearchCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) => SearchCommand.NotifyCanExecuteChanged();

    partial void OnCurrentBillChanged(BillInfo? value)
    {
        OnPropertyChanged(nameof(HasBill));
        OnPropertyChanged(nameof(HasNoBill));
        OnPropertyChanged(nameof(AmountText));
    }

    /// <summary>دکمه پرداخت: ارسال مبلغ به دستگاه POS</summary>
    [RelayCommand(CanExecute = nameof(CanPay))]
    private async Task PayAsync()
    {
        StatusMessage = "در انتظار پرداخت روی دستگاه POS ...";
        IsBusy = true;

        try
        {
            await _pos.StartPaymentAsync(CurrentBill!);
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا در پرداخت: {ex.Message}";
            IsBusy = false;
        }
    }

    private bool CanPay() => !IsBusy && HasBill;

    /// <summary>نتیجه برگشتی از دستگاه POS</summary>
    private async void OnPaymentResultReceived(Android.Content.Intent? data)
    {
        try
        {
            IsBusy = true;
            var posResult = PosService.ParsePaymentResult(data);

            if (!posResult.Success)
            {
                StatusMessage = posResult.Unknown
                    ? "وضعیت پرداخت نامشخص است. لطفاً استعلام بگیرید."
                    : $"پرداخت ناموفق: {posResult.StatusMessage}";
                return;
            }

            StatusMessage = "پرداخت موفق! در حال ثبت تأیید در سرور ...";

            // اعلام پرداخت به سرور
            var confirm = await _api.ConfirmPaymentAsync(CurrentBill!, posResult);
            if (!confirm.Success)
            {
                StatusMessage = $"تأیید سرور ناموفق بود: {confirm.Message}";
                return;
            }

            StatusMessage = "پرداخت تأیید شد. در حال چاپ رسید ...";

            // چاپ رسید
            try
            {
                await _pos.PrintReceiptAsync(CurrentBill!, posResult);
                StatusMessage = "✅ پرداخت با موفقیت انجام و رسید چاپ شد.";
            }
            catch (Exception printEx)
            {
                StatusMessage = $"پرداخت تأیید شد اما چاپ ناموفق بود: {printEx.Message}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Dispose()
    {
        MainActivity.PaymentResultReceived -= OnPaymentResultReceived;
        _pos.Dispose();
    }
}
