namespace Amard.App.Services;

using System.Net.Http.Headers;
using System.Text.Json;
using Android.Content;
using Microsoft.Maui.ApplicationModel;

/// <summary>نتیجه بررسی نسخه جدید از GitHub Releases</summary>
public sealed record UpdateCheckResult
{
    /// <summary>آیا نسخه جدیدتری از نسخه نصب‌شده موجود است؟</summary>
    public bool UpdateAvailable { get; init; }

    /// <summary>نسخه آخرین Release منتشرشده (مثلاً «1.2.0»)</summary>
    public string LatestVersion { get; init; } = string.Empty;

    /// <summary>نسخه نصب‌شده روی دستگاه (مثلاً «1.0.0»)</summary>
    public string CurrentVersion { get; init; } = string.Empty;

    /// <summary>یادداشت‌های انتشار (Release notes)</summary>
    public string ReleaseNotes { get; init; } = string.Empty;

    /// <summary>آدرس دانلود مستقیم فایل APK</summary>
    public string DownloadUrl { get; init; } = string.Empty;

    /// <summary>نام فایل APK در Release</summary>
    public string AssetName { get; init; } = string.Empty;

    /// <summary>حجم فایل APK (بایت)</summary>
    public long SizeBytes { get; init; }

    /// <summary>آدرس صفحه Release در GitHub</summary>
    public string HtmlUrl { get; init; } = string.Empty;
}

/// <summary>
/// سرویس به‌روزرسانی خودکار برنامه از طریق GitHub Releases.
/// برای انتشار نسخه جدید: یک Release با تگ «vX.Y.Z» بسازید و فایل APK را
/// به‌عنوان Asset آپلود کنید — راهنمای کامل در فایل UPDATE.md ریشه مخزن.
/// </summary>
public class UpdateService
{
    private static readonly HttpClient Client = CreateHttpClient();

    /// <summary>نسخه نصب‌شده روی دستگاه (از csproj - ApplicationDisplayVersion)</summary>
    public static string CurrentVersion => AppInfo.VersionString;

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        // اعمال پروکسی سیستم (وای‌فای) در صورت وجود — مرورگر دستگاه از آن استفاده می‌کند
        HttpProxyHelper.EnsureConfigured();

