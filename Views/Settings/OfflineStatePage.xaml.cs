using FlashRevise.ViewModels;

namespace FlashRevise.Views.Settings;

public partial class OfflineStatePage : ContentPage
{
    private readonly OfflineStateViewModel _viewModel;

    public OfflineStatePage(OfflineStateViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }

    protected override void OnDisappearing()
    {
        _viewModel.Stop();
        base.OnDisappearing();
    }
}
