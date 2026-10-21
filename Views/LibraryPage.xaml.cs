using MyJellyfinClient.ViewModels;

namespace MyJellyfinClient.Views;

public partial class LibraryPage : ContentPage
{
    private readonly LibraryViewModel _viewModel;

    public LibraryPage(LibraryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    // Zurück-Geste/-Taste: erst im Ordnerbaum nach oben, nicht die App schließen
    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.IsInSubFolder)
        {
            _viewModel.GoBackCommand.Execute(null);
            return true; // Ereignis ist behandelt
        }

        // Im Root-Ordner: Nachfrage, ob die App geschlossen werden soll
        _ = MyJellyfinClient.Services.ExitConfirmation.AskAsync(this);
        return true;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Filme laden, sobald der Tab angeklickt wird 
        // (verhindert erneutes Laden, wenn bereits Daten da sind)
        if (!_viewModel.LibraryItems.Any())
        {
            _viewModel.LoadLibraryCommand.Execute(null);
        }
    }
}