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