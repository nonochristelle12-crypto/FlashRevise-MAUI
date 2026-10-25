using FlashRevise.ViewModels;

namespace FlashRevise.Views.Settings;

public partial class GoalsBooksPage : ContentPage
{
    private readonly GoalsBooksViewModel _viewModel;

    public GoalsBooksPage(GoalsBooksViewModel viewModel)
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

    private async void OnIncrementClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: BookRow row })
        {
            await _viewModel.IncrementCommand.ExecuteAsync(row);
        }
    }

    private async void OnDecrementClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: BookRow row })
        {
            await _viewModel.DecrementCommand.ExecuteAsync(row);
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: BookRow row })
        {
            return;
        }

        var confirmed = await DisplayAlertAsync("Retirer ce livre ?",
            "La progression enregistrée sera perdue.", "Retirer", "Annuler");

        if (confirmed)
        {
            await _viewModel.DeleteCommand.ExecuteAsync(row);
        }
    }
}
