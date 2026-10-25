using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Models;
using FlashRevise.Services;
using FlashRevise.Services.Ai;
using Microsoft.Extensions.Logging;

namespace FlashRevise.ViewModels;

/// <summary>Création et édition d'une flashcard, avec explication IA à la demande.</summary>
public partial class FlashcardEditViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IFlashcardService _cards;
    private readonly IAiService _ai;
    private readonly ILogger<FlashcardEditViewModel> _logger;
    private int _cardId;
    private int _subjectId;

    [ObservableProperty]
    public partial string Question { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Answer { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Hint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Tags { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int DifficultyIndex { get; set; } = 1;

    [ObservableProperty]
    public partial bool IsNew { get; set; } = true;

    [ObservableProperty]
    public partial string Explanation { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsExplaining { get; set; }

    public FlashcardEditViewModel(IFlashcardService cards, IAiService ai, ILogger<FlashcardEditViewModel> logger)
    {
        _cards = cards;
        _ai = ai;
        _logger = logger;
        Title = "Carte";
    }

    public IReadOnlyList<string> DifficultyOptions { get; } = ["Facile", "Moyen", "Difficile"];

    public string HeaderTitle => IsNew ? "Nouvelle carte" : "Modifier la carte";

    public bool HasExplanation => !string.IsNullOrWhiteSpace(Explanation);

    public bool CanExplain => !string.IsNullOrWhiteSpace(Question);

    partial void OnIsNewChanged(bool value) => OnPropertyChanged(nameof(HeaderTitle));

    partial void OnExplanationChanged(string value) => OnPropertyChanged(nameof(HasExplanation));

    partial void OnQuestionChanged(string value) => OnPropertyChanged(nameof(CanExplain));

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _cardId = query.TryGetValue("id", out var rawId) && rawId is not null ? Convert.ToInt32(rawId) : 0;
        _subjectId = query.TryGetValue("subjectId", out var rawSubject) && rawSubject is not null
            ? Convert.ToInt32(rawSubject)
            : 0;

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsNew = _cardId == 0;
        if (IsNew)
        {
            Question = string.Empty;
            Answer = string.Empty;
            Hint = string.Empty;
            Tags = string.Empty;
            DifficultyIndex = 1;
            Explanation = string.Empty;
            return;
        }

        IsBusy = true;
        try
        {
            var card = await _cards.GetAsync(_cardId);
            if (card is null)
            {
                ErrorMessage = "Cette carte n'existe plus.";
                return;
            }

            _subjectId = card.SubjectId;
            Question = card.Question;
            Answer = card.Answer;
            Hint = card.Hint;
            Tags = card.Tags;
            DifficultyIndex = card.Difficulty switch
            {
                DifficultyLevel.Easy => 0,
                DifficultyLevel.Hard => 2,
                _ => 1
            };
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
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Question) || string.IsNullOrWhiteSpace(Answer))
        {
            ErrorMessage = "La question et la réponse sont obligatoires.";
            return;
        }

        if (_subjectId == 0)
        {
            ErrorMessage = "Aucune matière associée à cette carte.";
            return;
        }

        try
        {
            var card = _cardId > 0
                ? await _cards.GetAsync(_cardId) ?? new Flashcard { SubjectId = _subjectId }
                : new Flashcard { SubjectId = _subjectId, CreatedAt = DateTime.UtcNow };

            card.Question = Question.Trim();
            card.Answer = Answer.Trim();
            card.Hint = Hint.Trim();
            card.Tags = Tags.Trim();
            card.Difficulty = DifficultyIndex switch
            {
                0 => DifficultyLevel.Easy,
                2 => DifficultyLevel.Hard,
                _ => DifficultyLevel.Medium
            };
            card.UpdatedAt = DateTime.UtcNow;

            await _cards.SaveAsync(card);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_cardId == 0)
        {
            return;
        }

        await _cards.DeleteAsync(_cardId);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task ExplainAsync()
    {
        if (!CanExplain || IsExplaining)
        {
            return;
        }

        IsExplaining = true;
        ClearError();
        try
        {
            var card = new Flashcard
            {
                Id = _cardId,
                SubjectId = _subjectId,
                Question = Question.Trim(),
                Answer = Answer.Trim(),
                Hint = Hint.Trim()
            };

            Explanation = await _ai.ExplainCardAsync(card);
        }
        catch (AiNotConfiguredException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Explication IA indisponible");
            ReportError(ex);
        }
        finally
        {
            IsExplaining = false;
        }
    }

    [RelayCommand]
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
