using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Maui.Networking;

namespace Amard.App.Services;

/// <summary>
/// سرویس بررسی اتصال اینترنت دستگاه و در دسترس بودن سرور API.
/// از <see cref="Connectivity"/> مائویی برای وضعیت شبکه و از یک درخواست
/// واقعی HTTP برای تست رسیدن به سرور استفاده می‌کند.
/// </summary>
public static class ConnectivityService
{
    /// <summary>آیا دستگاه در حال حاضر به اینترنت دسترسی دارد؟</summary>
    public static bool IsConnected => Connectivity.NetworkAccess == NetworkAccess.Internet;

    /// <summary>متن فارسی کوتاه وضعیت اتصال اینترنت دستگاه</summary>
    public static string GetStatusText() => IsConnected
        ? "● متصل به اینترنت"
        : "○ بدون اتصال به اینترنت";

    /// <summary>
    /// تست مرحله‌ای: ۱) اینترنت دستگاه ۲) تبدیل دامنه به IP ‏(DNS)‏
    /// ۳) اتصال TCP به پورت سرور ۴) درخواست HTTP.
    /// با این مراحل دقیقاً مشخص می‌شود مشکل در کدام لایه است:
    /// DNS، فایروال/فیلتر پورت، یا مسیر (path) اشتباه روی سرور.
    /// توجه: چون آدرس پایه به‌خودی‌خود endpoint نیست، پاسخ 404 از سرور هم
    /// به‌معنای «سرور در دسترس است» تلقی می‌شود (فقط یعنی مسیر پایه endpoint ندارد).
    /// </summary>
    /// <returns>‎(ok: آیا سرور در دسترس است؟, message: گزارش فارسی مرحله‌به‌مرحله, latencyMs: زمان پاسخ)‎</returns>
    public static async Task<(bool Ok, string Message, long LatencyMs)> TestApiConnectionAsync(
        CancellationToken ct = default)
    {
        if (!IsConnected)
            return (false,
                "دستگاه به اینترنت متصل نیست. اتصال Wi-Fi یا دیتای سیم‌کارت دستگاه POS را بررسی کنید.",
                0);

        if (!AppConstants.TryGetApiBaseUrl(out var baseUrl, out var urlError))
            return (false, urlError, 0);

        var report = new StringBuilder();
        report.AppendLine($"آدرس: {baseUrl}");

        // هشدار «پورت داخل مسیر» (مثل /443) — رایج‌ترین اشتباه تنظیمات
        var pathWarning = AppConstants.GetPortInPathWarning();
        if (pathWarning is not null)
            report.AppendLine(pathWarning);

        Uri uri = new Uri(baseUrl);

        // اطلاعات دستگاه + پروکسی مؤثر (برای مقایسه با مرورگر)
        try
        {
            report.AppendLine($"دستگاه: Android API {(int)Android.OS.Build.VERSION.SdkInt} " +
                $"(Android {Android.OS.Build.VERSION.Release}), مدل {Android.OS.Build.Model}");
        }
        catch { /* ignore */ }

        try
        {
            var dp = HttpClient.DefaultProxy;
            report.AppendLine(dp is null || dp.IsBypassed(uri)
                ? "پروکسی مؤثر HttpClient: مستقیم (بدون پروکسی)"
                : $"پروکسی مؤثر HttpClient: {dp}");
        }
        catch (Exception ex)
        {
            report.AppendLine($"پروکسی مؤثر HttpClient: خطا در خواندن ({FirstLine(ex.Message)})");
        }

        foreach (var varName in new[] { "https_proxy", "http_proxy", "all_proxy", "HTTPS_PROXY", "HTTP_PROXY", "no_proxy" })
        {
            var val = Environment.GetEnvironmentVariable(varName);
            if (!string.IsNullOrWhiteSpace(val))
                report.AppendLine($"⚠ متغیر محیطی {varName}={val} روی دستگاه ست شده و ممکن است مسیر اپ را از مرورگر جدا کند.");
        }

        // ---- مرحله ۱: DNS ----
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.Host, ct);
            if (addresses.Length == 0)
                return (false, report + $"❌ مرحله DNS ناموفق: دامنه «{uri.Host}» به هیچ IP تبدیل نشد.",
                    0);

            report.AppendLine($"‏DNS: «{uri.Host}» = {string.Join("، ", addresses.Select(a => a.ToString()))}");

