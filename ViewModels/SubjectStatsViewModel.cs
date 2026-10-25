using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Data.Repositories;
using FlashRevise.Models;
using FlashRevise.Services;
using Microsoft.Maui.Graphics;

namespace FlashRevise.ViewModels;

/// <summary>Ligne d'historique d'une session de révision sur une matière.</summary>
public sealed class SessionHistoryRow
{
    public SessionHistoryRow(ReviewSession session)
    {
        DateLabel = session.StartedAt.ToLocalTime().ToString("ddd d MMM · HH:mm", CultureInfo.CurrentCulture);
        CardsLabel = session.CardsReviewed == 1 ? "1 carte" : $"{session.CardsReviewed} cartes";
        AccuracyLabel = $"{Math.Round(session.SuccessRate * 100)} %";
        AccuracyRatio = session.SuccessRate;
        DurationLabel = session.DurationSeconds < 60
            ? $"{session.DurationSeconds} s"
            : $"{session.DurationSeconds / 60} min";
    }

    public string DateLabel { get; }
    public string CardsLabel { get; }
    public string AccuracyLabel { get; }
    public double AccuracyRatio { get; }
    public string DurationLabel { get; }
}

/// <summary>Détail de progression d'une matière : maîtrise, échéances et historique.</summary>
public partial class SubjectStatsViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IStatsService _stats;
    private readonly IReviewSessionRepository _sessions;

    private int _subjectId;

    [ObservableProperty]
    public partial string SubjectName { get; set; } = "Matière";

    [ObservableProperty]
    public partial string TotalLabel { get; set; } = "0";

    [ObservableProperty]
    public partial string DueLabel { get; set; } = "0";

    [ObservableProperty]
    public partial string MasteredLabel { get; set; } = "0";

    [ObservableProperty]
    public partial string TodayLabel { get; set; } = "0";

    [ObservableProperty]
    public partial string MasteryLabel { get; set; } = "0 %";

    [ObservableProperty]
    public partial double MasteryRatio { get; set; }

    [ObservableProperty]
    public partial string LastReviewedLabel { get; set; } = "Jamais révisée";

    [ObservableProperty]
    public partial Color AccentColor { get; set; } = Color.FromArgb("#6C4CF1");

    public SubjectStatsViewModel(IStatsService stats, IReviewSessionRepository sessions)
    {
        _stats = stats;
        _sessions = sessions;
        Title = "Statistiques";
    }

    public ObservableCollection<SessionHistoryRow> History { get; } = [];

    public bool HasHistory => History.Count > 0;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _subjectId = query.TryGetValue("id", out var raw) && raw is not null ? Convert.ToInt32(raw) : 0;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy || _subjectId == 0)
        {
            return;
        }

        IsBusy = true;
        ClearError();
        try
        {
            var stats = await _stats.GetSubjectStatsAsync(_subjectId);
            if (stats is null)
            {
                ErrorMessage = "Cette matière n'existe plus.";
                return;
            }

            SubjectName = stats.SubjectName;
            TotalLabel = stats.TotalCards.ToString();
            DueLabel = stats.DueCards.ToString();
            MasteredLabel = stats.MasteredCards.ToString();
            TodayLabel = stats.ReviewedToday.ToString();
            MasteryRatio = stats.MasteryRatio;
            MasteryLabel = $"{Math.Round(stats.MasteryRatio * 100)} %";
            AccentColor = stats.AccentColor;
            LastReviewedLabel = stats.LastReviewedAt is null
                ? "Jamais révisée"
                : $"Dernière révision : {stats.LastReviewedAt.Value.ToLocalTime():d MMM yyyy}";

            var recent = await _sessions.GetRecentAsync(30);
            History.Clear();
            foreach (var session in recent.Where(s => s.SubjectId == _subjectId && s.EndedAt is not null).Take(6))
            {
                History.Add(new SessionHistoryRow(session));
            }

            OnPropertyChanged(nameof(HasHistory));
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
    private Task ReviewNowAsync() =>
        _subjectId == 0 ? Task.CompletedTask : Shell.Current.GoToAsync($"{Routes.RevisionConfig}?subjectId={_subjectId}");

    [RelayCommand]
    private Task EditSubjectAsync() =>
        _subjectId == 0 ? Task.CompletedTask : Shell.Current.GoToAsync(Routes.WithId(Routes.SubjectDetail, _subjectId));

    [RelayCommand]
    private Task RemindersAsync() =>
        _subjectId == 0 ? Task.CompletedTask : Shell.Current.GoToAsync($"{Routes.ScheduleRevision}?subjectId={_subjectId}");

    [RelayCommand]
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
