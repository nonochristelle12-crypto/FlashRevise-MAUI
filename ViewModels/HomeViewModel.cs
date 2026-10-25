using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Models;
using FlashRevise.Services;

namespace FlashRevise.ViewModels;

/// <summary>Tableau de bord : session prioritaire, objectif du jour, prochaines révisions.</summary>
public partial class HomeViewModel : BaseViewModel
{
    private readonly IStatsService _stats;
    private readonly IOfflineService _offline;

    [ObservableProperty]
    public partial DashboardSnapshot Dashboard { get; set; } = new();

    [ObservableProperty]
    public partial string Greeting { get; set; } = "Bonjour 👋";

    [ObservableProperty]
    public partial string GoalSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RetentionLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasSubjects { get; set; }

    public HomeViewModel(IStatsService stats, IOfflineService offline)
    {
        _stats = stats;
        _offline = offline;
        Title = "Accueil";
        _offline.OnlineChanged += (_, online) => IsOffline = !online;
        IsOffline = !_offline.IsOnline;
    }

    public ObservableCollection<SubjectStats> NextReviews { get; } = [];

    public double GoalRatio => Dashboard.GoalRatio;

    public string GoalPercentLabel => $"{Dashboard.GoalRatio * 100:0}%";

    public string DueHeadline => Dashboard.DueToday == 0
        ? "Tout est à jour"
        : $"{Dashboard.DueToday} cartes à réviser";

    public string DueSubtitle => Dashboard.DueToday == 0
        ? "Reviens demain pour garder ta série"
        : $"{Dashboard.UrgentCards} urgentes · {Dashboard.SpacedCards} rappels espacés";

    public string MasteryLabel => $"{Dashboard.GlobalMastery * 100:0}% de maîtrise globale";

    public bool HasStreak => Dashboard.StreakDays > 0;

    public bool HasAvatar => !string.IsNullOrWhiteSpace(Dashboard.AvatarPath) && File.Exists(Dashboard.AvatarPath);

    public bool HasNextReviews => NextReviews.Count > 0;

    public bool IsEmpty => !HasSubjects;

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
            Dashboard = await _stats.GetDashboardAsync();
            Greeting = Dashboard.IsGuest ? "Bonjour 👋" : $"Bonjour, {Dashboard.FirstName} 👋";
            GoalSummary = Dashboard.RemainingForGoal == 0
                ? "Objectif du jour atteint, bravo !"
                : $"Plus que {Dashboard.RemainingForGoal} cartes pour valider ton cap !";
            RetentionLabel = Dashboard.RetentionDelta == 0
                ? "Rétention stable sur 7 jours"
                : $"Rétention {Dashboard.RetentionDelta:+0;-0}% sur 7 jours";

            var subjects = await _stats.GetSubjectsStatsAsync();
            NextReviews.Clear();
            foreach (var subject in subjects.OrderByDescending(s => s.DueCards).Take(3))
            {
                NextReviews.Add(subject);
            }

            HasSubjects = subjects.Count > 0;
            OnPropertyChanged(nameof(HasNextReviews));
            OnPropertyChanged(nameof(HasStreak));
            OnPropertyChanged(nameof(HasAvatar));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(GoalRatio));
            OnPropertyChanged(nameof(GoalPercentLabel));
            OnPropertyChanged(nameof(DueHeadline));
            OnPropertyChanged(nameof(DueSubtitle));
            OnPropertyChanged(nameof(MasteryLabel));
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
    private static Task StartRevisionAsync() =>
        Shell.Current.GoToAsync($"{Routes.RevisionConfig}?subjectId=0");

    [RelayCommand]
    private static Task AddSubjectAsync() =>
        Shell.Current.GoToAsync($"{Routes.SubjectDetail}?id=0");

    [RelayCommand]
    private static Task OpenAiAsync() => Shell.Current.GoToAsync(Routes.AiUpload);

    [RelayCommand]
    private static Task OpenChatAsync() => Shell.Current.GoToAsync(Routes.AiChat);

    [RelayCommand]
    private static Task OpenSubjectAsync(SubjectStats? subject) =>
        subject is null ? Task.CompletedTask : Shell.Current.GoToAsync(Routes.WithId(Routes.SubjectDetail, subject.SubjectId));

    [RelayCommand]
    private static Task OpenNotificationsAsync() => Shell.Current.GoToAsync(Routes.Reminders);

    [RelayCommand]
    private static Task OpenOfflineAsync() => Shell.Current.GoToAsync(Routes.OfflineState);
}
