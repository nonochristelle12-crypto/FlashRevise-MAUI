using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Models;
using FlashRevise.Services;
using FlashRevise.Services.Auth;

namespace FlashRevise.ViewModels;

/// <summary>Création d'un compte via POST /v1/auth/register.</summary>
public partial class RegisterViewModel : BaseViewModel
{
    private readonly IAuthService _auth;
    private readonly IStatsService _stats;

    [ObservableProperty]
    public partial string DisplayName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ConfirmPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StudyLevel { get; set; } = "Licence";

    [ObservableProperty]
    public partial bool ConsentTerms { get; set; }

    public IReadOnlyList<string> StudyLevels { get; } =
        ["Lycée", "Prépa", "Licence", "Master", "Doctorat", "Concours", "Autre"];

    public RegisterViewModel(IAuthService auth, IStatsService stats)
    {
        _auth = auth;
        _stats = stats;
        Title = "Créer un compte";
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ClearError();

        if (DisplayName.Trim().Length is < 2 or > 40)
        {
            ErrorMessage = "Choisis un nom entre 2 et 40 caractères.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@'))
        {
            ErrorMessage = "Saisis une adresse e-mail valide.";
            return;
        }

        if (Password.Length < 8 || !Password.Any(char.IsUpper) || !Password.Any(char.IsDigit))
        {
            ErrorMessage = "Le mot de passe doit contenir au moins 8 caractères, une majuscule et un chiffre.";
            return;
        }

        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "Les deux mots de passe ne correspondent pas.";
            return;
        }

        if (string.IsNullOrWhiteSpace(StudyLevel))
        {
            ErrorMessage = "Sélectionne ton niveau d'études.";
            return;
        }

        if (!ConsentTerms)
        {
            ErrorMessage = "Tu dois accepter les conditions d'utilisation.";
            return;
        }

        IsBusy = true;
        try
        {
            var session = await _auth.RegisterAsync(new RegisterRequest(
                DisplayName.Trim(),
                Email.Trim(),
                Password,
                StudyLevel,
                ConsentTerms));

            await ApplyDisplayNameAsync(session.User.DisplayName);
            Password = string.Empty;
            ConfirmPassword = string.Empty;
            await Shell.Current.GoToAsync(Routes.Main);
        }
        catch (AuthException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            ErrorMessage = "Création impossible. Vérifie le réseau et l'adresse du serveur.";
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
    private static Task OpenLoginAsync() => Shell.Current.GoToAsync("..");

    [RelayCommand]
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
