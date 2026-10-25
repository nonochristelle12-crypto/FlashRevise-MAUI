using FlashRevise.ViewModels;

namespace FlashRevise.Views.AI;

public partial class AiReviewPage : ContentPage
{
    private readonly AiReviewViewModel _viewModel;

    public AiReviewPage(AiReviewViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Load();
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: AiReviewItem item })
        {
            return;
        }

        var confirmed = await DisplayAlertAsync("Retirer cette carte ?",
            "Elle ne sera pas enregistrée dans la matière.", "Retirer", "Annuler");

        if (confirmed)
        {
            _viewModel.RemoveCommand.Execute(item);
        }
    }
}
