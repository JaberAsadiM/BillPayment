using System.Text;
using System.Text.RegularExpressions;

namespace Amard.App.Services;

/// <summary>نتیجه ارسال پیامک</summary>
public class SmsSendResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    /// <summary>شناسه پیامِ برگشتی از سرور در ارسال موفق</summary>
    public long MessageId { get; set; }
}

/// <summary>
/// سرویس ارسال پیامک از طریق API خود سرور.
///
/// آدرس ارسال:
/// {ApiBaseUrl}/BaseControl/SendSMS/SendMessageSms?message={text}&strMobileNo={mob}
///
/// سرور (سمت BaseControl) مسئول ارسال نهایی پیام است و اپ فقط
/// متن پیام و شماره موبایل را برای آن می‌فرستد.
/// متن پیام از تنظیمات برنامه (AppConstants ← صفحه «تنظیمات») خوانده می‌شود.
/// داخل متن پیام، عبارت‌های داخل آکولاد با مقادیر واقعی قبض/پرداخت جایگزین می‌شوند:
///   {BillId} → شناسه قبض، {PayId} → شناسه پرداخت، {TrackingNo} → شماره پیگیری دستگاه POS
/// </summary>
public class SmsService
{
    private readonly HttpClient _httpClient;

    public SmsService() : this(ApiHttpClientFactory.Create(TimeSpan.FromSeconds(30)))
    {
    }

    public SmsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// ارسال پیامک به شماره موبایل مشخص (فیلد mob مالک انتخاب‌شده).
    /// متن و شماره به API سرور فرستاده می‌شود و ارسال نهایی روی سرور انجام می‌گیرد.
    /// </summary>
    public async Task<SmsSendResult> SendAsync(string mobile, string billId, string payId, string trackingNo = "", CancellationToken ct = default)
    {
        var to = NormalizeMobile(mobile);
        if (to.Length == 0)
            return new SmsSendResult { Message = $"شماره موبایل مالک («{(mobile ?? string.Empty).Trim()}») معتبر نیست؛ پیامک ارسال نشد." };

        // متن پیام از تنظیمات + جایگزینی شناسه‌ها (بدون حساسیت به بزرگی/کوچکی حروف)
        var text = AppConstants.SmsMessageText
            .Replace("{BillId}", billId, StringComparison.OrdinalIgnoreCase)
            .Replace("{PayId}", payId, StringComparison.OrdinalIgnoreCase)
            .Replace("{TrackingNo}", trackingNo ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        text = MyFunction.EncryptUrlsInText(text);

        if (!AppConstants.TryGetApiBaseUrl(out var baseUrl, out var baseUrlError))
            return new SmsSendResult { Message = $"آدرس سرور نامعتبر است؛ پیامک ارسال نشد. {baseUrlError}" };

        // همه‌ی مقادیر باید URL-encode شوند (متن فارسی و کاراکترهای & و = داخل لینک می‌شکنند)
        var url = $"{baseUrl.TrimEnd('/')}/BaseControl/SendSMS/SendMessageSms" +
                  $"?message={Uri.EscapeDataString(text)}" +
                  $"&strMobileNo={Uri.EscapeDataString(to)}";
        url = url.Replace("/api/PaymentAvarez", "");

        string body;
        try
        {
            // متد سمت سرور [HttpPost] است؛ پارامترها از query string خوانده می‌شوند
            using var response = await _httpClient.PostAsync(url, content: null, ct);
            body = await response.Content.ReadAsStringAsync(ct);

            //if (!response.IsSuccessStatusCode)
            //return new SmsSendResult { Message = $"پیام ارسال شد." };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new SmsSendResult { Message = "سرور پیامک در زمان مقرر پاسخ نداد (اتصال قطع یا کند است)." };
        }
        catch (Exception ex)
        {
            return new SmsSendResult { Message = $"ارسال پیامک از طریق سرور انجام نشد: {ex.Message}" };
        }

        return new SmsSendResult
        {
            Success = true,
            //MessageId = ParseFirstCode() ?? 0,
            Message = "درخواست ارسال پیامک به سرور ارسال شد."
        };
    }

    /// <summary>
    /// نرمال‌سازی شماره موبایل: ارقام فارسی/عربی به انگلیسی و حذف فاصله/خط تیره/پرانتز.
    /// خروجی خالی یعنی شماره‌ای در کار نیست.
    /// </summary>
    private static string NormalizeMobile(string? mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile))
            return string.Empty;

        var sb = new StringBuilder(mobile.Length);
        foreach (var ch in mobile.Trim())
        {
            if (ch is >= '۰' and <= '۹')
                sb.Append((char)('0' + (ch - '۰')));
            else if (ch is >= '٠' and <= '٩')
                sb.Append((char)('0' + (ch - '٠')));
            else if (char.IsDigit(ch))
                sb.Append(ch);
            // فاصله، خط تیره، پرانتز و ... حذف می‌شوند
        }

        return sb.ToString();
    }

    /// <summary>اولین عدد داخل پاسخ سرور (در صورت برگرداندن شناسه پیام)</summary>
    private static long? ParseFirstCode(string body)
    {
        var match = Regex.Match(body ?? string.Empty, @"-?\d+");
        return match.Success && long.TryParse(match.Value, out var code) ? code : null;
    }

    /// <summary>کوتاه‌کردن متن پاسخ برای نمایش در پیام خطا</summary>
    private static string Shrink(string text)
    {
        text = (text ?? string.Empty).Trim();
        return text.Length > 200 ? text[..200] + "…" : text;
    }
}
