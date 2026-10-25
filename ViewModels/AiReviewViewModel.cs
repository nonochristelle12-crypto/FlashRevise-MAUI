using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Models;
using FlashRevise.Services;

namespace FlashRevise.ViewModels;

/// <summary>Carte proposée par l'IA, éditable avant enregistrement.</summary>
public partial class AiReviewItem : ObservableObject
{
    private static readonly IReadOnlyList<string> Levels = ["Facile", "Moyen", "Difficile"];

    private readonly AiGeneratedCard _card;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = true;

    [ObservableProperty]
    public partial string Question { get; set; }

    [ObservableProperty]
    public partial string Answer { get; set; }

    [ObservableProperty]
    public partial string Hint { get; set; }

    public AiReviewItem(AiGeneratedCard card, int position)
    {
        _card = card;
        Position = position;
        Question = card.Question;
        Answer = card.Answer;
        Hint = card.Hint;
    }

    public int Position { get; }

    public IReadOnlyList<string> DifficultyOptions => Levels;

    public int DifficultyIndex
    {
        get => _card.Difficulty switch
        {
            DifficultyLevel.Easy => 0,
            DifficultyLevel.Hard => 2,
            _ => 1
        };
        set
        {
            var level = value switch
            {
                0 => DifficultyLevel.Easy,
                2 => DifficultyLevel.Hard,
                _ => DifficultyLevel.Medium
            };

            if (_card.Difficulty == level)
            {
                return;
            }

            _card.Difficulty = level;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DifficultyLabel));
        }
    }

    public string DifficultyLabel => DifficultyIndex switch
    {
        0 => "Facile",
        2 => "Difficile",
        _ => "Moyen"
    };

    public AiGeneratedCard ToCard()
    {
        _card.Question = Question.Trim();
        _card.Answer = Answer.Trim();
        _card.Hint = Hint.Trim();
        _card.IsAccepted = IsSelected;
        return _card;
    }
}

/// <summary>Validation, édition et suppression des cartes générées par l'IA.</summary>
public partial class AiReviewViewModel : BaseViewModel
{
    private readonly IFlashcardService _cards;
    private readonly AiDraftStore _draft;

    private bool _loaded;
    private int _savedSubjectId;

    [ObservableProperty]
    public partial string SubjectName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SourceLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CountLabel { get; set; } = "0 carte proposée";

    [ObservableProperty]
    public partial int SelectedCount { get; set; }

    [ObservableProperty]
    public partial bool IsSaved { get; set; }

    [ObservableProperty]
    public partial int SavedCount { get; set; }

    public AiReviewViewModel(IFlashcardService cards, AiDraftStore draft)
    {
        _cards = cards;
        _draft = draft;
        Title = "Valider les cartes";
    }

    public ObservableCollection<AiReviewItem> Cards { get; } = [];

    public bool HasCards => Cards.Count > 0;

    public bool HasSelection => SelectedCount > 0;

    public string SelectionLabel => $"{SelectedCount} / {Cards.Count} sélectionnées";

    public string SaveButtonLabel => SelectedCount > 1
        ? $"Enregistrer {SelectedCount} cartes"
        : "Enregistrer la carte";

    public string SavedLabel => SavedCount > 1
        ? $"{SavedCount} cartes ajoutées à la matière"
        : "1 carte ajoutée à la matière";

    /// <summary>Remplit l'écran depuis le brouillon partagé (appelé au premier affichage).</summary>
    public void Load()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;

        if (!_draft.IsReady || _draft.Cards.Count == 0)
        {
            ErrorMessage = "Aucune carte à valider. Relance une génération depuis l'écran IA.";
            return;
        }

        SubjectName = _draft.SubjectName;
        SourceLabel = _draft.SourceName;
        _savedSubjectId = _draft.SubjectId;

        var position = 1;
        foreach (var card in _draft.Cards)
        {
            var item = new AiReviewItem(card, position++);
            item.PropertyChanged += OnItemChanged;
            Cards.Add(item);
        }

        CountLabel = Cards.Count == 1 ? "1 carte proposée" : $"{Cards.Count} cartes proposées";
        RefreshSummary();
        OnPropertyChanged(nameof(HasCards));
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AiReviewItem.IsSelected))
        {
            RefreshSummary();
        }
    }

    private void RefreshSummary()
    {
        SelectedCount = Cards.Count(c => c.IsSelected);
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionLabel));
        OnPropertyChanged(nameof(SaveButtonLabel));
        OnPropertyChanged(nameof(SavedLabel));
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var item in Cards)
        {
            item.IsSelected = true;
        }
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var item in Cards)
        {
            item.IsSelected = false;
        }
    }

    [RelayCommand]
    private void Remove(AiReviewItem? item)
    {
        if (item is null)
        {
            return;
        }

        item.PropertyChanged -= OnItemChanged;
        Cards.Remove(item);
        CountLabel = Cards.Count == 1 ? "1 carte proposée" : $"{Cards.Count} cartes proposées";
        RefreshSummary();
        OnPropertyChanged(nameof(HasCards));

        if (Cards.Count == 0)
        {
            ErrorMessage = "Toutes les propositions ont été supprimées. Relance une génération.";
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy || IsSaved)
        {
            return;
        }

        ClearError();

        if (SelectedCount == 0)
        {
            ErrorMessage = "Sélectionne au moins une carte à enregistrer.";
            return;
        }

        if (_savedSubjectId == 0)
        {
            ErrorMessage = "La matière de destination est introuvable.";
            return;
        }

        IsBusy = true;
        try
        {
            var accepted = Cards.Where(c => c.IsSelected).Select(c => c.ToCard()).ToList();
            var saved = await _cards.SaveGeneratedAsync(_savedSubjectId, accepted);

            if (saved == 0)
            {
                ErrorMessage = "Les cartes sélectionnées sont incomplètes : vérifie la question et la réponse.";
                return;
            }

            SavedCount = saved;
            IsSaved = true;
            OnPropertyChanged(nameof(SavedLabel));

            var subjectId = _savedSubjectId;
            _draft.Reset();

            await Task.Delay(500);
            await OpenSubjectAsync(subjectId);
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
    private Task ViewSubjectAsync() => OpenSubjectAsync(_savedSubjectId);

    [RelayCommand]
    private static Task GoHomeAsync() => Shell.Current.GoToAsync(Routes.Main);

    [RelayCommand]
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");

    private static async Task OpenSubjectAsync(int subjectId)
    {
        if (subjectId <= 0)
        {
            await Shell.Current.GoToAsync(Routes.Main);
            return;
        }

        await Shell.Current.GoToAsync(Routes.Main);
        await Shell.Current.GoToAsync($"{Routes.SubjectDetail}?id={subjectId}");
    }
}
