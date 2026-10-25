using FlashRevise.ViewModels;

namespace FlashRevise.Views.Settings;

public partial class RemindersPage : ContentPage
{
    private readonly RemindersViewModel _viewModel;

    public RemindersPage(RemindersViewModel viewModel)
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

    private async void OnReminderTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is ReminderRow row)
        {
            await _viewModel.EditCommand.ExecuteAsync(row);
        }
    }

    private async void OnReminderToggled(object? sender, ToggledEventArgs e)
    {
        if (sender is Switch { BindingContext: ReminderRow row })
        {
            await _viewModel.SetEnabledAsync(row, e.Value);
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: ReminderRow row })
        {
            return;
        }

        var confirmed = await DisplayAlertAsync("Supprimer ce rappel ?",
            "La notification ne sera plus programmée.", "Supprimer", "Annuler");

        if (confirmed)
        {
            await _viewModel.DeleteCommand.ExecuteAsync(row);
        }
    }
}
