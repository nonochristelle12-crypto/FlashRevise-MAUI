using FlashRevise.Models;
using FlashRevise.ViewModels;

namespace FlashRevise.Views.Tabs;

public partial class SubjectsTabView : ContentView
{
    public SubjectsTabView()
    {
        InitializeComponent();
    }

    private async void OnSubjectTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is SubjectsViewModel vm && e.Parameter is SubjectStats subject)
        {
            await vm.OpenSubjectCommand.ExecuteAsync(subject);
        }
    }
}
