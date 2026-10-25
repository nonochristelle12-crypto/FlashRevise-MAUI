using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Services;
using FlashRevise.Services.Auth;

namespace FlashRevise.ViewModels;

/// <summary>Connexion à un compte FlashRevise existant.</summary>
public partial class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _auth;
    private readonly IStatsService _stats;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ServerLabel { get; set; } = string.Empty;

    public LoginViewModel(IAuthService auth, IStatsService stats)
    {
        _auth = auth;
        _stats = stats;
        Title = "Connexion";
    }

    [RelayCommand]
    private void Load()
    {
        ServerLabel = _auth.BaseUrl;
        ClearError();
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ClearError();

        if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@'))
        {
            ErrorMessage = "Saisis une adresse e-mail valide.";
            return;
        }

        if (string.IsNullOrEmpty(Password))
        {
            ErrorMessage = "Saisis ton mot de passe.";
            return;
        }

        IsBusy = true;
        try
        {
            var session = await _auth.LoginAsync(Email.Trim(), Password);
            await ApplyDisplayNameAsync(session.User.DisplayName);
            Password = string.Empty;
            await Shell.Current.GoToAsync(Routes.Main);
        }
        catch (AuthException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            ErrorMessage = "Connexion impossible. Vérifie le réseau et l'adresse du serveur.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ApplyDisplayNameAsync(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        var profile = await _stats.GetProfileAsync();
        profile.Name = displayName.Trim();
        await _stats.SaveProfileAsync(profile);
    }

    [RelayCommand]
    private static Task OpenRegisterAsync() => Shell.Current.GoToAsync(Routes.Register);

    [RelayCommand]
    private static Task OpenForgotPasswordAsync() => Shell.Current.GoToAsync(Routes.ForgotPassword);

    [RelayCommand]
    private static Task ContinueAsGuestAsync() => Shell.Current.GoToAsync("..");

    [RelayCommand]
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
