using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Services;
using FlashRevise.Services.Ai;
using Microsoft.Maui.Media;

namespace FlashRevise.ViewModels;

/// <summary>Profil, objectif quotidien et configuration de la clé API Groq.</summary>
public partial class ProfileViewModel : BaseViewModel
{
    private readonly IStatsService _stats;
    private readonly IApiKeyStore _apiKeys;
    private readonly IAiService _ai;

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

    partial void OnIsDarkThemeChanged(bool value)
    {
        var target = value ? AppTheme.Dark : AppTheme.Light;
        if (Application.Current is not null && Application.Current.UserAppTheme != target)
        {
            Application.Current.UserAppTheme = target;
        }
    }

    public ProfileViewModel(IStatsService stats, IApiKeyStore apiKeys, IAiService ai)
    {
        _stats = stats;
        _apiKeys = apiKeys;
        _ai = ai;
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
}
