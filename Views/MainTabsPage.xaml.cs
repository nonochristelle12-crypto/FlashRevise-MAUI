using FlashRevise.ViewModels;

namespace FlashRevise.Views;

public partial class MainTabsPage : ContentPage
{
    private readonly HomeViewModel _home;
    private readonly SubjectsViewModel _subjects;
    private readonly StatsViewModel _stats;
    private readonly ProfileViewModel _profile;
    private int _selectedIndex = -1;

    public MainTabsPage(
        HomeViewModel home,
        SubjectsViewModel subjects,
        StatsViewModel stats,
        ProfileViewModel profile)
    {
        InitializeComponent();

        _home = home;
        _subjects = subjects;
        _stats = stats;
        _profile = profile;

        HomeTab.BindingContext = home;
        SubjectsTab.BindingContext = subjects;
        StatsTab.BindingContext = stats;
        ProfileTab.BindingContext = profile;

        SelectTab(0);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = LoadCurrentTabAsync();
    }

    private async void OnHomeTabTapped(object? sender, TappedEventArgs e) => await SelectAndLoadAsync(0);

    private async void OnSubjectsTabTapped(object? sender, TappedEventArgs e) => await SelectAndLoadAsync(1);

    private async void OnStatsTabTapped(object? sender, TappedEventArgs e) => await SelectAndLoadAsync(2);

    private async void OnProfileTabTapped(object? sender, TappedEventArgs e) => await SelectAndLoadAsync(3);

    private async void OnReviseTapped(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync($"{Routes.RevisionConfig}?subjectId=0");

    private async Task SelectAndLoadAsync(int index)
    {
        if (_selectedIndex == index)
        {
            await LoadCurrentTabAsync();
            return;
        }

        SelectTab(index);
        await LoadCurrentTabAsync();
    }

    private async Task LoadCurrentTabAsync()
    {
        switch (_selectedIndex)
        {
            case 0:
                await _home.LoadCommand.ExecuteAsync(null);
                break;
            case 1:
                await _subjects.LoadCommand.ExecuteAsync(null);
                break;
            case 2:
                await _stats.LoadCommand.ExecuteAsync(null);
                break;
            case 3:
                await _profile.LoadCommand.ExecuteAsync(null);
                break;
        }
    }

    private void SelectTab(int index)
    {
        _selectedIndex = index;

        HomeTab.IsVisible = index == 0;
        SubjectsTab.IsVisible = index == 1;
        StatsTab.IsVisible = index == 2;
        ProfileTab.IsVisible = index == 3;

        ApplyState(HomeTabIcon, HomeTabLabel, index == 0);
        ApplyState(SubjectsTabIcon, SubjectsTabLabel, index == 1);
        ApplyState(StatsTabIcon, StatsTabLabel, index == 2);
        ApplyState(ProfileTabIcon, ProfileTabLabel, index == 3);
    }

    private void ApplyState(Label icon, Label caption, bool selected)
    {
        var color = SelectedColor();
        icon.TextColor = selected ? color : MutedColor();
        caption.TextColor = selected ? color : MutedColor();
        caption.FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None;
    }

    private static Color SelectedColor() =>
        Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#C9BEFF")
            : Color.FromArgb("#6C4CF1");

    private static Color MutedColor() =>
        Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#938EA1")
            : Color.FromArgb("#8A8FA6");
}
