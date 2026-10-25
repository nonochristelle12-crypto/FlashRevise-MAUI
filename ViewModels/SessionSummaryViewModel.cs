using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Models;
using FlashRevise.Services;

namespace FlashRevise.ViewModels;

/// <summary>Bilan de fin de session de révision.</summary>
public partial class SessionSummaryViewModel : BaseViewModel
{
    private readonly SessionOutcomeStore _outcomes;

    [ObservableProperty]
    public partial bool HasResult { get; set; }

    [ObservableProperty]
    public partial string SubjectName { get; set; } = "Révision";

    [ObservableProperty]
    public partial int TotalCards { get; set; }

    [ObservableProperty]
    public partial int CorrectAnswers { get; set; }

    [ObservableProperty]
    public partial int IncorrectAnswers { get; set; }

    [ObservableProperty]
    public partial string SuccessRateLabel { get; set; } = "0%";

    [ObservableProperty]
    public partial string DurationLabel { get; set; } = "00:00";

    [ObservableProperty]
    public partial string AverageLabel { get; set; } = "0 s / carte";

    [ObservableProperty]
    public partial string XpLabel { get; set; } = "0 XP";

    [ObservableProperty]
    public partial string StreakLabel { get; set; } = "0 jour";

    [ObservableProperty]
    public partial string Headline { get; set; } = "Session terminée";

    [ObservableProperty]
    public partial string Encouragement { get; set; } = string.Empty;

    public SessionSummaryViewModel(SessionOutcomeStore outcomes)
    {
        _outcomes = outcomes;
        Title = "Bilan";
        Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        var result = _outcomes.Last;
        HasResult = result is not null;
        if (result is null)
        {
            return;
        }

        SubjectName = result.SubjectName;
        TotalCards = result.TotalCards;
        CorrectAnswers = result.CorrectAnswers;
        IncorrectAnswers = result.IncorrectAnswers;
        SuccessRateLabel = $"{result.SuccessRate * 100:0}%";
        DurationLabel = $"{(int)result.Duration.TotalMinutes:00}:{result.Duration.Seconds:00}";
        AverageLabel = $"{result.AverageSecondsPerCard:0.0} s / carte";
        XpLabel = $"+{result.XpEarned} XP";
        StreakLabel = result.StreakDays == 1 ? "1 jour" : $"{result.StreakDays} jours";

        Headline = result.SuccessRate switch
        {
            >= 0.85 => "Excellent travail !",
            >= 0.6 => "Belle session !",
            _ => "Session terminée"
        };

        Encouragement = result.SuccessRate switch
        {
            >= 0.85 => "Ta mémoire consolide vite. Garde ce rythme pour ancrer ces notions sur le long terme.",
            >= 0.6 => "Bonne base : les cartes ratées reviendront bientôt pour être renforcées.",
            _ => "C'est en se trompant qu'on apprend. Les cartes difficiles sont reprogrammées dès demain."
        };
    }

    [RelayCommand]
    private static Task DoneAsync() => Shell.Current.GoToAsync(Routes.Main);

    [RelayCommand]
    private static Task NewSessionAsync() => Shell.Current.GoToAsync(Routes.RevisionConfig);

    [RelayCommand]
    private static Task OpenStatsAsync() => Shell.Current.GoToAsync(Routes.Main);
}
