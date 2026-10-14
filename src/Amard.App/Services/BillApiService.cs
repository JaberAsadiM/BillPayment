using Amard.App.Models;
using Microsoft.Maui.Networking;
using System.Text;
using System.Text.Json;

namespace Amard.App.Services;

/// <summary>
/// سرویس ارتباط با سرور برای جستجوی قبض و تأیید پرداخت.
/// Endpoint ها sample هستند و باید با API واقعی سرور هماهنگ شوند.
/// </summary>
public interface IBillApiService
{
    Task<List<Owners>> SearchOwnersAsync(BillSearchRequest request, CancellationToken ct = default);
    Task<List<ParvandehViewModel>> SearchParvandehByNationalCodeAsync(string nationalCode, CancellationToken ct = default);
    Task<List<BillInfo>> GetBillAsync(Owners owner, CancellationToken ct = default);
    Task<PaymentConfirmResult> ConfirmPaymentAsync(BillInfo bill, PosPaymentResult posResult, CancellationToken ct = default);
}

public class BillApiService : IBillApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public BillApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;

        // اگر در تنظیمات وای‌فای دستگاه پروکسی تعریف شده باشد (مرورگر از آن استفاده
        // می‌کند و جواب می‌گیرد)، باید پروکسی در HttpClient سراسری برنامه ثبت شود
        // تا این HttpClient هم از همان مسیر (مرورگر) استفاده کند.
        HttpProxyHelper.EnsureConfigured();
    }

    /// <summary>
    /// اعتبارسنجی آدرس سرور + بررسی اتصال اینترنت قبل از هر درخواست.
    /// آدرس نهایی را برمی‌گرداند تا در پیام خطا هم استفاده شود.
    /// </summary>
    private static string RequireBaseUrl()
    {
        if (!AppConstants.TryGetApiBaseUrl(out var baseUrl, out var error))
            throw new InvalidOperationException(error);

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException(
                $"آدرس سرور نامعتبر است: «{baseUrl}». در صفحه تنظیمات آدرس صحیح را وارد کنید.");

        // هشدار مشخص برای رایج‌ترین اشتباه: localhost روی دستگاه واقعی یعنی خودِ دستگاه POS
        if (!IsEmulatorHost(uri.Host) &&
            (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.Equals("127.0.0.1") ||
             uri.Host.Equals("10.0.2.2")))
            throw new InvalidOperationException(
                $"آدرس سرور «{uri.Host}» روی دستگاه واقعی به خودِ دستگاه POS اشاره می‌کند، نه به سرور! " +
                "در صفحه «تنظیمات»، فیلد «آدرس سرور برای دستگاه واقعی» را به IP شبکه یا دامنه اینترنتی سرور تغییر دهید.");

        if (Connectivity.NetworkAccess != NetworkAccess.Internet)
            throw new InvalidOperationException(
                "دستگاه به اینترنت متصل نیست. اتصال Wi-Fi یا دیتای سیم‌کارت دستگاه POS را بررسی کنید.");

        return baseUrl;
    }

    private static bool IsEmulatorHost(string host) =>
        host.Equals("10.0.2.2", StringComparison.OrdinalIgnoreCase);

    /// <summary>درخواست GET با پیام خطای فارسی شامل آدرس سرور</summary>
    private async Task<HttpResponseMessage> GetWithErrorsAsync(string url, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"سرور در زمان تعیین‌شده پاسخ نداد.\nآدرس: {url}\nسرعت اینترنت دستگاه یا روشن بودن سرور را بررسی کنید.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"اتصال به سرور برقرار نشد.\nآدرس: {url}\n{ConnectivityService.ToPersianReason(ex)}\nجزئیات: {ex.Message}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = string.Empty;
            try { body = await response.Content.ReadAsStringAsync(ct); } catch { /* ignore */ }
            if (body.Length > 300) body = body[..300] + "…";
            throw new InvalidOperationException(
                $"سرور خطای {(int)response.StatusCode} برگرداند.\nآدرس: {url}" +
                (string.IsNullOrWhiteSpace(body) ? string.Empty : $"\nپاسخ سرور: {body}"));
        }

        return response;
    }

    /// <summary>درخواست POST با پیام خطای فارسی شامل آدرس سرور</summary>
    private async Task<HttpResponseMessage> PostWithErrorsAsync(string url, HttpContent content, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync(url, content, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"سرور در زمان تعیین‌شده پاسخ نداد.\nآدرس: {url}", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"اتصال به سرور برقرار نشد.\nآدرس: {url}\n{ConnectivityService.ToPersianReason(ex)}\nجزئیات: {ex.Message}", ex);
        }

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"ثبت تأیید در سرور ناموفق بود (کد {(int)response.StatusCode}).\nآدرس: {url}");

        return response;
    }

    /// <summary>
    /// جستجوی مالکین بر اساس نام مالک / کد ملی / کد نوسازی و دریافت لیست کامل نتایج از سرور.
    /// </summary>
    public async Task<List<Owners>> SearchOwnersAsync(BillSearchRequest request, CancellationToken ct = default)
    {
        // آدرس کامل در لحظه از تنظیمات خوانده می‌شود (تغییر تنظیمات بلافاصله اعمال می‌شود)
        var baseUrl = RequireBaseUrl();
        var url = "";

        if (request.SearchType == BillSearchType.RenovationCode)
            url = $"{baseUrl}GetMalekin?codeNosazi={Uri.EscapeDataString(request.SearchValue)}";

        else if (request.SearchType == BillSearchType.PostalCode)
            url = $"{baseUrl}GetMalekinByPostalCode?postalCode={Uri.EscapeDataString(request.SearchValue)}";


        using var response = await GetWithErrorsAsync(url, ct);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var result = await JsonSerializer.DeserializeAsync<List<Owners>>(stream, JsonOptions, ct);

        if (result is null || result.Count == 0)
            throw new InvalidOperationException("مالکی با این مشخصات یافت نشد.");

        List<string> renovationCode = await GetRenovationCode(result, ct);
        List<GetRenovationCodeViewModel> newRenovationCode = [];

        foreach (var item in renovationCode)
        {
            var newItem = item.Split('#');
            decimal.TryParse(newItem[0], out var folderId);

            decimal proprtyFolderId = 0;
            if (newItem.Length > 2)
                decimal.TryParse(newItem[2], out proprtyFolderId);

            newRenovationCode.Add(new GetRenovationCodeViewModel()
            {
                FolderId = folderId,
                RenovationCode = newItem[1],
                ProprtyFolderId = proprtyFolderId
            });
        }


        // پر کردن فیلدهای نمایشی برای همه‌ی مالکین
        foreach (var owner in result)
        {
            owner.OwnerName = $"{owner.name} {owner.family}".Trim();
            owner.NationalCode = owner.kodemeli ?? string.Empty;
            owner.RenovationCode = newRenovationCode.FirstOrDefault(a => a.FolderId == owner.shop ||
                                                                         a.ProprtyFolderId == owner.shop)?.RenovationCode ?? "";
        }

        return result;
    }

    /// <summary>
    ///  دریافت لیست کد نوسازی از سرور.
    /// </summary>
    public async Task<List<string>> GetRenovationCode(List<Owners> owners, CancellationToken ct = default)
    {
        // استخراج FolderIdها از لیست Owners
        var folderIds = owners.Select(o => (int)o.shop).Distinct().ToList();

        // ساخت query string با فرمت صحیح
        //var folderIdParams = string.Join("&folderId=", folderIds);
        var folderIdParams = string.Join(",", folderIds);
        var url = $"{RequireBaseUrl()}GetRenovationCode?folderId={folderIdParams}";

        using var response = await GetWithErrorsAsync(url, ct);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var result = await JsonSerializer.DeserializeAsync<List<string>>(stream, JsonOptions, ct);

        if (result is null || result.Count == 0)
            throw new InvalidOperationException("مالکی با این مشخصات یافت نشد.");

        return result;
    }

    /// <summary>
    /// جستجوی پرونده‌ها بر اساس کد ملی.
    /// سرور لیستی از <see cref="ParvandehViewModel"/> برمی‌گرداند که فقط
    /// کد نوسازی (codeN) و آدرس آن به کاربر نمایش داده می‌شود.
    /// </summary>
    public async Task<List<ParvandehViewModel>> SearchParvandehByNationalCodeAsync(string nationalCode, CancellationToken ct = default)
    {
        // آدرس کامل در لحظه از تنظیمات خوانده می‌شود (تغییر تنظیمات بلافاصله اعمال می‌شود)
        var url = $"{RequireBaseUrl()}GetMyProperty?nationalCode={Uri.EscapeDataString(nationalCode)}";

        using var response = await GetWithErrorsAsync(url, ct);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var result = await JsonSerializer.DeserializeAsync<List<ParvandehViewModel>>(stream, JsonOptions, ct);

        if (result is null || result.Count == 0)
            throw new InvalidOperationException("پرونده‌ای با این کد ملی یافت نشد.");

        return result;
    }

    /// <summary>
    /// دریافت لیست قبض‌ها (مبلغ، شناسه قبض و شناسه پرداخت) برای مالک انتخاب‌شده از سرور.
    /// توجه: نام endpoint و پارامترها نمونه است و باید با API واقعی سرور هماهنگ شود.
    /// </summary>
    public async Task<List<BillInfo>> GetBillAsync(Owners owner, CancellationToken ct = default)
    {
        // آدرس کامل در لحظه از تنظیمات خوانده می‌شود (تغییر تنظیمات بلافاصله اعمال می‌شود)
        var url = $"{RequireBaseUrl()}ReceiveBill" +
                  $"?codeNosazi={Uri.EscapeDataString(owner.RenovationCode)}" +
                  $"&ownerId={Uri.EscapeDataString(((int)owner.id).ToString())}";

        using var response = await GetWithErrorsAsync(url, ct);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var estelamGhabzList = await JsonSerializer.DeserializeAsync<List<EstelamGhabz>>(stream, JsonOptions, ct);

        if (estelamGhabzList is null || estelamGhabzList.Count == 0)
            throw new InvalidOperationException("قبضی برای این مالک یافت نشد.");

        // تبدیل همه‌ی قبض‌های دریافتی به لیست BillInfo
        var bills = estelamGhabzList
            .Where(e => e.Price > 0 && !string.IsNullOrWhiteSpace(e.ShenaseGhabz))
            .Select(e => new BillInfo
            {
                AmountRials = (long)e.Price,
                BillId = e.ShenaseGhabz,
                PayId = e.ShenasePardakht ?? string.Empty,
                OwnerName = e.NameOwner ?? string.Empty,
                Period = e.Year.ToString(),
                Description = e.Description ?? string.Empty
            })
            .ToList();

        if (bills.Count == 0)
            throw new InvalidOperationException("قبض معتبری برای این مالک یافت نشد یا مبلغ نامعتبر است.");

        return bills;
    }

    /// <summary>
    /// اعلام پرداخت موفق به سرور (تأیید نهایی پرداخت قبض).
    /// </summary>
    public async Task<PaymentConfirmResult> ConfirmPaymentAsync(BillInfo bill, PosPaymentResult posResult, CancellationToken ct = default)
    {
        var payload = new
        {
            billId = bill.BillId,
            payId = bill.PayId,
            amountRials = bill.AmountRials,
            appliedAmountRials = posResult.AppliedAmountRials,
            maskedCard = posResult.MaskedCard,
            stan = posResult.Stan,
            rrn = posResult.Rrn,
            hostTranId = posResult.HostTranId,
            paidByCredit = posResult.PaidByCredit,
            transactionDate = posResult.TransactionDate
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        // آدرس کامل در لحظه از تنظیمات خوانده می‌شود (تغییر تنظیمات بلافاصله اعمال می‌شود)
        using var response = await PostWithErrorsAsync($"{RequireBaseUrl()}bill/confirm-payment", content, ct);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var result = await JsonSerializer.DeserializeAsync<PaymentConfirmResult>(stream, JsonOptions, ct);

        return result ?? new PaymentConfirmResult { Success = true, Message = "تأیید پرداخت ارسال شد." };
    }
}