            // اگر دامنه به IP داخلی (شبکه محلی) resolve شود، از اینترنت موبایل قابل دسترسی نیست
            var privateOnes = addresses.Where(IsPrivateOrLoopback).Select(a => a.ToString()).ToList();
            if (privateOnes.Count > 0)
                report.AppendLine($"⚠ این IP داخلی ({string.Join("، ", privateOnes)}) متعلق به شبکه محلی است و " +
                    "از اینترنت سیم‌کارت/وای‌فایِ خارج از آن شبکه قابل دسترسی نیست! " +
                    "سرور باید روی IP عمومی (Public) باشد یا دستگاه POS به همان شبکه داخلی وصل شود.");
        }
        catch (Exception ex)
        {
            return (false,
                report + $"❌ مرحله DNS ناموفق: نام «{uri.Host}» به IP تبدیل نشد.\n" +
                $"علت: DNS دستگاه POS این دامنه را نمی‌شناسد (در محیط توسعه احتمالاً DNS داخلی شرکت آن را می‌شناسد).\n" +
                $"جزئیات: {ex.Message}",
                0);
        }

        // ---- مرحله ۲: پروکسی سیستم ----
        // Uri.Port برای https بدون پورت صریح، 443 و برای http، 80 برمی‌گرداند
        int port = uri.Port;
        var proxy = HttpProxyHelper.Detect(uri);
        bool tcpFailed = false;
        string? tcpFailureMessage = null;

        if (proxy is not null)
        {
            report.AppendLine($"پروکسی سیستم: {proxy.Value.Host}:{proxy.Value.Port} — تنظیمات وای‌فای دستگاه پروکسی دارد. " +
                "مرورگر از همین پروکسی استفاده می‌کند؛ تست TCP خام از پروکسی عبور نمی‌کند، " +
                "پس مستقیماً به تست HTTP (با همین پروکسی) می‌رویم.");
        }
        else
        {
            // ---- مرحله ۲ (بدون پروکسی): TCP تک‌تک آدرس‌ها، با اولویت IPv4 ----
            // چرا تک‌تک؟ اگر دامنه آدرس IPv6 هم برگرداند و IPv6 شبکه بدون پاسخ باشد
            // (در شبکه‌های ایران رایج است)، اتصالِ تکیِ هاست تا آخرین لحظه روی IPv6
            // می‌ماند و کل زمان‌سنج می‌سوزد؛ ولی مرورگر همزمان هر دو را امتحان می‌کند
            // و وصل می‌شود. این دقیقاً همان حالتی است که «مرورگر وصل می‌شود ولی اپ نه».
            bool tcpOk = false;
            string? lastTcpError = null;
            var swTcp = Stopwatch.StartNew();

            foreach (var addr in addresses.OrderBy(a => a.AddressFamily == AddressFamily.InterNetwork ? 0 : 1))
            {
                if (swTcp.Elapsed > TimeSpan.FromSeconds(10))
                {
                    lastTcpError ??= "زمان مجاز تست (۱۰ ثانیه) تمام شد";
                    break;
                }

                try
                {
                    using var tcp = new TcpClient(addr.AddressFamily);
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(5));
                    var swOne = Stopwatch.StartNew();
                    await tcp.ConnectAsync(addr, port, cts.Token);
                    report.AppendLine($"‏TCP: اتصال به {addr}:{port} برقرار شد ({swOne.ElapsedMilliseconds}ms)");
                    tcpOk = true;
                    break;
                }
                catch (Exception ex)
                {
                    lastTcpError = $"{addr}: {FirstLine(ex.Message)}";
                }
            }

            if (!tcpOk)
            {
                // هنوز قطعی نیست: از مسیر بالاتر (HttpClient) هم یک‌بار امتحان می‌کنیم
                tcpFailed = true;
                tcpFailureMessage =
                    $"❌ مرحله TCP خام ناموفق: اتصال مستقیم به {uri.Host}:{port} برقرار نشد.\n" +
                    $"آدرس‌های تست‌شده: {string.Join("، ", addresses)}\n" +
                    $"آخرین خطا: {lastTcpError}\n" +
                    "اگر مرورگر خودِ همین دستگاه با همین آدرس وصل می‌شود، دو احتمال است:\n" +
                    "  ۱) تنظیمات وای‌فای دستگاه «پروکسی» دارد و مرورگر از آن استفاده می‌کند — " +
                    "در تنظیمات وای‌فای (پیشرفته → تنظیمات پروکسی / Proxy) بررسی و در صورت نیاز پاک کنید.\n" +
                    "  ۲) IP برگشتی DNS با IP سرور واقعی فرق دارد — آدرس بالا را با پینگ در کامپیوتر مقایسه کنید.";
            }
        }

        // ---- مرحله ۲/۵: TLS — هر نسخه پروتکل جداگانه روی همین دستگاه امتحان می‌شود ----
        // TCP وصل می‌شود ولی HTTP می‌شکند یعنی دست‌دادن امنیتی (handshake) یا گواهی
        // مشکل دارد؛ مثلاً فروشگاه گواهی‌های اندروید قدیمی، ریشه گواهی جدید را ندارد
        // در حالی که مرورگر کروم ریشه‌های خودش را دارد و وصل می‌شود.
        var firstV4 = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addresses[0];
        X509Certificate2? leaf = null;
        var certProblems = new List<string>();

