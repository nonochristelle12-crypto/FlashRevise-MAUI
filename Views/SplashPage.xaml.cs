namespace FlashRevise.Views;

public partial class SplashPage : ContentPage
{
    public SplashPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        LogoMark.Opacity = 0;
        LogoMark.Scale = 0.85;

        await Task.WhenAll(
            LogoMark.FadeToAsync(1, 420, Easing.CubicOut),
            LogoMark.ScaleToAsync(1, 520, Easing.SpringOut));
    }
}
