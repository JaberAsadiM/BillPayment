using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Microsoft.Maui;

namespace Amard.App.Platforms.Android;

[Activity(
    Theme = "@style/Amard.MainTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
        ConfigChanges.UiMode | ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    public const int PaymentRequestCode = 7501;

    /// <summary>
    /// نتیجه تراکنش POS که بعد از بسته شدن Activity پرداخت برمی‌گردد.
    /// پارامتر دوم بیانگر آن است که resultCode برابر RESULT_OK بوده است
    /// (طبق مستندات، Intent دریافتی تنها در این حالت معتبر است).
    /// </summary>
    public static event Action<Intent?, bool>? PaymentResultReceived;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // کرش‌های جاوا را به‌جای بسته شدن بی‌صدا، روی صفحه نمایش بده
        global::Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
            MauiProgram.LogCrash("Android", e.Exception);

        base.OnCreate(savedInstanceState);
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode == PaymentRequestCode)
        {
            // طبق مستندات فقط در صورتی Intent معتبر است که resultCode برابر RESULT_OK باشد
            PaymentResultReceived?.Invoke(data, resultCode == Result.Ok);
        }
    }
}
