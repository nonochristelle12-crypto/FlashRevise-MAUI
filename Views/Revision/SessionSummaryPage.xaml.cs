using FlashRevise.ViewModels;

namespace FlashRevise.Views.Revision;

public partial class SessionSummaryPage : ContentPage
{
    public SessionSummaryPage(SessionSummaryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
