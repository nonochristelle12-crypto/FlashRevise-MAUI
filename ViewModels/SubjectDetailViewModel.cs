using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Models;
using FlashRevise.Services;
using Microsoft.Maui.Graphics;

namespace FlashRevise.ViewModels;

/// <summary>Détail d'une matière, création d'une matière et gestion de ses cartes.</summary>
public partial class SubjectDetailViewModel : BaseViewModel, IQueryAttributable
{
    private readonly ISubjectService _subjects;
    private readonly IFlashcardService _cards;
    private readonly IStatsService _stats;
    private int _subjectId;

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedColor { get; set; } = "#6C4CF1";

    [ObservableProperty]
    public partial bool IsNew { get; set; }

    [ObservableProperty]
    public partial string SubjectName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int TotalCards { get; set; }

    [ObservableProperty]
    public partial int DueCards { get; set; }

    [ObservableProperty]
    public partial double MasteryRatio { get; set; }

    [ObservableProperty]
    public partial string MasteryLabel { get; set; } = "0%";

    [ObservableProperty]
    public partial bool HasCards { get; set; }

    public SubjectDetailViewModel(ISubjectService subjects, IFlashcardService cards, IStatsService stats)
    {
        _subjects = subjects;
        _cards = cards;
        _stats = stats;
        Title = "Matière";
    }

    public ObservableCollection<Flashcard> Cards { get; } = [];

    public IReadOnlyList<string> ColorOptions { get; } =
        ["#6C4CF1", "#22D3EE", "#34D399", "#FBBF24", "#F87171", "#FB7185"];

    public Color AccentColor => Color.FromArgb(SelectedColor);

    public string HeaderTitle => IsNew ? "Nouvelle matière" : SubjectName;

    public string CardsLabel => TotalCards == 1 ? "1 carte" : $"{TotalCards} cartes";

    public string DueLabel => DueCards == 0 ? "À jour" : $"{DueCards} à réviser";

    public bool IsEmpty => !HasCards;

    partial void OnSelectedColorChanged(string value) => OnPropertyChanged(nameof(AccentColor));

    partial void OnIsNewChanged(bool value) => OnPropertyChanged(nameof(HeaderTitle));

    partial void OnSubjectNameChanged(string value) => OnPropertyChanged(nameof(HeaderTitle));

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var id = query.TryGetValue("id", out var raw) && raw is not null
            ? Convert.ToInt32(raw)
            : 0;
        _ = InitializeAsync(id);
    }

    private async Task InitializeAsync(int subjectId)
    {
        _subjectId = subjectId;
        IsNew = subjectId == 0;

        if (IsNew)
        {
            Name = string.Empty;
            Description = string.Empty;
            Cards.Clear();
            HasCards = false;
            OnPropertyChanged(nameof(IsEmpty));
            return;
        }

        await LoadSubjectAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadSubjectAsync();

    private async Task LoadSubjectAsync()
    {
        if (IsBusy || _subjectId == 0)
        {
            return;
        }

        IsBusy = true;
        ClearError();
        try
        {
            var subject = await _subjects.GetAsync(_subjectId);
            if (subject is null)
            {
                ErrorMessage = "Cette matière n'existe plus.";
                return;
            }

            SubjectName = subject.Name;
            Name = subject.Name;
            Description = subject.Description;
            SelectedColor = subject.Color;

            var cards = await _cards.GetBySubjectAsync(_subjectId);
            Cards.Clear();
            foreach (var card in cards.OrderByDescending(c => c.UpdatedAt))
            {
                Cards.Add(card);
            }

            HasCards = Cards.Count > 0;
            TotalCards = cards.Count;

            var stats = await _stats.GetSubjectStatsAsync(_subjectId);
            DueCards = stats?.DueCards ?? 0;
            MasteryRatio = stats?.MasteryRatio ?? 0;
            MasteryLabel = $"{MasteryRatio * 100:0}%";

            OnPropertyChanged(nameof(IsEmpty));
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
    private void SetColor(string hex) => SelectedColor = hex;

    [RelayCommand]
    private async Task SaveSubjectAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Donne un nom à ta matière.";
            return;
        }

        try
        {
            var subject = _subjectId > 0
                ? await _subjects.GetAsync(_subjectId) ?? new Subject()
                : new Subject();

            subject.Name = Name.Trim();
            subject.Description = Description.Trim();
            subject.Color = SelectedColor;

            await _subjects.SaveAsync(subject);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    [RelayCommand]
    private Task AddCardAsync() =>
        Shell.Current.GoToAsync($"{Routes.FlashcardEdit}?id=0&subjectId={_subjectId}");

    [RelayCommand]
    private static Task EditCardAsync(Flashcard? card) =>
        card is null
            ? Task.CompletedTask
            : Shell.Current.GoToAsync($"{Routes.FlashcardEdit}?id={card.Id}&subjectId={card.SubjectId}");

    [RelayCommand]
    private async Task DeleteCardAsync(Flashcard? card)
    {
        if (card is null)
        {
            return;
        }

        await _cards.DeleteAsync(card.Id);
        await LoadSubjectAsync();
    }

    [RelayCommand]
    private async Task StartRevisionAsync()
    {
        if (_subjectId == 0)
        {
            return;
        }

        await Shell.Current.GoToAsync($"{Routes.RevisionConfig}?subjectId={_subjectId}");
    }

    [RelayCommand]
    private async Task ScheduleAsync()
    {
        if (_subjectId == 0)
        {
            return;
        }

        await Shell.Current.GoToAsync($"{Routes.ScheduleRevision}?subjectId={_subjectId}");
    }

    [RelayCommand]
    private async Task ArchiveAsync()
    {
        if (_subjectId == 0)
        {
            return;
        }

        await _subjects.SetArchivedAsync(_subjectId, true);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
