using System.Net;

namespace Amard.App.Services;

/// <summary>
/// تشخیص و اعمال پروکسی تنظیم‌شده در سیستم اندروید (پروکسی دستی وای‌فای یا PAC).
/// نکته مهم: مرورگر دستگاه از این پروکسی استفاده می‌کند ولی TcpClient در .NET
/// اصلاً پروکسی را نمی‌بیند و HttpClient هم به‌صورت خودکار پراپرتی‌های جاوا را
/// نمی‌خواند؛ در نتیجه مرورگر وصل می‌شود ولی اپ خطا می‌دهد.
/// راه‌حل: پروکسی سیستم را یک‌بار تشخیص داده و در HttpClient.DefaultProxy (سراسری)
/// ثبت می‌کنیم تا همه‌ی HttpClientهای برنامه از آن استفاده کنند.
/// </summary>
public static class HttpProxyHelper
{
    private static bool _configured;

    /// <summary>
    /// پروکسی تنظیم‌شده سیستم برای این آدرس؛ null یعنی اتصال مستقیم.
    /// از پراپرتی‌های جاوا خوانده می‌شود (http.proxyHost / https.proxyHost
    /// که اندروید بر اساس تنظیمات وای‌فای در پروسه برنامه ست می‌کند).
    /// اگر آدرس داخل لیست مستثنیات (nonProxyHosts) باشد هم null برمی‌گردد.
    /// </summary>
    public static (string Host, int Port)? Detect(Uri uri)
    {
        try
        {
            var scheme = uri.Scheme == Uri.UriSchemeHttps ? "https" : "http";

            // ترتیب: همان scheme، بعد http، بعد https
            foreach (var prefix in new[] { scheme, "http", "https" }.Distinct())
            {
                var host = Java.Lang.JavaSystem.GetProperty($"{prefix}.proxyHost");
                var portText = Java.Lang.JavaSystem.GetProperty($"{prefix}.proxyPort");
                if (string.IsNullOrWhiteSpace(host) || !int.TryParse(portText, out var port) || port <= 0)
                    continue;

                var bypass = Java.Lang.JavaSystem.GetProperty($"{prefix}.nonProxyHosts")
                          ?? Java.Lang.JavaSystem.GetProperty("http.nonProxyHosts");
                if (!string.IsNullOrWhiteSpace(bypass) && MatchesBypassList(uri.Host, bypass))
                    return null;

                return (host.Trim(), port);
            }
        }
        catch { /* ignore */ }

        return null;
    }

    /// <summary>
    /// ثبت پروکسی سیستم در HttpClient.DefaultProxy — فقط یک‌بار اجرا می‌شود
    /// و همه‌ی HttpClientهای بعدی برنامه به‌طور خودکار از آن استفاده می‌کنند.
    /// </summary>
    public static void EnsureConfigured()
    {
        if (_configured)
            return;
        _configured = true;

        try
        {
            var proxy = Detect(new Uri("https://localhost/")) ?? Detect(new Uri("http://localhost/"));
            if (proxy is null)
                return;

            var bypassList = GetBypassPatterns();
            HttpClient.DefaultProxy = new WebProxy(
                new Uri($"http://{proxy.Value.Host}:{proxy.Value.Port}"),
                true,   // BypassOnLocal: آدرس‌های لوکال مستقیم وصل شوند
                bypassList);
        }
        catch { /* اگر نشد، اتصال مستقیم انجام می‌شود */ }
    }

    /// <summary>تبدیل nonProxyHosts (با جداکننده | و کارت‌های *) به الگوهای WebProxy</summary>
    private static string[] GetBypassPatterns()
    {
        try
        {
            var bypass = Java.Lang.JavaSystem.GetProperty("https.nonProxyHosts")
                      ?? Java.Lang.JavaSystem.GetProperty("http.nonProxyHosts");
            if (string.IsNullOrWhiteSpace(bypass))
                return [];

            return bypass
                .Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => "^" + System.Text.RegularExpressions.Regex.Escape(p.Trim()).Replace("\\*", ".*") + "$")
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// آیا میزبان در لیست مستثنیات پروکسی (nonProxyHosts) است؟
    /// فرمت لیست: الگوهای جداشده با «|» که هر کدام می‌تواند «*» داشته باشد،
    /// مثلاً «localhost|127.0.0.1|192.168.*»
    /// </summary>
    private static bool MatchesBypassList(string host, string bypassList)
    {
        foreach (var pattern in bypassList.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = pattern.Trim();
            if (p.Length == 0)
                continue;

            var regex = "^" + System.Text.RegularExpressions.Regex.Escape(p).Replace("\\*", ".*") + "$";
            if (System.Text.RegularExpressions.Regex.IsMatch(
                    host, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return true;
        }

        return false;
    }
}
