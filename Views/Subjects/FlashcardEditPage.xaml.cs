using FlashRevise.ViewModels;

namespace FlashRevise.Views.Subjects;

public partial class FlashcardEditPage : ContentPage
{
    public FlashcardEditPage(FlashcardEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
