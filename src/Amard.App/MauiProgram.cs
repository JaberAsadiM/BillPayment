using Microsoft.Extensions.Logging;

namespace Amard.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>();

        // رنگ نوار ابزار (Toolbar):
        // در .NET 10 پراپرتی‌های NavigationPage.BarBackgroundColor / BarTextColor حذف شده‌اند،
        // پس به‌جای آن‌ها ToolbarHandler را سفارشی می‌کنیم.
        // پس‌زمینه بنفشِ ملایم با عنوان سرمه‌ای تا متن آیتم‌های نوار (مثل «⚙ تنظیمات») خوانا بماند.
        Microsoft.Maui.Handlers.ToolbarHandler.Mapper.AppendToMapping("AmardToolbarColors", (handler, view) =>
        {
            var toolbar = handler.PlatformView;
            toolbar.SetBackgroundColor(Android.Graphics.Color.ParseColor("#E8EAF6"));
            toolbar.SetTitleTextColor(Android.Graphics.Color.ParseColor("#1A237E"));
            toolbar.SetSubtitleTextColor(Android.Graphics.Color.ParseColor("#1A237E"));

            // رنگ متن آیتم‌های منوی Toolbar (مثل «⚙ تنظیمات»):
            // منو با هر ناوبری بین صفحات دوباره ساخته می‌شود و رنگ آن را تم
            // Material3 (سفید) تعیین می‌کند؛ پس یک Listener می‌بندیم که در هر
            // layout pass رنگ آیتم‌های منو را دوباره اعمال کند (idempotent است).
            AttachMenuColorizer(toolbar);
        });

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

    /// <summary>رنگ آیتم‌های منوی Toolbarها</summary>
    private static readonly Android.Graphics.Color MenuTextColor = Android.Graphics.Color.ParseColor("#1A237E");

    /// <summary>Toolbarهایی که Colorizer به آن‌ها وصل شده (برای جلوگیری از اتصال تکراری)</summary>
    private static readonly HashSet<long> _colorizedToolbars = new();

    /// <summary>
    /// یک listener به Toolbar وصل می‌کند که در هر layout pass، رنگ متن آیتم‌های
    /// منو را دوباره اعمال می‌کند. چون Toolbar و منوی آن با هر ناوبری بین صفحات
    /// بازسازی می‌شوند و رنگشان را از تم Material3 (سفید) می‌گیرند، این تنها
    /// راه حفظ رنگ ثابت است.
    /// </summary>
    private static void AttachMenuColorizer(AndroidX.AppCompat.Widget.Toolbar toolbar)
    {
        lock (_colorizedToolbars)
        {
            if (!_colorizedToolbars.Add(toolbar.Handle))
                return;
        }

        var colorizer = new ToolbarMenuColorizer();
        toolbar.AddOnAttachStateChangeListener(colorizer);

        // اگر Toolbar از قبل attach شده باشد، رویداد attach دیگر fire نمی‌شود؛
        // پس مستقیماً GlobalLayoutListener را ثبت می‌کنیم.
        if (toolbar.IsAttachedToWindow)
            colorizer.OnViewAttachedToWindow(toolbar);
    }

    /// <summary>
    /// با اتصال Toolbar به پنجره، GlobalLayoutListener ثبت می‌کند و در هر layout
    /// رنگ آیتم‌های منو را (در صورت نیاز) سرمه‌ای می‌کند؛ با جداشدن، unregister می‌شود.
    /// </summary>
    private sealed class ToolbarMenuColorizer : Java.Lang.Object,
        Android.Views.View.IOnAttachStateChangeListener,
        Android.Views.ViewTreeObserver.IOnGlobalLayoutListener
    {
        private AndroidX.AppCompat.Widget.Toolbar? _toolbar;

        public void OnViewAttachedToWindow(Android.Views.View attachedView)
        {
            _toolbar = attachedView as AndroidX.AppCompat.Widget.Toolbar;
            _toolbar?.ViewTreeObserver?.AddOnGlobalLayoutListener(this);
        }

        public void OnViewDetachedFromWindow(Android.Views.View detachedView)
        {
            _toolbar?.ViewTreeObserver?.RemoveOnGlobalLayoutListener(this);
            _toolbar = null;
        }

        public void OnGlobalLayout()
        {
            if (_toolbar is null)
                return;

            for (int i = 0; i < _toolbar.ChildCount; i++)
            {
                if (_toolbar.GetChildAt(i) is not AndroidX.AppCompat.Widget.ActionMenuView menuView)
                    continue;

                for (int j = 0; j < menuView.ChildCount; j++)
                {
                    // هر آیتم منو یک ViewGroup است که TextView داخلش دارد
                    if (menuView.GetChildAt(j) is Android.Views.ViewGroup itemView)
                    {
                        for (int k = 0; k < itemView.ChildCount; k++)
                        {
                            if (itemView.GetChildAt(k) is Android.Widget.TextView itemText &&
                                itemText.CurrentTextColor != MenuTextColor)
                            {
                                itemText.SetTextColor(MenuTextColor);
                            }
                        }
                    }
                    else if (menuView.GetChildAt(j) is Android.Widget.TextView directText &&
                             directText.CurrentTextColor != MenuTextColor)
                    {
                        directText.SetTextColor(MenuTextColor);
                    }
                }
            }
        }
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
