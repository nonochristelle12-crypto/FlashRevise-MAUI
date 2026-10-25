using FlashRevise.ViewModels;

namespace FlashRevise.Views.Revision;

public partial class RevisionSessionPage : ContentPage
{
    private readonly RevisionSessionViewModel _viewModel;

    public RevisionSessionPage(RevisionSessionViewModel viewModel)
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

    protected override void OnDisappearing()
    {
        _viewModel.Stop();
        base.OnDisappearing();
    }

    private async void OnQuitClicked(object? sender, EventArgs e)
    {
        var confirmed = await DisplayAlertAsync("Quitter la session ?",
            "Les cartes déjà révisées sont enregistrées, mais le bilan ne sera pas affiché.",
            "Quitter", "Continuer");

        if (confirmed)
        {
            await _viewModel.QuitCommand.ExecuteAsync(null);
        }
    }
}
