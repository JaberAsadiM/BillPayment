using Amard.App.Models;
using Amard.App.ViewModels;

namespace Amard.App.Views;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _vm;

    public MainPage()
    {
        InitializeComponent();
        BindingContext = _vm = new MainViewModel();

        // پر کردن نوع جستجو پس از ساخته شدن ViewModel (تا SelectedIndexChanged کرش نکند)
        SearchTypePicker.ItemsSource = new List<string> { "نام مالک", "کد ملی", "کد نوسازی" };
        SearchTypePicker.SelectedIndex = 0;
    }

    private void OnSearchTypeChanged(object? sender, EventArgs e)
    {
        if (_vm is null || SearchTypePicker.SelectedIndex < 0)
            return;

        _vm.SearchType = (BillSearchType)SearchTypePicker.SelectedIndex;
    }

    /// <summary>باز کردن صفحه‌ی تنظیمات برنامه</summary>
    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        try
        {
            await Navigation.PushAsync(new SettingsPage());
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("AmardCrash", $"Open settings FAILED: {ex}");
            await DisplayAlert("خطا", ex.Message, "باشه");
        }
    }
}
