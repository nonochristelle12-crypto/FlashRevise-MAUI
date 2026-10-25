using FlashRevise.ViewModels;

namespace FlashRevise.Views.Revision;

public partial class RevisionConfigPage : ContentPage
{
    public RevisionConfigPage(RevisionConfigViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
