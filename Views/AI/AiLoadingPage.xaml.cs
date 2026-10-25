using FlashRevise.ViewModels;

namespace FlashRevise.Views.AI;

public partial class AiLoadingPage : ContentPage
{
    private readonly AiLoadingViewModel _viewModel;

    public AiLoadingPage(AiLoadingViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Start();
    }
}
