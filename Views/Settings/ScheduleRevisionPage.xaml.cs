using FlashRevise.ViewModels;

namespace FlashRevise.Views.Settings;

public partial class ScheduleRevisionPage : ContentPage
{
    public ScheduleRevisionPage(ScheduleRevisionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
