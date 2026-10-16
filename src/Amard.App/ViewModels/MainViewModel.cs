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

    /// <summary>صف فیش‌های پرداخت‌شده که نتیجه آن‌ها هنوز به سرور اعلام نشده (ذخیره در حافظه دستگاه)</summary>
    private readonly PendingConfirmationStore _pendingStore;

    public bool HasOwners => OwnersList.Count > 0;
    public bool HasParvandehs => ParvandehList.Count > 0;
    public bool HasSelectedOwner => SelectedOwner is not null;
    public bool HasBills => BillsList.Count > 0;
    public bool HasSelectedBill => SelectedBill is not null;

    public string AmountText => SelectedBill is null ? "-" : $"{SelectedBill.AmountRials:N0} ریال";

    /// <summary>لیست فیش‌های پرداخت‌شده که نتیجه آن‌ها هنوز به سرور اعلام نشده (ارسال مجدد)</summary>
    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<PendingBillConfirmation> pendingConfirmationsList = new();

    /// <summary>متن تعداد فیش‌های در انتظار ارسال (نمایش زیر عنوان کارت ارسال مجدد)</summary>
    [ObservableProperty]
    private string pendingConfirmationsCountText = string.Empty;

    /// <summary>آیا فیش پرداخت‌شده‌ی اعلام‌نشده‌ای برای ارسال مجدد وجود دارد؟ (نمایش کارت ارسال مجدد)</summary>
    public bool HasPendingConfirmations => PendingConfirmationsList.Count > 0;

    partial void OnPendingConfirmationsListChanged(System.Collections.ObjectModel.ObservableCollection<PendingBillConfirmation> value) =>
        OnPropertyChanged(nameof(HasPendingConfirmations));

    public MainViewModel()
    {
        // HttpClient استاندارد برنامه: اعتبارسنجی گواهی با لنگر داخلی (کار روی
        // دستگاه‌های قدیمی) + پروکسی سیستم. آدرس پایه لحظه‌ای از AppConstants
        // خوانده می‌شود تا تغییر تنظیمات بلافاصله اعمال شود.
        _api = new BillApiService(ApiHttpClientFactory.Create(TimeSpan.FromSeconds(30)));
        _pos = new PosService();
        _pendingStore = new PendingConfirmationStore();
        _pendingStore.Changed += RefreshPendingConfirmations;

        // نتیجه پرداخت از Activity دستگاه POS
        MainActivity.PaymentResultReceived += OnPaymentResultReceived;

        // وضعیت اولیه اتصال + گوش دادن به تغییر شبکه (قطع/وصل شدن اینترنت دستگاه)
        UpdateConnectionState();
        Connectivity.ConnectivityChanged += OnConnectivityChanged;

        // نمایش فیش‌های در انتظار ارسال به سرور (ممکن است از اجرای قبلی برنامه باقی مانده باشند)
        RefreshPendingConfirmations();
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
        ResendAllConfirmationsCommand.NotifyCanExecuteChanged();
        SendPendingConfirmationCommand.NotifyCanExecuteChanged();
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

    /// <summary>
    /// دکمه «📤 ارسال مجدد همه به سرور»: ارسال دونه‌دونه‌ی نتیجه‌ی همه‌ی پرداخت‌های موفقی که
    /// هنوز در سرور ثبت نشده‌اند. هر فیش که با موفقیت ارسال شد از حافظه‌ی دستگاه حذف می‌شود.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanResendConfirmations))]
    private async Task ResendAllConfirmationsAsync()
    {
        var items = _pendingStore.GetAll();
        if (items.Count == 0)
            return;

        IsBusy = true;
        var sent = 0;
        var failed = 0;

        try
        {
            foreach (var item in items)
            {
                StatusMessage = $"در حال ارسال مجدد نتیجه پرداخت به سرور ({sent + failed + 1} از {items.Count}) ...";

                try
                {
                    var result = await _api.ConfirmAmardFishAsync(item);
                    if (result.Success)
                    {
                        _pendingStore.Remove(item);
                        sent++;
                    }
                    else
                    {
                        failed++;
                    }
                }
                catch (Exception)
                {
                    failed++;
                }
            }

            StatusMessage = failed == 0
                ? $"✅ نتیجه {sent} فیش پرداخت‌شده با موفقیت به سرور اعلام شد."
                : $"✅ {sent} فیش به سرور اعلام شد؛ ❌ برای {failed} فیش ارتباط با سرور برقرار نشد. پس از رفع مشکل دوباره تلاش کنید.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanResendConfirmations() => !IsBusy && _pendingStore.Count > 0;

    /// <summary>دکمه «📤 ارسال این فیش» داخل هر آیتم: ارسال دونه‌دونه‌ی یک فیش مشخص از لیست انتظار</summary>
    [RelayCommand(CanExecute = nameof(CanSendPendingConfirmation))]
    private async Task SendPendingConfirmationAsync(PendingBillConfirmation? item)
    {
        if (item is null)
            return;

        IsBusy = true;
        StatusMessage = $"در حال ارسال نتیجه پرداخت قبض «{item.BillId}» به سرور ...";

        try
        {
            var result = await _api.ConfirmAmardFishAsync(item);
            if (result.Success)
            {
                _pendingStore.Remove(item);
                StatusMessage = "✅ نتیجه پرداخت با موفقیت به سرور اعلام شد.";
            }
            else
            {
                StatusMessage = $"❌ ارسال به سرور ناموفق بود: {result.Message}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ ارسال به سرور ناموفق بود: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSendPendingConfirmation(PendingBillConfirmation? item) => !IsBusy && item is not null;

    /// <summary>به‌روزرسانی لیست و شمارنده‌ی فیش‌های در انتظار ارسال به سرور (بعد از هر تغییر در صف)</summary>
    private void RefreshPendingConfirmations()
    {
        var items = _pendingStore.GetAll();
        PendingConfirmationsList = new System.Collections.ObjectModel.ObservableCollection<PendingBillConfirmation>(items);
        PendingConfirmationsCountText = items.Count == 0
            ? string.Empty
            : $"{items.Count} فیش پرداخت‌شده در انتظار ارسال به سرور";

        ResendAllConfirmationsCommand.NotifyCanExecuteChanged();
    }

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

            // اعلام پرداخت به سرور (ConfirmAmardFish: شناسه قبض، شناسه پرداخت، تاریخ شمسی و شماره پیگیری)
            PaymentConfirmResult? confirm = null;
            var confirmError = string.Empty;
            try
            {
                confirm = await _api.ConfirmAmardFishAsync(paidBill, posResult);
            }
            catch (Exception ex)
            {
                confirmError = ex.Message;
            }

            var confirmed = confirm is not null && confirm.Success;

            if (confirmed)
            {
                StatusMessage = "پرداخت تأیید شد. در حال چاپ رسید ...";
            }
            else
            {
                // پرداخت روی دستگاه موفق بوده؛ نتیجه در حافظه دستگاه ذخیره می‌شود تا
                // با دکمه «ارسال مجدد» بعد از برطرف شدن مشکل اتصال، به سرور اعلام شود
                _pendingStore.Add(new PendingBillConfirmation
                {
                    BillId = paidBill.BillId,
                    PayId = paidBill.PayId,
                    PaymentDate = BillApiService.BuildPersianPaymentDate(posResult),
                    TrackingNo = BillApiService.GetTrackingNo(posResult),
                    OwnerName = paidBill.OwnerName,
                    AmountRials = paidBill.AmountRials,
                    SavedAt = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
                });

                var reason = string.IsNullOrWhiteSpace(confirmError)
                    ? confirm?.Message ?? string.Empty
                    : confirmError;
                StatusMessage = "پرداخت موفق بود اما ثبت آن در سرور ناموفق بود" +
                                (string.IsNullOrWhiteSpace(reason) ? "." : $": {reason}") +
                                "\nنتیجه پرداخت در حافظه دستگاه ذخیره شد؛ پس از برطرف شدن مشکل اتصال، با دکمه «📤 ارسال مجدد به سرور» ارسال کنید.";
            }

            // چاپ رسید — پرداخت روی دستگاه موفق است؛ حتی اگر اعلام به سرور ناموفق بوده باشد چاپ می‌شود
            try
            {
                await _pos.PrintReceiptAsync(paidBill, posResult);
                StatusMessage = confirmed
                    ? "✅ پرداخت با موفقیت انجام و رسید چاپ شد."
                    : "✅ رسید چاپ شد؛ نتیجه پرداخت در حافظه ذخیره شد و در انتظار ارسال به سرور است.";
            }
            catch (Exception printEx)
            {
                StatusMessage = confirmed
                    ? $"پرداخت تأیید شد اما چاپ ناموفق بود: {printEx.Message}"
                    : $"چاپ رسید ناموفق بود ({printEx.Message})؛ نتیجه پرداخت در حافظه ذخیره شد و در انتظار ارسال به سرور است.";
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
        _pendingStore.Changed -= RefreshPendingConfirmations;
        _pos.Dispose();
    }
}
