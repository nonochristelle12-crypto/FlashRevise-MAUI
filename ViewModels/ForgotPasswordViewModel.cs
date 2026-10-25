using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Services.Auth;

namespace FlashRevise.ViewModels;

/// <summary>Demande d'e-mail de réinitialisation (réponse volontairement neutre).</summary>
public partial class ForgotPasswordViewModel : BaseViewModel
{
    private readonly IAuthService _auth;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatusMessage));

    public ForgotPasswordViewModel(IAuthService auth)
    {
        _auth = auth;
        Title = "Mot de passe oublié";
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ClearError();
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@'))
        {
            ErrorMessage = "Saisis l'adresse e-mail de ton compte.";
            return;
        }

        IsBusy = true;
        try
        {
            await _auth.RequestPasswordResetAsync(Email.Trim());
            StatusMessage = "Si un compte existe pour cette adresse, un e-mail de réinitialisation vient d'être envoyé. Le lien expire rapidement.";
        }
        catch (AuthException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            ErrorMessage = "Demande impossible. Vérifie le réseau et l'adresse du serveur.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
