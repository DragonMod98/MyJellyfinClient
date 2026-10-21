using MyJellyfinClient.ViewModels;

namespace MyJellyfinClient.Views;

public partial class SettingsPage : ContentPage
{
	public SettingsPage(SettingsViewModel viewModel)
	{
		InitializeComponent();

        BindingContext = viewModel;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = MyJellyfinClient.Services.ExitConfirmation.AskAsync(this);
        return true;
    }
}