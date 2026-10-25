using FlashRevise.ViewModels;

namespace FlashRevise.Views.Settings;

public partial class AchievementsPage : ContentPage
{
    public AchievementsPage(AchievementsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is AchievementsViewModel viewModel)
        {
            viewModel.LoadCommand.Execute(null);
        }
    }
}
