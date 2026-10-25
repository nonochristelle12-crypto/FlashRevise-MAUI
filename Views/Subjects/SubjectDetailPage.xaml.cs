using FlashRevise.Models;
using FlashRevise.ViewModels;

namespace FlashRevise.Views.Subjects;

public partial class SubjectDetailPage : ContentPage
{
    private readonly SubjectDetailViewModel _viewModel;

    public SubjectDetailPage(SubjectDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.RefreshCommand.Execute(null);
    }

    private async void OnCardTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is Flashcard card)
        {
            await _viewModel.EditCardCommand.ExecuteAsync(card);
        }
    }

    private async void OnDeleteCardClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Flashcard card })
        {
            return;
        }

        var confirmed = await DisplayAlertAsync("Supprimer la carte ?",
            "La carte et son historique de révision seront supprimés.", "Supprimer", "Annuler");

        if (confirmed)
        {
            await _viewModel.DeleteCardCommand.ExecuteAsync(card);
        }
    }
}
