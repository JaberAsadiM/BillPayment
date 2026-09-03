using Microsoft.Extensions.Logging;

namespace Amard.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>();

        // نمایش خطاهای مدیریت‌نشده روی صفحه برای سهولت عیب‌یابی
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogCrash("AppDomain", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogCrash("Task", e.Exception);
            e.SetObserved();
        };

        var app = builder.Build();
        return app;
    }

    /// <summary>خطا را در کنسول لاگ می‌کند و یک Alert روی صفحه نشان می‌دهد.</summary>
    public static void LogCrash(string source, Exception? ex)
    {
        System.Diagnostics.Debug.WriteLine($"[{source}] CRASH: {ex}");
        Android.Util.Log.Error("AmardCrash", $"{source}: {ex}");

        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var page = Microsoft.Maui.Controls.Application.Current?.MainPage?.Navigation?.NavigationStack?.LastOrDefault()
                           ?? Microsoft.Maui.Controls.Application.Current?.MainPage;
                if (page is not null)
                    await page.DisplayAlert("خطا", ex?.Message ?? "خطای ناشناخته", "باشه");
            }
            catch { /* ignore */ }
        });
    }
}