#pragma warning disable SYSLIB0039 // نسخه‌های قدیمی فقط برای تشخیص لازم‌اند
        foreach (var (protoName, proto) in new[]
        {
            ("TLS 1.3", SslProtocols.Tls13),
            ("TLS 1.2", SslProtocols.Tls12),
            ("TLS 1.1", SslProtocols.Tls11),
            ("TLS 1.0", SslProtocols.Tls),
        })
        {
            try
            {
                using var tcpTls = new TcpClient(firstV4.AddressFamily);
                using var ctsTls = CancellationTokenSource.CreateLinkedTokenSource(ct);
                ctsTls.CancelAfter(TimeSpan.FromSeconds(6));
                await tcpTls.ConnectAsync(firstV4, port, ctsTls.Token);

                using var ssl = new SslStream(tcpTls.GetStream(), false,
                    (s, c, ch, e) =>
                    {
                        if (c is not null)
                        {
                            try { leaf = new X509Certificate2(c); } catch { /* ignore */ }
                        }
                        if (e != SslPolicyErrors.None)
                        {
                            certProblems.Add($"{protoName}: policy={e}");
                            if (ch is not null)
                                foreach (var st in ch.ChainStatus)
                                    certProblems.Add($"{protoName}: {st.Status} {FirstLine(st.StatusInformation ?? string.Empty)}");
                        }
                        return true; // فقط برای تشخیص؛ ادامه می‌دهیم تا نسخه مذاکره‌شده معلوم شود
                    });

                await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = uri.Host,
                    EnabledSslProtocols = proto,
                }, ctsTls.Token);

                report.AppendLine($"‏TLS: {protoName} موفق (مذاکره‌شده: {ssl.SslProtocol}، رمز: {ssl.NegotiatedCipherSuite})");
                break;
            }
            catch (Exception ex)
            {
                report.AppendLine($"‏TLS: {protoName} ناموفق: {FirstLine(ex.Message)}");
            }
        }
