namespace Amard.App;

using Amard.App.Services;

public partial class App : Application
{
    public App()
    {
        Android.Util.Log.Info("AmardCrash", "App ctor: starting");
        InitializeComponent();
        // قفل کردن تم روشن تا رنگ‌های پیش‌فرضِ حالت تاریکِ سیستم
        // (متن سفید Entry/Picker روی زمینه روشن) اعمال نشود
        UserAppTheme = AppTheme.Light;
        Android.Util.Log.Info("AmardCrash", "App ctor: done");
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        try
        {
            var window = new Window(new NavigationPage(new Views.MainPage()));
            // بررسی خودکار نسخه جدید (یک‌بار، بعد از باز شدن پنجره)
            window.Activated += OnWindowActivatedForUpdateCheck;
            return window;
        }
        catch (Exception ex)
        {
            // به‌جای صفحهٔ سفیدِ بی‌خطا، متن خطا را روی صفحه نشان بده
            Android.Util.Log.Error("AmardCrash", $"CreateWindow FAILED: {ex}");
            System.Diagnostics.Debug.WriteLine($"[CreateWindow] CRASH: {ex}");
            return new Window(new ContentPage
            {
                BackgroundColor = Colors.White,
                Content = new ScrollView
                {
                    Content = new Label
                    {
                        Text = ex.ToString(),
                        TextColor = Colors.Red
                    }
                }
            });
        }
    }

    /// <summary>
    /// چند ثانیه بعد از شروع برنامه، نسخه جدید را از GitHub بررسی می‌کند؛
    /// اگر نسخه جدید موجود باشد، به کاربر اطلاع می‌دهد و به صفحه تنظیمات هدایت می‌کند.
    /// </summary>
    private async void OnWindowActivatedForUpdateCheck(object? sender, EventArgs e)
    {
        var window = (Window)sender!;
        window.Activated -= OnWindowActivatedForUpdateCheck;

        // کمی صبر تا صفحه اصلی کامل بارگذاری شود
        await Task.Delay(3000);

        try
        {
            if (!AppSettingsService.UpdateCheckOnStartup)
                return;

            var update = await new Services.UpdateService().CheckForUpdateAsync();
            if (!update.UpdateAvailable)
                return;

            var page = Microsoft.Maui.Controls.Application.Current?.MainPage;
            if (page is null)
                return;

            var goToSettings = await page.DisplayAlert(
                "🔄 به‌روزرسانی جدید",
                $"نسخه {update.LatestVersion} منتشر شده است (نسخه فعلی: {update.CurrentVersion}).\n" +
                "برای دانلود و نصب، به صفحه تنظیمات بروید.",
                "برو به تنظیمات", "بعداً");

            if (goToSettings && page.Navigation is not null)
                await page.Navigation.PushAsync(new Views.SettingsPage());
        }
        catch
        {
            // خطای بررسی نسخه هنگام شروع، برای کاربر مزاحمت ایجاد نکند
        }
    }
}
