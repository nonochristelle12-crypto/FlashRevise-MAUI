using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;

namespace FlashRevise.ViewModels;

/// <summary>Contenu d'une diapositive d'onboarding.</summary>
public sealed record OnboardingSlide(string Title, string Description, string Icon, Color Accent, string Badge);

/// <summary>Parcours de bienvenue en quatre étapes.</summary>
public partial class OnboardingViewModel : BaseViewModel
{
    [ObservableProperty]
    public partial int Position { get; set; }

    [ObservableProperty]
    public partial OnboardingSlide CurrentSlide { get; set; } = null!;

    public OnboardingViewModel()
    {
        Title = "Bienvenue";
        Slides =
        [
            new("Révise mieux,\npas plus", "Des sessions courtes et ciblées, construites autour de ta mémoire — pas de ton temps libre.", Icons.Bolt, Color.FromArgb("#6C4CF1"), "Méthode FlashRevise"),
            new("Des cartes qui\ns'adaptent", "La répétition espacée SM-2 replace chaque notion au bon moment, juste avant l'oubli.", Icons.Schedule, Color.FromArgb("#22D3EE"), "Répétition espacée"),
            new("Ton copilote IA", "Importe un cours en PDF ou colle ton texte : l'IA en fait des flashcards prêtes à valider.", Icons.AutoAwesome, Color.FromArgb("#34D399"), "Groq · Llama"),
            new("Suis ta\nprogression", "Séries, maîtrise par matière et objectifs quotidiens pour garder le cap jusqu'au jour J.", Icons.Insights, Color.FromArgb("#FBBF24"), "Statistiques")
        ];

        CurrentSlide = Slides[0];
    }

    public ObservableCollection<OnboardingSlide> Slides { get; }

    public bool IsLast => Position >= Slides.Count - 1;

    public bool IsNotLast => !IsLast;

    public string PrimaryActionLabel => IsLast ? "C'est parti" : "Suivant";

    partial void OnPositionChanged(int value)
    {
        CurrentSlide = Slides[Math.Clamp(value, 0, Slides.Count - 1)];
        OnPropertyChanged(nameof(IsLast));
        OnPropertyChanged(nameof(IsNotLast));
        OnPropertyChanged(nameof(PrimaryActionLabel));
    }

    [RelayCommand]
    private void Previous()
    {
        if (Position > 0)
        {
            Position--;
        }
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        if (!IsLast)
        {
            Position++;
            return;
        }

        await FinishAsync();
    }

    [RelayCommand]
    private Task SkipAsync() => FinishAsync();

    private static async Task FinishAsync()
    {
        Preferences.Default.Set(AppPreferences.OnboardingCompleted, true);
        await Shell.Current.GoToAsync(Routes.Main);
    }
}
