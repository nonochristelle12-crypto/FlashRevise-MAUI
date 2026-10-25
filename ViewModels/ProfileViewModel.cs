using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Services;
using FlashRevise.Services.Ai;
using FlashRevise.Services.Auth;
using Microsoft.Maui.Media;

namespace FlashRevise.ViewModels;

/// <summary>Profil, objectif quotidien et configuration de la clé API Groq.</summary>
public partial class ProfileViewModel : BaseViewModel
{
    private readonly IStatsService _stats;
    private readonly IApiKeyStore _apiKeys;
    private readonly IAiService _ai;
    private readonly IAuthService _auth;

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AvatarPath { get; set; } = string.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "Invité" : Name;

    public bool HasAvatar => !string.IsNullOrWhiteSpace(AvatarPath) && File.Exists(AvatarPath);

    public bool HasNoAvatar => !HasAvatar;

    [ObservableProperty]
    public partial string DailyGoalText { get; set; } = "30";

    [ObservableProperty]
    public partial string StreakLabel { get; set; } = "0 jour";

    [ObservableProperty]
    public partial string LongestStreakLabel { get; set; } = "0 jour";

    [ObservableProperty]
    public partial string XpLabel { get; set; } = "0 XP";

    [ObservableProperty]
    public partial string ApiKeyInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasApiKey { get; set; }

    [ObservableProperty]
    public partial string ApiStatus { get; set; } = "Aucune clé configurée";

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial bool IsDarkTheme { get; set; }

    [ObservableProperty]
    public partial bool IsSignedIn { get; set; }

    [ObservableProperty]
    public partial string AccountEmail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AuthBaseUrlInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? AuthStatusMessage { get; set; }

    [ObservableProperty]
    public partial bool IsAuthBusy { get; set; }

    public bool IsSignedOut => !IsSignedIn;

    public bool IsNotAuthBusy => !IsAuthBusy;

    public bool HasAuthStatusMessage => !string.IsNullOrWhiteSpace(AuthStatusMessage);

    public string AccountSubtitle => IsSignedIn
        ? $"Connecté · {AccountEmail}"
        : "Mode invité — tes révisions restent sur l'appareil";

