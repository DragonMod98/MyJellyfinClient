using MyJellyfinClient.ViewModels;

namespace MyJellyfinClient.Views;

public partial class DownloadsPage : ContentPage
{
    private readonly DownloadsViewModel _viewModel;

    public DownloadsPage(DownloadsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = MyJellyfinClient.Services.ExitConfirmation.AskAsync(this);
        return true;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Lädt die Downloads frisch aus der Datenbank, sobald der Tab geöffnet wird
        _viewModel.LoadDownloadsCommand.Execute(null);
    }
}