using Android.App;
using Android.Runtime;

namespace Amard.App.Platforms.Android;

/// <summary>
/// کلاس Application برنامه — نقطهٔ راه‌اندازی MAUI.
/// بدون این کلاس، <see cref="MauiProgram.CreateMauiApp"/> هرگز فراخوانی نمی‌شود
/// و اپ فقط یک صفحهٔ سفید (پس‌زمینهٔ SplashTheme) نمایش می‌دهد.
/// </summary>
[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
