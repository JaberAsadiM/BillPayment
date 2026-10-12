using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace Amard.App.Services;

/// <summary>
/// اعتبارسنجی گواهی سرور با لنگر اعتماد داخلی اپ (ریشه Certum جاسازی‌شده).
/// پس‌زمینه: فروشگاه گواهی اندرویدهای قدیمی (مثل دستگاه POS با Android 10 که سال‌هاست
/// آپدیت نشده) ریشه جدید «Certum Trusted Root CA» را ندارد؛ در نتیجه دست‌دادن TLS
/// با خطای «Trust anchor for certification path not found» می‌شکند، در حالی که
/// مرورگر کروم (با فروشگاه ریشه‌های خودش) وصل می‌شود.
/// این ولیدیتور زنجیره را با ریشه داخلی می‌سازد و امضا، انقضا، نام هاست و لنگر را
/// بررسی می‌کند — برخلاف «return true» کور که راه را برای حمله مرد-میانی باز می‌کند.
/// </summary>
public static class CertificateValidator
{
    /// <summary>لنگر موردانتظار: ریشه Certum (باید دقیقاً همین باشد، نه هر ریشه دیگری)</summary>
    private const string ExpectedRootThumbprint = "C88344C018AE9FCCF187B78F22D1C5D74584BAE5";

    private static X509Certificate2? _root;
    private static X509Certificate2? _intermediate;

    /// <summary>ریشه Certum جاسازی‌شده در اپ (انقضا 2043)</summary>
    private static X509Certificate2 Root =>
        _root ??= LoadEmbedded("Amard.App.Resources.Certs.certum_root.cer");

    /// <summary>واسط Certum جاسازی‌شده در اپ</summary>
    private static X509Certificate2 Intermediate =>
        _intermediate ??= LoadEmbedded("Amard.App.Resources.Certs.certum_intermediate_r39.cer");

    private static X509Certificate2 LoadEmbedded(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"منبع گواهی داخلی یافت نشد: {resourceName}");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return X509CertificateLoader.LoadCertificate(ms.ToArray());
    }

    /// <summary>
    /// کال‌بک اعتبارسنجی برای SslClientAuthenticationOptions.
    /// اگر سیستم زنجیره را سالم بداند قبول است؛ در غیر این صورت (مثل دستگاه قدیمی
    /// که ریشه را نمی‌شناسد) زنجیره با لنگر داخلی بازسازی و بررسی می‌شود.
    /// </summary>
    public static bool Validate(
        object sender,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors errors)
    {
        // زنجیره سالم از نظر سیستم (سرورهای دیگر مثل github) → قبول
        if (errors == SslPolicyErrors.None)
            return true;

        if (certificate is null)
            return false;

        // نام هاست حتماً باید درست باشد؛ در غیر این صورت بدون چون‌وچرا رد
        if ((errors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
            return false;

        try
        {
            var leaf = X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());

            // گواهی منقضی یا هنوز معتبرنشده → رد
            if (leaf.NotAfter.ToUniversalTime() < DateTime.UtcNow ||
                leaf.NotBefore.ToUniversalTime() > DateTime.UtcNow)
                return false;

            // مسیر اول: لنگر اعتماد سفارشی (تمیزترین روش)
            if (TryCustomRootTrust(leaf))
                return true;

            // مسیر دوم (سازگاری بیشتر): زنجیره معمولی + چشم‌پوشی فقط از «ناشناس بودن
            // ریشه»، بعد کنترل دستی اینکه لنگر دقیقاً ریشه داخلی ماست
            if (TryUnknownAuthorityWithAnchorCheck(leaf))
                return true;

            return false;
        }
        catch
        {
            return false; // در هر تردیدی، بسته (fail-closed)
        }
    }

    /// <summary>ساخت زنجیره با ریشه داخلی به‌عنوان تنها لنگر اعتماد</summary>
    private static bool TryCustomRootTrust(X509Certificate2 leaf)
    {
        try
        {
            using var customChain = new X509Chain();
            customChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            customChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            customChain.ChainPolicy.CustomTrustStore.Add(Root);
            customChain.ChainPolicy.ExtraStore.Add(Root);
            customChain.ChainPolicy.ExtraStore.Add(Intermediate);

            if (!customChain.Build(leaf))
                return false;

            var anchor = customChain.ChainElements[customChain.ChainElements.Count - 1].Certificate;
            return string.Equals(anchor.Thumbprint, ExpectedRootThumbprint, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// ساخت زنجیره معمولی با واسط اضافه و قبول‌کردن «ریشه ناشناس»،
    /// به‌شرط اینکه لنگر نهایی دقیقاً ریشه داخلی ما باشد.
    /// </summary>
    private static bool TryUnknownAuthorityWithAnchorCheck(X509Certificate2 leaf)
    {
        try
        {
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.ExtraStore.Add(Intermediate);
            chain.ChainPolicy.ExtraStore.Add(Root);
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

            if (!chain.Build(leaf))
                return false;

            // هیچ خطایی جز «ریشه ناشناس» نباید باشد
            foreach (var element in chain.ChainElements)
            {
                foreach (var status in element.ChainElementStatus)
                {
                    if (status.Status is not (X509ChainStatusFlags.NoError or X509ChainStatusFlags.UntrustedRoot))
                        return false;
                }
            }

            var anchor = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
            return string.Equals(anchor.Thumbprint, ExpectedRootThumbprint, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
