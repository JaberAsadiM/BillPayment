namespace Amard.App;

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
            return new Window(new NavigationPage(new Views.MainPage()));
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
}
