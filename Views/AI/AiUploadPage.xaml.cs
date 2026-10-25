using FlashRevise.ViewModels;

namespace FlashRevise.Views.AI;

public partial class AiUploadPage : ContentPage
{
    private readonly AiUploadViewModel _viewModel;

    public AiUploadPage(AiUploadViewModel viewModel)
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
}
