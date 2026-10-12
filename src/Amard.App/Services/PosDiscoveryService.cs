using Android.Content;
using Android.Content.PM;

namespace Amard.App.Services;

/// <summary>
/// یک سرویس پرداخت یافت‌شده روی دستگاه (پکیج + کلاس + اکشنی که به آن پاسخ می‌دهد).
/// </summary>
public sealed record PosServiceCandidate(
    string PackageName,
    string ClassName,
    string? Action,
    string Source);

/// <summary>
/// کشف خودکار سرویس پرداخت پایانه روی دستگاه.
/// مشکل: نام پکیج/اکشن سرویس روی دستگاه‌های مختلف فرق می‌کند و حدس زدن
/// (مثل «posservis» یا حتی پیش‌فرض) جواب نمی‌دهد؛ در حالی که اپ تست خودِ
/// آسان‌پرداخت روی همان دستگاه کار می‌کند، یعنی سرویس هست و فقط باید پیدایش کرد.
/// این سرویس دو کار می‌کند:
///  ۱) پرس‌وجوی سیستمی سرویس‌هایی که به اکشن‌های شناخته‌شده پاسخ می‌دهند؛
///  ۲) اسکن همه برنامه‌های نصب‌شده و بررسی سرویس‌های exported آن‌ها.
/// </summary>
public static class PosDiscoveryService
{
    /// <summary>اکشن‌های شناخته‌شده سرویس پرداخت (به ترتیب اولویت)</summary>
    public static IReadOnlyList<string> KnownActions => new[]
    {
        AppSettingsService.DefaultPosServiceAction,   // com.persianswitch.smartpos.service
        AppSettingsService.LegacyPosServiceAction,    // نام اینترفیس AIDL (نسخه‌های قدیمی)
        "com.persianswitch.smartpos.IPosService",
        "com.persianswitch.smartpos.POS_SERVICE",
        "ir.asanpardakht.smartpos.service",
    };

    /// <summary>کلیدواژه‌های نام پکیج برنامه پرداخت (حساس به حروف کوچک/بزرگ نیست)</summary>
    private static readonly string[] PackageKeywords =
    {
        "smartpos", "persianswitch", "pardakht", "asanpardakht", "asan",
        "payment", "terminal", "psp", "shaparak",
    };

    /// <summary>کلیدواژه‌های نام کلاس سرویس پرداخت</summary>
    private static readonly string[] ServiceClassKeywords =
    {
        "smartpos", "persianswitch", "posservice", "paymentservice",
        "applicationervice", "pos", "payment",
    };

    private static Context CurrentContext =>
        Microsoft.Maui.ApplicationModel.Platform.CurrentActivity
        ?? (Context)global::Android.App.Application.Context;

    /// <summary>
    /// اجرای کامل کشف؛ خروجی بدون تکرار، با اولویت یافته‌های اکشن.
    /// ممکن است کمی طول بکشد (اسکن پکیج‌ها)؛ در ترد پس‌زمینه صدا بزنید.
    /// </summary>
    public static List<PosServiceCandidate> Discover()
    {
        var found = new List<PosServiceCandidate>();

        found.AddRange(DiscoverByActions());
        found.AddRange(DiscoverByPackageScan());

        return found
            .GroupBy(c => (c.PackageName, c.ClassName))
            .Select(g => g.OrderBy(c => c.Action is null ? 1 : 0).First())
            .OrderBy(c => c.Action is null ? 1 : 0)
            .ThenBy(c => c.PackageName)
            .ToList();
    }

    /// <summary>پرس‌وجوی سیستمی: کدام سرویس به اکشن‌های شناخته‌شده پاسخ می‌دهد؟</summary>
    public static List<PosServiceCandidate> DiscoverByActions()
    {
        var found = new List<PosServiceCandidate>();
        var pm = CurrentContext.PackageManager;
        if (pm is null)
            return found;

        var actions = KnownActions
            .Append(AppSettingsService.PosServiceAction)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var action in actions)
        {
            try
            {
                var services = pm.QueryIntentServices(
                    new Intent(action),
                    PackageInfoFlags.MatchAll);

                if (services is null)
                    continue;

                foreach (var ri in services)
                {
                    var si = ri?.ServiceInfo;
                    if (si?.PackageName is { } pkg && si.Name is { } cls)
                        found.Add(new PosServiceCandidate(pkg, cls, action, "action"));
                }
            }
            catch
            {
                // اکشن بعدی
            }
        }

        return found;
    }

    /// <summary>
    /// اسکن همه برنامه‌های نصب‌شده: پکیج‌هایی که نامشان شبیه برنامه پرداخت است،
    /// به‌علاوه سرویس‌های exported که نامشان شبیه سرویس پرداخت است.
    /// (در اندروید ۱۰ مثل دستگاه شما لیست کامل برمی‌گردد؛ در ۱۱+ به مجوز
    /// QUERY_ALL_PACKAGES نیاز دارد که در منیفست اضافه شده است.)
    /// </summary>
    public static List<PosServiceCandidate> DiscoverByPackageScan()
    {
        var found = new List<PosServiceCandidate>();
        var pm = CurrentContext.PackageManager;
        if (pm is null)
            return found;

        IList<PackageInfo>? packages;
        try
        {
#pragma warning disable CS0618 // در APIهای جدید منسوخ شده ولی روی دستگاه هدف لازم است
            packages = pm.GetInstalledPackages(PackageInfoFlags.Services);
#pragma warning restore CS0618
        }
        catch
        {
            return found;
        }

        if (packages is null)
            return found;

        foreach (var pkg in packages)
        {
            var packageName = pkg?.PackageName;
            if (string.IsNullOrWhiteSpace(packageName))
                continue;

            bool packageMatch = PackageKeywords.Any(k =>
                packageName.Contains(k, StringComparison.OrdinalIgnoreCase));

            var services = pkg?.Services;
            if (services is null)
                continue;

            foreach (var svc in services)
            {
                var className = svc?.Name;
                if (string.IsNullOrWhiteSpace(className))
                    continue;

                bool classMatch = ServiceClassKeywords.Any(k =>
                    className.Contains(k, StringComparison.OrdinalIgnoreCase));

                // فقط exportedها یا مواردی که نامشان явно شبیه است
                if ((packageMatch || classMatch) && (svc!.Exported || packageMatch || classMatch))
                    found.Add(new PosServiceCandidate(packageName, className, null, "package-scan"));
            }
        }

        return found;
    }

    /// <summary>آیا این مقدار اصلاً می‌تواند نام پکیج باشد؟ (مثل «posservis» بدون نقطه، نه)</summary>
    public static bool LooksLikePackageName(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains('.');
}
