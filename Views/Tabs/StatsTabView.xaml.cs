using FlashRevise.Models;
using FlashRevise.ViewModels;

namespace FlashRevise.Views.Tabs;

public partial class StatsTabView : ContentView
{
    public StatsTabView()
    {
        InitializeComponent();
    }

    private async void OnSubjectStatsTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is StatsViewModel vm && e.Parameter is SubjectStats subject)
        {
            await vm.OpenSubjectCommand.ExecuteAsync(subject);
        }
    }
}
