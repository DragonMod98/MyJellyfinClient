using MyJellyfinClient.ViewModels;

namespace MyJellyfinClient.Views;

public partial class MediaDetailPage : ContentPage
{
	public MediaDetailPage(MediaDetailViewModel viewModel)
	{
		InitializeComponent();
        BindingContext = viewModel;
	}

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is MediaDetailViewModel vm)
        {
            await vm.FetchDownloadOptionsAsync();
        }
    }
}