    public string ServerHint =>
        "Sur téléphone, saisis l'adresse IP du PC sur le même Wi-Fi (ex. http://192.168.1.10:5047/).";

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public string ApiKeyHint => HasApiKey
        ? "Clé active — tu peux la remplacer à tout moment"
        : "Colle ta clé Groq (gsk_...) pour activer la génération IA";

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatusMessage));

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(DisplayName));

    partial void OnAvatarPathChanged(string value)
    {
        OnPropertyChanged(nameof(HasAvatar));
        OnPropertyChanged(nameof(HasNoAvatar));
    }

    partial void OnHasApiKeyChanged(bool value) => OnPropertyChanged(nameof(ApiKeyHint));

    partial void OnIsSignedInChanged(bool value)
    {
        OnPropertyChanged(nameof(IsSignedOut));
        OnPropertyChanged(nameof(AccountSubtitle));
    }

    partial void OnAccountEmailChanged(string value) => OnPropertyChanged(nameof(AccountSubtitle));

    partial void OnAuthStatusMessageChanged(string? value) =>
        OnPropertyChanged(nameof(HasAuthStatusMessage));

    partial void OnIsAuthBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotAuthBusy));

    partial void OnIsDarkThemeChanged(bool value)
    {
        var target = value ? AppTheme.Dark : AppTheme.Light;
        if (Application.Current is not null && Application.Current.UserAppTheme != target)
        {
            Application.Current.UserAppTheme = target;
        }
    }

    public ProfileViewModel(IStatsService stats, IApiKeyStore apiKeys, IAiService ai, IAuthService auth)
    {
        _stats = stats;
        _apiKeys = apiKeys;
        _ai = ai;
        _auth = auth;
        Title = "Profil";
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ClearError();
        try
        {
            var profile = await _stats.GetProfileAsync();
            Name = profile.Name;
            AvatarPath = profile.AvatarPath;
            DailyGoalText = profile.DailyGoal.ToString();
            StreakLabel = profile.Streak == 1 ? "1 jour" : $"{profile.Streak} jours";
            LongestStreakLabel = profile.LongestStreak == 1 ? "1 jour" : $"{profile.LongestStreak} jours";
            XpLabel = $"{profile.TotalXp} XP";

            HasApiKey = await _apiKeys.HasKeyAsync();
            ApiStatus = HasApiKey
                ? "Clé enregistrée dans le stockage sécurisé"
                : "Aucune clé configurée";
            IsDarkTheme = Application.Current?.UserAppTheme == AppTheme.Dark;

            IsSignedIn = await _auth.HasSessionAsync();
            var account = await _auth.GetCurrentUserAsync();
            AccountEmail = account?.Email ?? string.Empty;
            AuthBaseUrlInput = _auth.BaseUrl;
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        try
        {
            var profile = await _stats.GetProfileAsync();
            profile.Name = Name.Trim();
            profile.DailyGoal = int.TryParse(DailyGoalText, out var goal) ? Math.Clamp(goal, 1, 500) : profile.DailyGoal;
            await _stats.SaveProfileAsync(profile);
            DailyGoalText = profile.DailyGoal.ToString();
            StatusMessage = "Profil enregistré.";
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    [RelayCommand]
    private async Task PickAvatarAsync()
    {
        try
        {
            ClearError();
            var photos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
            {
                Title = "Choisir une photo de profil"
            });
            var photo = photos?.FirstOrDefault();

            if (photo is null)
            {
                return;
            }

            var extension = Path.GetExtension(photo.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".jpg";
            }

            var destination = Path.Combine(FileSystem.AppDataDirectory, $"avatar_{DateTime.UtcNow.Ticks}{extension}");
            await using (var source = await photo.OpenReadAsync())
            await using (var target = File.Create(destination))
            {
                await source.CopyToAsync(target);
            }

            var profile = await _stats.GetProfileAsync();
            var previous = profile.AvatarPath;
            profile.AvatarPath = destination;
            await _stats.SaveProfileAsync(profile);

            if (!string.IsNullOrWhiteSpace(previous) && previous != destination && File.Exists(previous))
            {
                File.Delete(previous);
            }

            AvatarPath = destination;
            StatusMessage = "Photo de profil mise à jour.";
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    [RelayCommand]
    private async Task RemoveAvatarAsync()
    {
        try
        {
            ClearError();
            var profile = await _stats.GetProfileAsync();
            if (!string.IsNullOrWhiteSpace(profile.AvatarPath) && File.Exists(profile.AvatarPath))
            {
                File.Delete(profile.AvatarPath);
            }

            profile.AvatarPath = string.Empty;
            await _stats.SaveProfileAsync(profile);
            AvatarPath = string.Empty;
            StatusMessage = "Photo de profil supprimée.";
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    [RelayCommand]
    private async Task SaveApiKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiKeyInput))
        {
            StatusMessage = "Colle une clé avant d'enregistrer.";
            return;
        }

        await _apiKeys.SetKeyAsync(ApiKeyInput);
        HasApiKey = await _apiKeys.HasKeyAsync();
        ApiStatus = _apiKeys.IsSecureStorageAvailable
            ? "Clé enregistrée dans le stockage sécurisé"
            : "Clé gardée en mémoire : le stockage sécurisé est indisponible sur cet appareil";
        ApiKeyInput = string.Empty;
        StatusMessage = "Clé enregistrée.";
    }

    [RelayCommand]
    private async Task TestApiKeyAsync()
    {
        IsBusy = true;
        try
        {
            var ok = await _ai.TestConnectionAsync();
            StatusMessage = ok ? "Connexion Groq opérationnelle." : "Échec de connexion : vérifie la clé et le réseau.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClearApiKeyAsync()
    {
        await _apiKeys.ClearAsync();
        HasApiKey = false;
        ApiStatus = "Aucune clé configurée";
        StatusMessage = "Clé supprimée.";
    }

    [RelayCommand]
    private static Task OpenGoalsAsync() => Shell.Current.GoToAsync(Routes.GoalsBooks);

    [RelayCommand]
    private static Task OpenAchievementsAsync() => Shell.Current.GoToAsync(Routes.Achievements);

    [RelayCommand]
    private static Task OpenRemindersAsync() => Shell.Current.GoToAsync(Routes.Reminders);

    [RelayCommand]
    private static Task OpenOfflineAsync() => Shell.Current.GoToAsync(Routes.OfflineState);

    [RelayCommand]
    private static Task OpenLoginAsync() => Shell.Current.GoToAsync(Routes.Login);

    [RelayCommand]
    private static Task OpenRegisterAsync() => Shell.Current.GoToAsync(Routes.Register);

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Se déconnecter",
            "Tes cartes et tes révisions restent sur l'appareil. Tu repasses en mode invité.",
            "Se déconnecter",
            "Annuler");
        if (!confirmed)
        {
            return;
        }

        IsAuthBusy = true;
        AuthStatusMessage = null;
        try
        {
            await _auth.LogoutAsync();
            await ClearAccountStateAsync();
            AuthStatusMessage = "Déconnecté. Mode invité activé.";
        }
        catch (Exception ex)
        {
            AuthStatusMessage = ex.Message;
        }
        finally
        {
            IsAuthBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAccountAsync()
    {
        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Supprimer mon compte",
            "Le compte sera supprimé définitivement sur le serveur. Tes cartes locales restent sur l'appareil.",
            "Continuer",
            "Annuler");
        if (!confirmed)
        {
            return;
        }

        var doubleCheck = await Shell.Current.DisplayAlertAsync(
            "Confirmer la suppression",
            "Cette action est irréversible.",
            "Supprimer définitivement",
            "Annuler");
        if (!doubleCheck)
        {
            return;
        }

        IsAuthBusy = true;
        AuthStatusMessage = null;
        try
        {
            await _auth.DeleteAccountAsync();
            await ClearAccountStateAsync();
            AuthStatusMessage = "Compte supprimé. Mode invité activé.";
        }
        catch (Exception ex)
        {
            AuthStatusMessage = ex.Message;
        }
        finally
        {
            IsAuthBusy = false;
        }
    }

    [RelayCommand]
    private void SaveServerUrl()
    {
        try
        {
            _auth.SetBaseUrl(AuthBaseUrlInput);
            AuthBaseUrlInput = _auth.BaseUrl;
            AuthStatusMessage = "Adresse du serveur enregistrée.";
        }
        catch (Exception ex)
        {
            AuthStatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task TestServerAsync()
    {
        IsAuthBusy = true;
        AuthStatusMessage = "Test de connexion en cours…";
        try
        {
            _auth.SetBaseUrl(AuthBaseUrlInput);
            AuthBaseUrlInput = _auth.BaseUrl;
            var reachable = await _auth.CheckServerAsync();
            AuthStatusMessage = reachable
                ? $"Serveur joignable ({_auth.BaseUrl})."
                : $"Serveur injoignable ({_auth.BaseUrl}). Vérifie que l'API tourne et que le téléphone est sur le même Wi-Fi.";
        }
        catch (Exception ex)
        {
            AuthStatusMessage = ex.Message;
        }
        finally
        {
            IsAuthBusy = false;
        }
    }

    private async Task ClearAccountStateAsync()
    {
        IsSignedIn = false;
        AccountEmail = string.Empty;

        var profile = await _stats.GetProfileAsync();
        if (!string.IsNullOrWhiteSpace(profile.Name))
        {
            profile.Name = string.Empty;
            await _stats.SaveProfileAsync(profile);
        }

        Name = string.Empty;
    }
}
