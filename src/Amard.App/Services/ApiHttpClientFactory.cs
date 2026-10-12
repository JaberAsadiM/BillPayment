using System.Net.Security;
using System.Security.Authentication;

namespace Amard.App.Services;

/// <summary>
/// ساخت HttpClient استاندارد برنامه برای ارتباط با API.
/// تفاوت با HttpClient معمولی:
///  - اعتبارسنجی گواهی با لنگر داخلی (CertificateValidator) تا روی دستگاه‌های
///    قدیمی که ریشه Certum را در فروشگاه سیستم ندارند هم TLS برقرار شود؛
///  - پروکسی وای‌فای سیستم (همان مسیر مرورگر) در صورت وجود اعمال می‌شود.
/// </summary>
public static class ApiHttpClientFactory
{
    /// <summary>ساخت HttpClient با timeout مشخص برای فراخوانی‌های API</summary>
    public static HttpClient Create(TimeSpan timeout)
    {
        HttpProxyHelper.EnsureConfigured();

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            SslOptions = new SslClientAuthenticationOptions
            {
                // سرور تا TLS 1.2 می‌دهد؛ 1.1 هم مجاز است چون روی سخت‌افزار ضعیف
                // POS مذاکره 1.2 گاهی کند/ناموفق است و سرور 1.1 را هم قبول می‌کند.
                // سرور بالاترین نسخه مشترک (1.2) را انتخاب می‌کند.
                EnabledSslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12 | SslProtocols.Tls11,

                // بررسی ابطال (OCSP) روی شبکه POS اغلب ناممکن است؛ مرورگرها هم در
                // عمل soft-fail می‌کنند. امنیت با پین لنگر حفظ می‌شود.
                CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck,

                RemoteCertificateValidationCallback = CertificateValidator.Validate,
            }
        };

        return new HttpClient(handler) { Timeout = timeout };
    }
}
