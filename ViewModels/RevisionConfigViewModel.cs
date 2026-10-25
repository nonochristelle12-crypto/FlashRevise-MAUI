using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Models;
using FlashRevise.Services;
using Microsoft.Maui.Graphics;

namespace FlashRevise.ViewModels;

/// <summary>Option de nombre de cartes proposée avant de lancer une session.</summary>
public sealed record CountOption(int Value, string Label);

/// <summary>Choix du périmètre d'une session de révision.</summary>
public partial class RevisionConfigViewModel : BaseViewModel, IQueryAttributable
{
    private readonly ISubjectService _subjects;
    private readonly IStatsService _stats;
    private readonly IReviewSessionService _session;
    private int _requestedSubjectId;

    [ObservableProperty]
    public partial int SelectedSubjectIndex { get; set; }

    [ObservableProperty]
    public partial int SelectedCountIndex { get; set; } = 1;

    [ObservableProperty]
    public partial bool IsRandomized { get; set; } = true;

    [ObservableProperty]
    public partial string DueLabel { get; set; } = "0 carte à réviser";

    [ObservableProperty]
    public partial string SubjectLabel { get; set; } = "Révision libre";

    public RevisionConfigViewModel(ISubjectService subjects, IStatsService stats, IReviewSessionService session)
    {
        _subjects = subjects;
        _stats = stats;
        _session = session;
        Title = "Nouvelle session";
    }

    public ObservableCollection<Subject> Subjects { get; } = [];

    public ObservableCollection<string> SubjectNames { get; } = [];

    public IReadOnlyList<CountOption> CountOptions { get; } =
    [
        new(10, "10 cartes"),
        new(20, "20 cartes"),
        new(30, "30 cartes"),
        new(0, "Toutes les cartes")
    ];

    public IReadOnlyList<string> CountLabels { get; } = ["10 cartes", "20 cartes", "30 cartes", "Toutes les cartes"];

    public bool HasSubjects => Subjects.Count > 0;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _requestedSubjectId = query.TryGetValue("subjectId", out var raw) && raw is not null
            ? Convert.ToInt32(raw)
            : 0;

        _ = LoadAsync();
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
            var subjects = await _subjects.GetSubjectsAsync();
            Subjects.Clear();
            SubjectNames.Clear();
            foreach (var subject in subjects)
            {
                Subjects.Add(subject);
                SubjectNames.Add(subject.Name);
            }

            if (_requestedSubjectId > 0)
            {
                var index = Subjects.ToList().FindIndex(s => s.Id == _requestedSubjectId);
                if (index >= 0)
                {
                    SelectedSubjectIndex = index;
                }
            }

            OnPropertyChanged(nameof(HasSubjects));
            await RefreshDueAsync();
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

    partial void OnSelectedSubjectIndexChanged(int value) => _ = RefreshDueAsync();

    private async Task RefreshDueAsync()
    {
        try
        {
            if (SelectedSubjectIndex < 0 || SelectedSubjectIndex >= Subjects.Count)
            {
                SubjectLabel = "Révision libre";
                return;
            }

            var subject = Subjects[SelectedSubjectIndex];
            SubjectLabel = subject.Name;
            var stats = await _stats.GetSubjectStatsAsync(subject.Id);
            var due = stats?.DueCards ?? 0;
            DueLabel = due == 0
                ? "Aucune échéance : la session utilisera tes cartes existantes."
                : due == 1
                    ? "1 carte arrive à échéance"
                    : $"{due} cartes arrivent à échéance";
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (Subjects.Count == 0)
        {
            ErrorMessage = "Crée d'abord une matière pour lancer une session.";
            return;
        }

        IsBusy = true;
        ClearError();
        try
        {
            var subject = Subjects[Math.Clamp(SelectedSubjectIndex, 0, Subjects.Count - 1)];
            var count = CountOptions[Math.Clamp(SelectedCountIndex, 0, CountOptions.Count - 1)].Value;

            var started = await _session.BeginAsync(subject.Id, count, IsRandomized);
            if (!started)
            {
                ErrorMessage = "Aucune carte disponible dans cette matière. Ajoute des cartes pour commencer.";
                return;
            }

            await Shell.Current.GoToAsync(Routes.RevisionSession);
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
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
