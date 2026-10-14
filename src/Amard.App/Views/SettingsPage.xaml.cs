using Amard.App.ViewModels;

namespace Amard.App.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        BindingContext = new SettingsViewModel();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        (BindingContext as ViewModels.SettingsViewModel)?.CheckOnAppearing();
    }
}