using FlashRevise.Models;
using FlashRevise.ViewModels;

namespace FlashRevise.Views.Tabs;

public partial class HomeTabView : ContentView
{
    public HomeTabView()
    {
        InitializeComponent();
    }

    private async void OnSubjectCardTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is HomeViewModel vm && e.Parameter is SubjectStats subject)
        {
            await vm.OpenSubjectCommand.ExecuteAsync(subject);
        }
    }
}