#pragma warning restore SYSLIB0039

        if (leaf is not null)
            report.AppendLine($"گواهی سرور: {leaf.Subject} | صادرکننده: {leaf.Issuer} | انقضا: {leaf.NotAfter:yyyy-MM-dd}");

        foreach (var cp in certProblems.Distinct())
            report.AppendLine("⚠ مشکل گواهی: " + cp);

        // اعتبارسنجی سخت‌گیرانه سیستم (دقیقاً مثل HttpClient واقعی، بدون چشم‌پوشی)
        try
        {
            using var tcpStrict = new TcpClient(firstV4.AddressFamily);
            using var ctsStrict = CancellationTokenSource.CreateLinkedTokenSource(ct);
            ctsStrict.CancelAfter(TimeSpan.FromSeconds(8));
            await tcpStrict.ConnectAsync(firstV4, port, ctsStrict.Token);

            using var sslStrict = new SslStream(tcpStrict.GetStream(), false, null);
            await sslStrict.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = uri.Host,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            }, ctsStrict.Token);

            report.AppendLine("‏TLS: ✅ اعتبارسنجی سخت‌گیرانه سیستم هم موفق بود.");
        }
        catch (Exception ex)
        {
            report.AppendLine($"‏TLS: ❌ اعتبارسنجی سخت‌گیرانه سیستم ناموفق بود: {FullChain(ex)}");
        }

        // اعتبارسنجی با ریشه داخلی اپ (دقیقاً همان منطق واقعی برنامه) —
        // اگر این موفق باشد، اتصال واقعی برنامه هم برقرار می‌شود
        try
        {
            using var tcpApp = new TcpClient(firstV4.AddressFamily);
            using var ctsApp = CancellationTokenSource.CreateLinkedTokenSource(ct);
            ctsApp.CancelAfter(TimeSpan.FromSeconds(10));
            await tcpApp.ConnectAsync(firstV4, port, ctsApp.Token);

            using var sslApp = new SslStream(tcpApp.GetStream(), false,
                CertificateValidator.Validate);
            await sslApp.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = uri.Host,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            }, ctsApp.Token);

            report.AppendLine($"‏TLS: ✅ اعتبارسنجی با ریشه داخلی اپ موفق بود (مذاکره‌شده: {sslApp.SslProtocol}) — اتصال واقعی برنامه هم برقرار می‌شود.");
        }
        catch (Exception ex)
        {
            report.AppendLine($"‏TLS: ❌ اعتبارسنجی با ریشه داخلی اپ ناموفق بود: {FullChain(ex)}");
        }

        // ---- مرحله ۳: HTTP (با همان HttpClient واقعی برنامه: لنگر داخلی + پروکسی) ----
        try
        {
            using var client = ApiHttpClientFactory.Create(TimeSpan.FromSeconds(12));
            if (proxy is not null)
                report.AppendLine("‏HTTP: درخواست با پروکسی سیستم ارسال می‌شود (همان مسیر مرورگر).");

            var sw = Stopwatch.StartNew();
            using var response = await client.GetAsync(baseUrl, ct);
            sw.Stop();

            int code = (int)response.StatusCode;

            // اگر TCP خام شکست خورده ولی HTTP جواب داده، یعنی مسیر بالاتر (با پروکسی یا
            // مکانیزم دیگر سیستم) کار می‌کند — خوش‌خیم است و گزارش می‌دهیم
            if (tcpFailed)
                report.AppendLine("‏✅ نکته: اتصال خام TCP برقرار نشد ولی درخواست HTTP از مسیر بالاتر جواب داد — " +
                    "یعنی اپ می‌تواند با سرور کار کند.");

            return code switch
            {
                >= 200 and < 300 => (true,
                    report + $"✅ اتصال کامل برقرار است. (پاسخ {code} در {sw.ElapsedMilliseconds} میلی‌ثانیه)",
                    sw.ElapsedMilliseconds),

                // 404 یعنی سرور زنده است و جواب داد، فقط این مسیر روی سرور endpoint ندارد
                404 or 405 => (false,
                    report + $"⚠ سرور در دسترس است و پاسخ داد (کد {code})، ولی این مسیر روی سرور وجود ندارد! " +
                    $"«مسیر پایه سرور» یا بخش‌های اضافی مسیر (مثل /443) را در تنظیمات اصلاح کنید. ({sw.ElapsedMilliseconds}ms)",
                    sw.ElapsedMilliseconds),

                400 or 401 or 403 => (true,
                    report + $"سرور در دسترس است (پاسخ {code}). اگر جستجو خطا داد، تنظیمات احراز هویت سرور را بررسی کنید. ({sw.ElapsedMilliseconds}ms)",
                    sw.ElapsedMilliseconds),

                _ => (false,
                    report + $"سرور پاسخ داد ولی خطای {code} برگرداند. ({sw.ElapsedMilliseconds}ms)",
                    sw.ElapsedMilliseconds),
            };
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return (false,
                report + (tcpFailed ? tcpFailureMessage + "\n\n" : string.Empty) +
                "مرحله HTTP هم timeout شد (۱۲ ثانیه پاسخ نداد).",
                0);
        }
        catch (HttpRequestException ex)
        {
            return (false,
                report + (tcpFailed ? tcpFailureMessage + "\n\n" : string.Empty) +
                $"مرحله HTTP هم ناموفق بود.\nعلت: {ToPersianReason(ex)}\nزنجیره خطا: {FullChain(ex)}",
                0);
        }
        catch (Exception ex)
        {
            return (false,
                report + (tcpFailed ? tcpFailureMessage + "\n\n" : string.Empty) +
                $"مرحله HTTP هم ناموفق بود.\nزنجیره خطا: {FullChain(ex)}",
                0);
        }
    }

    /// <summary>
    /// زنجیره کامل خطا: نوع و پیام هر سطح + کد سوکت؛ چون پیام سطح آخر
    /// (مثل «connection failure») به‌تنهایی علت را مشخص نمی‌کند.
    /// </summary>
    private static string FullChain(Exception ex)
    {
        var sb = new StringBuilder();
        for (var e = ex; e is not null; e = e.InnerException)
        {
            sb.Append($"[{e.GetType().Name}] {FirstLine(e.Message)}");
            if (e is SocketException se)
                sb.Append($" (SocketError={se.SocketErrorCode})");
            if (e.InnerException is not null)
                sb.Append(" ⟵ ");
        }
        return sb.ToString();
    }

    /// <summary>اولین خط پیام خطا (پیام‌های چندخطی را خلاصه می‌کند)</summary>
    private static string FirstLine(string message)
    {
        var idx = message.IndexOf('\n');
        return idx > 0 ? message[..idx].TrimEnd() : message;
    }

    /// <summary>آیا IP داخلی (شبکه محلی)، لوپ‌بک یا لینک‌لوکال است؟</summary>
    private static bool IsPrivateOrLoopback(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
            return true;

        var b = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            // ‎10.0.0.0/8‎، ‎172.16.0.0/12‎، ‎192.168.0.0/16‎، ‎127.0.0.0/8‎، ‎169.254.0.0/16‎
            return b[0] == 10
                || b[0] == 127
                || (b[0] == 172 && b[1] is >= 16 and <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 169 && b[1] == 254);
        }

        // IPv6: ‎::1‎، ‎fc00::/7 (unique local)‎، ‎fe80::/10 (link local)‎
        return ip.Equals(IPAddress.IPv6Loopback)
            || (b[0] & 0xfe) == 0xfc
            || (b[0] == 0xfe && (b[1] & 0xc0) == 0x80);
    }

    /// <summary>ترجمه علت‌های رایج خطای شبکه به فارسی</summary>
    internal static string ToPersianReason(HttpRequestException ex)
    {
        var msg = (ex.Message + " " + ex.InnerException?.Message).ToLowerInvariant();

        if (msg.Contains("name") || msg.Contains("host") || msg.Contains("dns") || msg.Contains("nodename"))
            return "نام دامنه/هاست به IP تبدیل نشد (مشکل DNS یا اشتباه بودن آدرس). اگر با دامنه وصل می‌شوید، DNS دستگاه POS را بررسی کنید.";

        if (msg.Contains("refused"))
            return "سرور اتصال را رد کرد — یا سرور خاموش است یا پورت بسته/فایروال جلوی آن را گرفته است.";

        if (msg.Contains("connection failure") || msg.Contains("failed to connect") || msg.Contains("could not be made"))
            return "اتصال TCP به سرور برقرار نشد — دستگاه به اینترنت وصل است ولی بسته‌هایش به این سرور:پورت نمی‌رسد. " +
                "معمولاً فایروال سرور IP دستگاه را قبول نمی‌کند (سرورهای سازمانی اغلب فقط IP دفتر را باز می‌گذارند) " +
                "یا شبکه/اپراتور دستگاه این پورت را فیلتر کرده است.";

        if (msg.Contains("timed out") || msg.Contains("timeout"))
            return "زمان اتصال تمام شد — سرور خیلی کند است یا مسیر شبکه به آن مسدود است.";

        if (msg.Contains("unreachable") || msg.Contains("network"))
            return "مسیر شبکه به سرور وجود ندارد — دستگاه به اینترنت وصل است ولی به این سرور مسیری ندارد (مثلاً سرور فقط در شبکه داخلی شرکت است یا سیم‌کارت POS اینترنت آزاد ندارد).";

        if (msg.Contains("ssl") || msg.Contains("tls") || msg.Contains("certificate") || msg.Contains("handshake"))
            return "مشکل امنیتی اتصال (SSL/TLS) — تاریخ/ساعت دستگاه POS را بررسی کنید و مطمئن شوید گواهی سرور معتبر است.";

        if (msg.Contains("cleartext"))
            return "اتصال ناامن (http) توسط سیستم مسدود شد.";

        return "دستگاه به اینترنت وصل است ولی سرور پاسخ نمی‌دهد. آدرس، پورت و روشن بودن سرور را بررسی کنید.";
    }
}
