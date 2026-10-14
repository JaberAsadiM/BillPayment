using Amard.App.Models;
using Amard.App.Platforms.Android;
using Amard.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Networking;

namespace Amard.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IBillApiService _api;
    private readonly PosService _pos;

    [ObservableProperty]
    private BillSearchType searchType = BillSearchType.PostalCode;

    [ObservableProperty]
    private string searchValue = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    /// <summary>آیا دستگاه به اینترنت متصل است؟ (نمایش بنر وضعیت اتصال)</summary>
    [ObservableProperty]
    private bool isConnected;

    /// <summary>متن فارسی وضعیت اتصال اینترنت دستگاه</summary>
    [ObservableProperty]
    private string connectionStatusText = string.Empty;

    /// <summary>رنگ پس‌زمینه بنر وضعیت اتصال</summary>
    [ObservableProperty]
    private Color connectionBannerColor = Colors.Gray;

    /// <summary>رنگ متن بنر وضعیت اتصال</summary>
    [ObservableProperty]
    private Color connectionTextColor = Colors.Black;

    /// <summary>آیا پیام وضعیتی برای نمایش وجود دارد (کنترل نمایش کارت وضعیت)</summary>
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatusMessage));

    /// <summary>لیست قبض‌های یافت‌شده برای مالک انتخاب‌شده</summary>
    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<BillInfo> billsList = new();

    /// <summary>لیست مالکین یافت‌شده در جستجو</summary>
    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<Owners> ownersList = new();

    /// <summary>لیست پرونده‌های یافت‌شده با جستجوی کد ملی (فقط کد نوسازی و آدرس نمایش داده می‌شود)</summary>
    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<ParvandehViewModel> parvandehList = new();

    /// <summary>پرونده انتخاب‌شده از لیست پرونده‌ها (کلیک روی کد نوسازی)</summary>
    [ObservableProperty]
    private ParvandehViewModel? selectedParvandeh;

    /// <summary>مالک انتخاب‌شده از لیست</summary>
    [ObservableProperty]
    private Owners? selectedOwner;

    /// <summary>قبض انتخاب‌شده از لیست قبض‌ها که فقط همان پرداخت می‌شود</summary>
    [ObservableProperty]
    private BillInfo? selectedBill;

    /// <summary>قبضی که در حال حاضر تراکنش پرداخت آن در جریان است</summary>
    private BillInfo? _pendingPaymentBill;

    public bool HasOwners => OwnersList.Count > 0;
    public bool HasParvandehs => ParvandehList.Count > 0;
    public bool HasSelectedOwner => SelectedOwner is not null;
    public bool HasBills => BillsList.Count > 0;
    public bool HasSelectedBill => SelectedBill is not null;

    public string AmountText => SelectedBill is null ? "-" : $"{SelectedBill.AmountRials:N0} ریال";

    public MainViewModel()
    {
        // HttpClient استاندارد برنامه: اعتبارسنجی گواهی با لنگر داخلی (کار روی
        // دستگاه‌های قدیمی) + پروکسی سیستم. آدرس پایه لحظه‌ای از AppConstants
        // خوانده می‌شود تا تغییر تنظیمات بلافاصله اعمال شود.
        _api = new BillApiService(ApiHttpClientFactory.Create(TimeSpan.FromSeconds(30)));
        _pos = new PosService();

        // نتیجه پرداخت از Activity دستگاه POS
        MainActivity.PaymentResultReceived += OnPaymentResultReceived;

        // وضعیت اولیه اتصال + گوش دادن به تغییر شبکه (قطع/وصل شدن اینترنت دستگاه)
        UpdateConnectionState();
        Connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    /// <summary>تغییر شبکه دستگاه (روشن/خاموش شدن اینترنت) — به‌روزرسانی بنر وضعیت</summary>
    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(UpdateConnectionState);
    }

    /// <summary>خواندن وضعیت فعلی اینترنت و تنظیم رنگ/متن بنر</summary>
    private void UpdateConnectionState()
    {
        IsConnected = ConnectivityService.IsConnected;
        if (IsConnected)
        {
            ConnectionStatusText = "● متصل به اینترنت";
            ConnectionBannerColor = Color.FromArgb("#E8F5E9");
            ConnectionTextColor = Color.FromArgb("#1B5E20");
        }
        else
        {
            ConnectionStatusText = "○ بدون اتصال به اینترنت";
            ConnectionBannerColor = Color.FromArgb("#FFEBEE");
            ConnectionTextColor = Color.FromArgb("#B71C1C");
        }
    }

    /// <summary>دکمه «تست اتصال»: بررسی اینترنت دستگاه + رسیدن واقعی به سرور API</summary>
    [RelayCommand]
    private async Task CheckConnectionAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        StatusMessage = "در حال بررسی اتصال به اینترنت و سرور ...";
        try
        {
            var (ok, message, _) = await ConnectivityService.TestApiConnectionAsync();
            StatusMessage = (ok ? "✅ " : "❌ ") + message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ خطا در تست اتصال: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>دکمه جستجوی قبض: با کد ملی لیست پرونده‌ها و با کد پستی/کد نوسازی لیست مالکین از سرور دریافت می‌شود</summary>
    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync()
    {
        if (!ConnectivityService.IsConnected)
        {
            StatusMessage = "❌ دستگاه به اینترنت متصل نیست. اتصال Wi-Fi یا دیتای سیم‌کارت دستگاه POS را بررسی کنید، سپس دوباره تلاش کنید.";
            return;
        }

        StatusMessage = string.Empty;
        OwnersList.Clear();
        SelectedOwner = null;
        BillsList.Clear();
        SelectedBill = null;
        ParvandehList.Clear();
        SelectedParvandeh = null;
        IsBusy = true;

        try
        {
            if (SearchType == BillSearchType.NationalCode)
            {
                // جستجو با کد ملی: سرور لیست پرونده‌ها (ParvandehViewModel) برمی‌گرداند
                // که فقط کد نوسازی و آدرس آن به کاربر نمایش داده می‌شود
                var parvandehs = await _api.SearchParvandehByNationalCodeAsync(SearchValue?.Trim() ?? string.Empty);

                foreach (var parvandeh in parvandehs)
                    ParvandehList.Add(parvandeh);

                OnPropertyChanged(nameof(HasParvandehs));
                StatusMessage = $"{parvandehs.Count} پرونده یافت شد. برای مشاهده اطلاعات مالک، روی کد نوسازی مورد نظر ضربه بزنید.";
            }
            else
            {
                var request = new BillSearchRequest
                {
                    SearchType = SearchType,
                    SearchValue = SearchValue?.Trim() ?? string.Empty
                };

                var owners = await _api.SearchOwnersAsync(request);

                foreach (var owner in owners)
                    OwnersList.Add(owner);

                OnPropertyChanged(nameof(HasOwners));
                StatusMessage = $"{owners.Count} مالک یافت شد. مالک مورد نظر را انتخاب کنید.";
            }
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
    partial void OnIsBusyChanged(bool value)
    {
        SearchCommand.NotifyCanExecuteChanged();
        GetBillCommand.NotifyCanExecuteChanged();
        PayCommand.NotifyCanExecuteChanged();
    }

    partial void OnOwnersListChanged(System.Collections.ObjectModel.ObservableCollection<Owners> value) =>
        OnPropertyChanged(nameof(HasOwners));

    partial void OnParvandehListChanged(System.Collections.ObjectModel.ObservableCollection<ParvandehViewModel> value) =>
        OnPropertyChanged(nameof(HasParvandehs));

    partial void OnSelectedParvandehChanged(ParvandehViewModel? value)
    {
        // با کلیک روی هر پرونده (کد نوسازی)، اطلاعات مالک آن از سرور دریافت می‌شود
        if (value is not null)
            _ = LoadOwnersFromParvandehAsync(value);
    }

    /// <summary>
    /// کلیک روی کد نوسازی در لیست پرونده‌ها: مجدداً به سرور می‌رود و با همان
    /// کد نوسازی، اطلاعات مالک را مانند جستجوی کد نوسازی نمایش می‌دهد.
    /// </summary>
    private async Task LoadOwnersFromParvandehAsync(ParvandehViewModel parvandeh)
    {
        if (IsBusy)
        {
            SelectedParvandeh = null;
            return;
        }

        if (!ConnectivityService.IsConnected)
        {
            StatusMessage = "❌ دستگاه به اینترنت متصل نیست. اتصال دستگاه را بررسی کنید، سپس دوباره تلاش کنید.";
            SelectedParvandeh = null;
            return;
        }

        StatusMessage = string.Empty;
        IsBusy = true;

        try
        {
            var request = new BillSearchRequest
            {
                SearchType = BillSearchType.RenovationCode,
                SearchValue = parvandeh.codeN ?? string.Empty
            };

            var owners = await _api.SearchOwnersAsync(request);

            OwnersList.Clear();
            SelectedOwner = null;
            BillsList.Clear();
            SelectedBill = null;

            // لیست پرونده‌ها پاک می‌شود تا فقط اطلاعات مالک نمایش داده شود؛
            // انتخاب هم ریست می‌شود تا کلیک دوباره روی همان آیتم هم کار کند
            ParvandehList.Clear();
            SelectedParvandeh = null;

            foreach (var owner in owners)
                OwnersList.Add(owner);

            OnPropertyChanged(nameof(HasOwners));
            OnPropertyChanged(nameof(HasParvandehs));
            StatusMessage = $"{owners.Count} مالک یافت شد. مالک مورد نظر را انتخاب کنید.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا در دریافت اطلاعات مالک: {ex.Message}";
            SelectedParvandeh = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnBillsListChanged(System.Collections.ObjectModel.ObservableCollection<BillInfo> value) =>
        OnPropertyChanged(nameof(HasBills));

    partial void OnSelectedOwnerChanged(Owners? value)
    {
        OnPropertyChanged(nameof(HasSelectedOwner));

        // با تغییر مالک انتخاب‌شده، لیست قبض‌های قبلی پاک می‌شود تا دکمه «دریافت مبلغ قبض» دوباره ظاهر شود
        BillsList.Clear();
        SelectedBill = null;

        GetBillCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedBillChanged(BillInfo? value)
    {
        OnPropertyChanged(nameof(HasSelectedBill));
        OnPropertyChanged(nameof(AmountText));
        PayCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// دکمه «دریافت مبلغ قبض»: دریافت لیست قبض‌ها (مبلغ، شناسه قبض و شناسه پرداخت)
    /// برای مالک انتخاب‌شده از سرور و نمایش آن‌ها به‌صورت لیست
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanGetBill))]
    private async Task GetBillAsync()
    {
        if (!ConnectivityService.IsConnected)
        {
            StatusMessage = "❌ دستگاه به اینترنت متصل نیست. اتصال دستگاه را بررسی کنید، سپس دوباره تلاش کنید.";
            return;
        }

        StatusMessage = string.Empty;
        IsBusy = true;

        try
        {
            var bills = await _api.GetBillAsync(SelectedOwner!);

            BillsList.Clear();
            foreach (var bill in bills)
                BillsList.Add(bill);

            SelectedBill = null;

            OnPropertyChanged(nameof(HasBills));
            StatusMessage = $"{bills.Count} قبض یافت شد. قبض مورد نظر را انتخاب و پرداخت کنید.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا در دریافت مبلغ قبض: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanGetBill() => !IsBusy && SelectedOwner is not null;

    /// <summary>دکمه پرداخت: ارسال مبلغِ فقطِ قبض انتخاب‌شده به دستگاه POS</summary>
    [RelayCommand(CanExecute = nameof(CanPay))]
    private async Task PayAsync()
    {
        // قبض در جریان، قبل از شروع تراکنش ثابت می‌شود تا تغییر انتخاب، پرداخت را خراب نکند
        _pendingPaymentBill = SelectedBill!;

        StatusMessage = $"در انتظار پرداخت مبلغ {_pendingPaymentBill.AmountRials:N0} ریال روی دستگاه POS ...";
        IsBusy = true;

        try
        {
            await _pos.StartPaymentAsync(_pendingPaymentBill);
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا در پرداخت: {ex.Message}";
            IsBusy = false;
        }
    }

    private bool CanPay() => !IsBusy && SelectedBill is not null;

    /// <summary>نتیجه برگشتی از دستگاه POS</summary>
    private async void OnPaymentResultReceived(Android.Content.Intent? data, bool resultOk)
    {
        try
        {
            IsBusy = true;
            var posResult = PosService.ParsePaymentResult(data, resultOk);

            if (posResult.HostTranId == 0)
                posResult.HostTranId = _pos.LastHostTranId;

            // اگر نتیجه نامشخص بود (کد 9999)، طبق مستندات با getTransactionStatus استعلام می‌گیریم
            if (!posResult.Success && posResult.Unknown && posResult.HostTranId != 0)
            {
                StatusMessage = "وضعیت پرداخت نامشخص است؛ در حال استعلام وضعیت تراکنش از دستگاه ...";

                var inquiry = await _pos.InquireAsync(posResult.HostTranId);
                if (inquiry is not null)
                    posResult = inquiry;
            }

            if (!posResult.Success)
            {
                StatusMessage = posResult.Unknown
                    ? $"وضعیت پرداخت نامشخص است: {posResult.StatusMessage}"
                    : $"پرداخت ناموفق: {posResult.StatusMessage}";
                return;
            }

            StatusMessage = "پرداخت موفق! در حال ثبت تأیید در سرور ...";

            var paidBill = _pendingPaymentBill;
            if (paidBill is null)
            {
                StatusMessage = "قبضی برای ثبت تأیید پرداخت یافت نشد.";
                return;
            }

            // اعلام پرداخت به سرور
            var confirm = await _api.ConfirmPaymentAsync(paidBill, posResult);
            if (!confirm.Success)
            {
                StatusMessage = $"تأیید سرور ناموفق بود: {confirm.Message}";
                return;
            }

            StatusMessage = "پرداخت تأیید شد. در حال چاپ رسید ...";

            // چاپ رسید
            try
            {
                await _pos.PrintReceiptAsync(paidBill, posResult);
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
        Connectivity.ConnectivityChanged -= OnConnectivityChanged;
        _pos.Dispose();
    }
}
