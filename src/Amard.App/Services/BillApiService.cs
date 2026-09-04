using Amard.App.Models;
using System.Text;
using System.Text.Json;

namespace Amard.App.Services;

/// <summary>
/// سرویس ارتباط با سرور برای جستجوی قبض و تأیید پرداخت.
/// Endpoint ها sample هستند و باید با API واقعی سرور هماهنگ شوند.
/// </summary>
public interface IBillApiService
{
    Task<BillInfo> SearchBillAsync(BillSearchRequest request, CancellationToken ct = default);
    Task<PaymentConfirmResult> ConfirmPaymentAsync(BillInfo bill, PosPaymentResult posResult, CancellationToken ct = default);
}

public class BillApiService : IBillApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public BillApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// جستجوی قبض بر اساس نام مالک / کد ملی / کد نوسازی و دریافت مبلغ از سرور.
    /// </summary>
    public async Task<BillInfo> SearchBillAsync(BillSearchRequest request, CancellationToken ct = default)
    {
        // آدرس کامل در لحظه از تنظیمات خوانده می‌شود (تغییر تنظیمات بلافاصله اعمال می‌شود)
        var url = $"{AppConstants.ApiBaseUrl}GetMalekin?codeNosazi={Uri.EscapeDataString(request.SearchValue)}";

        using var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var result = await JsonSerializer.DeserializeAsync<List<BillInfo>>(stream, JsonOptions, ct);
        result.FirstOrDefault()?.AmountRials = 15000;
        result.FirstOrDefault()?.NationalCode = result.FirstOrDefault()?.kodemeli;
        result.FirstOrDefault()?.OwnerName = $"{result.FirstOrDefault()?.name} {result.FirstOrDefault()?.family}";

        if (result is null || result.FirstOrDefault()?.AmountRials <= 0)
            throw new InvalidOperationException("قبضی با این مشخصات یافت نشد یا مبلغ نامعتبر است.");

        return result.FirstOrDefault();
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
            maskedCard = posResult.MaskedCard,
            stan = posResult.Stan,
            rrn = posResult.Rrn,
            transactionDate = posResult.TransactionDate
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        // آدرس کامل در لحظه از تنظیمات خوانده می‌شود (تغییر تنظیمات بلافاصله اعمال می‌شود)
        using var response = await _httpClient.PostAsync($"{AppConstants.ApiBaseUrl}bill/confirm-payment", content, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var result = await JsonSerializer.DeserializeAsync<PaymentConfirmResult>(stream, JsonOptions, ct);

        return result ?? new PaymentConfirmResult { Success = true, Message = "تأیید پرداخت ارسال شد." };
    }
}