        // GitHub بدون User-Agent درخواست‌ها را رد می‌کند
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Amard-BillPayment-Updater");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    /// <summary>
    /// آخرین Release (نسخه‌های pre-release و draft دیده نمی‌شوند) را بررسی و
    /// با نسخه نصب‌شده مقایسه می‌کند.
    /// </summary>
    /// <exception cref="InvalidOperationException">در خطای شبکه، rate limit یا نبود فایل APK</exception>
    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default)
    {
        var owner = AppSettingsService.UpdateRepoOwner.Trim();
        var repo = AppSettingsService.UpdateRepoName.Trim();
        var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var token = AppSettingsService.UpdateGitHubToken;
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());

        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            if ((int)response.StatusCode == 404)
                throw new InvalidOperationException(
                    "هیچ نسخه‌ای در GitHub منتشر نشده است (Release با تگ vX.Y.Z بسازید).");

            if ((int)response.StatusCode == 403)
                throw new InvalidOperationException(
                    "GitHub محدودیت تعداد درخواست (rate limit) اعمال کرده است. " +
                    "می‌توانید یک Personal Access Token در صفحه تنظیمات وارد کنید.");

            throw new InvalidOperationException($"بررسی نسخه جدید ناموفق بود (کد {(int)response.StatusCode}).");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;

        var tagName = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() ?? string.Empty : string.Empty;
        var latestVersion = NormalizeVersion(tagName);
        var currentVersion = NormalizeVersion(CurrentVersion);

        var notes = root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() ?? string.Empty : string.Empty;
        var htmlUrl = root.TryGetProperty("html_url", out var htmlEl) ? htmlEl.GetString() ?? string.Empty : string.Empty;

        // یافتن فایل APK در Assets — اگر چند APK بود، بزرگ‌ترین (کامل‌ترین) انتخاب می‌شود
        string assetName = string.Empty, downloadUrl = string.Empty;
        long sizeBytes = 0;

        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty;
                if (!name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                    continue;

                var assetSize = asset.TryGetProperty("size", out var sizeEl) ? sizeEl.GetInt64() : 0;
                if (assetSize >= sizeBytes)
                {
                    sizeBytes = assetSize;
                    assetName = name;
                    downloadUrl = asset.TryGetProperty("browser_download_url", out var urlEl)
                        ? urlEl.GetString() ?? string.Empty : string.Empty;
                }
            }
        }

        if (string.IsNullOrEmpty(downloadUrl))
            throw new InvalidOperationException("فایل APK در آخرین Release یافت نشد. یک فایل .apk به Release اضافه کنید.");

        return new UpdateCheckResult
        {
            UpdateAvailable = CompareVersions(latestVersion, currentVersion) > 0,
            LatestVersion = latestVersion,
            CurrentVersion = currentVersion,
            ReleaseNotes = notes,
            DownloadUrl = downloadUrl,
            AssetName = assetName,
            SizeBytes = sizeBytes,
            HtmlUrl = htmlUrl
        };
    }

    /// <summary>
    /// فایل APK جدید را دانلود کرده و مسیر آن را برمی‌گرداند (در cache برنامه ذخیره می‌شود).
    /// </summary>
    public async Task<string> DownloadApkAsync(UpdateCheckResult update, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var cacheDir = FileSystem.CacheDirectory;
        if (string.IsNullOrEmpty(cacheDir))
            throw new InvalidOperationException("مسیر cache برنامه در دسترس نیست.");

        var fileName = SanitizeFileName(string.IsNullOrWhiteSpace(update.AssetName)
            ? $"Amard-{update.LatestVersion}.apk"
            : update.AssetName);

        var filePath = Path.Combine(cacheDir, fileName);

        using var request = new HttpRequestMessage(HttpMethod.Get, update.DownloadUrl);
        var token = AppSettingsService.UpdateGitHubToken;
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());

        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? update.SizeBytes;
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        await using var output = File.Create(filePath);

        var buffer = new byte[81920];
        long downloaded = 0;
        int bytesRead;

        while ((bytesRead = await input.ReadAsync(buffer, ct)) > 0)
        {
            await output.WriteAsync(buffer, 0, bytesRead, ct);
            downloaded += bytesRead;
            if (totalBytes > 0)
                progress?.Report(Math.Min(1.0, (double)downloaded / totalBytes));
        }

        progress?.Report(1.0);
        return filePath;
    }

    /// <summary>
    /// آیا این برنامه اجازه نصب بسته‌ها (نصب از منابع ناشناس) را دارد؟
    /// در اندروید 8+ کاربر باید در تنظیمات سیستم مجوز بدهد.
    /// </summary>
    public static bool CanInstallPackages()
    {
        var context = GetContext();
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            return context.PackageManager?.CanRequestPackageInstalls() ?? false;

        return true; // قبل از API 26 مجوز جداگانه لازم نیست
    }

    /// <summary>باز کردن صفحه تنظیمات سیستم برای دادن مجوز «نصب برنامه‌های ناشناس»</summary>
    public static void OpenInstallPermissionSettings()
    {
        var context = GetContext();
        var intent = new Intent();

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            intent.SetAction(Android.Provider.Settings.ActionManageUnknownAppSources);
        else
            intent.SetAction(Android.Provider.Settings.ActionApplicationDetailsSettings);

        intent.SetData(Android.Net.Uri.Parse("package:" + context.PackageName));
        intent.AddFlags(Android.Content.ActivityFlags.NewTask);
        context.StartActivity(intent);
    }

    /// <summary>
    /// نصب APK دانلودشده — نصاب اندروید را با فایل باز می‌کند.
    /// فایل باید از طریق FileProvider به اشتراک گذاشته شود (اندروید 7+).
    /// </summary>
    public static void InstallApk(string filePath)
    {
        var context = GetContext();
        var file = new Java.IO.File(filePath);

        var intent = new Intent(Intent.ActionView);
        intent.SetDataAndType(GetShareableUri(context, file), "application/vnd.android.package-archive");
        intent.AddFlags(Android.Content.ActivityFlags.GrantReadUriPermission);
        intent.AddFlags(Android.Content.ActivityFlags.NewTask | Android.Content.ActivityFlags.ClearTop);
        context.StartActivity(intent);
    }

    /// <summary>ساخت content:// URI امن برای فایل APK از طریق FileProvider</summary>
    private static Android.Net.Uri GetShareableUri(Android.Content.Context context, Java.IO.File file)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(24))
        {
            var authority = context.PackageName + ".update.fileProvider";
            return AndroidX.Core.Content.FileProvider.GetUriForFile(context, authority, file);
        }

        return Android.Net.Uri.FromFile(file)!;
    }

    private static Android.Content.Context GetContext()
        => Platform.CurrentActivity
           ?? (Android.Content.Context)global::Android.App.Application.Context;

    /// <summary>حذف «v» ابتدای تگ و فاصله‌ها (v1.2.0 → 1.2.0)</summary>
    private static string NormalizeVersion(string? version)
        => (version ?? string.Empty).Trim().TrimStart('v', 'V').Trim();

    /// <summary>مقایسه نسخه‌ها: &gt;0 یعنی a جدیدتر است</summary>
    private static int CompareVersions(string a, string b)
    {
        if (Version.TryParse(a, out var va) && Version.TryParse(b, out var vb))
            return va.CompareTo(vb);

        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
