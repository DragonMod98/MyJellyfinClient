using MyJellyfinClient.ViewModels;

namespace MyJellyfinClient.Views;

public partial class LibraryPage : ContentPage
{
    public LibraryPage(LibraryViewModel viewModel)
    {
        InitializeComponent();

        // Verbindet die UI mit dem ViewModel
        BindingContext = viewModel;
    }
}