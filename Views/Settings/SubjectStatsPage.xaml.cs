using FlashRevise.ViewModels;

namespace FlashRevise.Views.Settings;

public partial class SubjectStatsPage : ContentPage
{
    public SubjectStatsPage(SubjectStatsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
