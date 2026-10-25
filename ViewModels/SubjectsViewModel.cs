using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Models;
using FlashRevise.Services;

namespace FlashRevise.ViewModels;

/// <summary>Liste des matières : recherche, filtres et compteurs du design Stitch.</summary>
public partial class SubjectsViewModel : BaseViewModel
{
    private readonly IStatsService _stats;
    private readonly ISubjectService _subjects;
    private List<SubjectStats> _all = [];
    private List<SubjectStats> _archived = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ActiveFilter { get; set; } = "Toutes";

    [ObservableProperty]
    public partial int AllCount { get; set; }

    [ObservableProperty]
    public partial int DueCount { get; set; }

    [ObservableProperty]
    public partial int MasteredCount { get; set; }

    [ObservableProperty]
    public partial int ArchivedCount { get; set; }

    [ObservableProperty]
    public partial string GlobalMasteryLabel { get; set; } = "0%";

    [ObservableProperty]
    public partial string ActiveSubjectsLabel { get; set; } = "0 matières actives";

    public SubjectsViewModel(IStatsService stats, ISubjectService subjects)
    {
        _stats = stats;
        _subjects = subjects;
        Title = "Matières";
    }

    public ObservableCollection<SubjectStats> Items { get; } = [];

    public bool IsEmpty => Items.Count == 0;

    public bool IsAllFilter => ActiveFilter == "Toutes";

    public bool IsDueFilter => ActiveFilter == "À réviser";

    public bool IsMasteredFilter => ActiveFilter == "Maîtrisées";

    public bool IsArchivedFilter => ActiveFilter == "Archivées";

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnActiveFilterChanged(string value)
    {
        OnPropertyChanged(nameof(IsAllFilter));
        OnPropertyChanged(nameof(IsDueFilter));
        OnPropertyChanged(nameof(IsMasteredFilter));
        OnPropertyChanged(nameof(IsArchivedFilter));
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
            _all = await _stats.GetSubjectsStatsAsync();

            var archivedSubjects = await _subjects.GetSubjectsAsync(includeArchived: true);
            _archived = archivedSubjects.Select(s => new SubjectStats
            {
                SubjectId = s.Id,
                SubjectName = s.Name,
                Color = s.Color
            }).ToList();

            AllCount = _all.Count + _archived.Count;
            DueCount = _all.Count(s => s.DueCards > 0);
            MasteredCount = _all.Count(s => s.MasteryRatio >= 0.7);
            ArchivedCount = _archived.Count;
            var totalCards = _all.Sum(s => s.TotalCards);
            var mastery = totalCards == 0 ? 0 : (double)_all.Sum(s => s.MasteredCards) / totalCards;
            GlobalMasteryLabel = $"{mastery * 100:0}%";
            ActiveSubjectsLabel = $"{_all.Count} matières actives";

            ApplyFilter();
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
    private void SetFilter(string filter)
    {
        ActiveFilter = filter;
        ApplyFilter();
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    [RelayCommand]
    private static Task OpenSubjectAsync(SubjectStats? subject) =>
        subject is null ? Task.CompletedTask : Shell.Current.GoToAsync(Routes.WithId(Routes.SubjectDetail, subject.SubjectId));

    [RelayCommand]
    private static Task AddSubjectAsync() => Shell.Current.GoToAsync($"{Routes.SubjectDetail}?id=0");

    [RelayCommand]
    private async Task ArchiveAsync(SubjectStats? subject)
    {
        if (subject is null)
        {
            return;
        }

        await _subjects.SetArchivedAsync(subject.SubjectId, true);
        await LoadAsync();
    }

    private void ApplyFilter()
    {
        IEnumerable<SubjectStats> source = ActiveFilter switch
        {
            "À réviser" => _all.Where(s => s.DueCards > 0),
            "Maîtrisées" => _all.Where(s => s.MasteryRatio >= 0.7),
            "Archivées" => _archived,
            _ => _all
        };

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            source = source.Where(s => s.SubjectName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        Items.Clear();
        foreach (var item in source)
        {
            Items.Add(item);
        }

        OnPropertyChanged(nameof(IsEmpty));
    }
}
