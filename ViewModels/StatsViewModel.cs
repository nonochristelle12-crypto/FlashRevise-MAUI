using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Models;
using FlashRevise.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace FlashRevise.ViewModels;

/// <summary>Tableau de bord de progression : KPI, activité hebdomadaire, maîtrise par matière.</summary>
public partial class StatsViewModel : BaseViewModel
{
    private readonly IStatsService _stats;

    [ObservableProperty]
    public partial int TotalReviews { get; set; }

    [ObservableProperty]
    public partial int MasteredCards { get; set; }

    [ObservableProperty]
    public partial int CurrentStreak { get; set; }

    [ObservableProperty]
    public partial string AccuracyLabel { get; set; } = "0%";

    [ObservableProperty]
    public partial string XpLabel { get; set; } = "0 XP";

    [ObservableProperty]
    public partial ISeries[] ReviewsSeries { get; set; } = [];

    [ObservableProperty]
    public partial ISeries[] AccuracySeries { get; set; } = [];

    [ObservableProperty]
    public partial Axis[] ReviewXAxes { get; set; } = [new Axis { IsVisible = false }];

    [ObservableProperty]
    public partial Axis[] AccuracyXAxes { get; set; } = [new Axis { IsVisible = false }];

    [ObservableProperty]
    public partial Axis[] ReviewYAxes { get; set; } = [new Axis { IsVisible = false }];

    [ObservableProperty]
    public partial Axis[] AccuracyYAxes { get; set; } = [new Axis { IsVisible = false, MinLimit = 0, MaxLimit = 100 }];

    public StatsViewModel(IStatsService stats)
    {
        _stats = stats;
        Title = "Progrès";
    }

    public ObservableCollection<SubjectStats> Subjects { get; } = [];

    public bool HasSubjects => Subjects.Count > 0;

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
            var activity = await _stats.GetActivityAsync(7);
            var subjects = await _stats.GetSubjectsStatsAsync();

            TotalReviews = activity.Sum(a => a.Reviews);
            CurrentStreak = profile.Streak;
            XpLabel = $"{profile.TotalXp} XP";

            var charts = await _stats.GetActivityAsync(30);
            var total = charts.Sum(c => c.Reviews);
            var correct = charts.Sum(c => (int)Math.Round(c.Reviews * c.Accuracy));
            AccuracyLabel = total == 0 ? "0%" : $"{correct * 100.0 / total:0}%";
            MasteredCards = subjects.Sum(s => s.MasteredCards);

            BuildCharts(activity);

            Subjects.Clear();
            foreach (var subject in subjects.OrderByDescending(s => s.TotalCards))
            {
                Subjects.Add(subject);
            }

            OnPropertyChanged(nameof(HasSubjects));
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
    private static Task OpenSubjectAsync(SubjectStats? subject) =>
        subject is null ? Task.CompletedTask : Shell.Current.GoToAsync(Routes.WithId(Routes.SubjectStats, subject.SubjectId));

    [RelayCommand]
    private static Task OpenGoalsAsync() => Shell.Current.GoToAsync(Routes.GoalsBooks);

    [RelayCommand]
    private static Task OpenAchievementsAsync() => Shell.Current.GoToAsync(Routes.Achievements);

    private void BuildCharts(IReadOnlyList<DailyPoint> points)
    {
        var labels = points.Select(p => p.Date.ToString("ddd")).ToArray();

        ReviewsSeries =
        [
            new ColumnSeries<double>
            {
                Values = points.Select(p => (double)p.Reviews).ToArray(),
                Fill = new SolidColorPaint(SKColor.Parse("#6C4CF1")),
                Stroke = null,
                MaxBarWidth = 18,
                Rx = 8,
                Ry = 8,
                Name = "Cartes révisées"
            }
        ];

        AccuracySeries =
        [
            new LineSeries<double>
            {
                Values = points.Select(p => Math.Round(p.Accuracy * 100, 1)).ToArray(),
                Stroke = new SolidColorPaint(SKColor.Parse("#22D3EE")) { StrokeThickness = 3 },
                Fill = null,
                GeometrySize = 6,
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#22D3EE")) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColor.Parse("#101221")),
                LineSmoothness = 0.6,
                Name = "Rétention"
            }
        ];

        var axisPaint = new SolidColorPaint(SKColor.Parse("#938EA1")) { SKTypeface = SKTypeface.Default };
        ReviewXAxes = [new Axis { Labels = labels, LabelsPaint = axisPaint, SeparatorsPaint = null, TextSize = 11 }];
        AccuracyXAxes = [new Axis { Labels = labels, LabelsPaint = axisPaint, SeparatorsPaint = null, TextSize = 11 }];
        ReviewYAxes = [new Axis { LabelsPaint = axisPaint, SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#2C2F42")) { StrokeThickness = 1 }, MinLimit = 0, TextSize = 11 }];
        AccuracyYAxes = [new Axis { LabelsPaint = axisPaint, SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#2C2F42")) { StrokeThickness = 1 }, MinLimit = 0, MaxLimit = 100, TextSize = 11 }];
    }
}
