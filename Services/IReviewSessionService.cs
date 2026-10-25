using FlashRevise.Models;

namespace FlashRevise.Services;

/// <summary>Résultat immédiat après validation d'une carte.</summary>
public sealed record ReviewOutcome(bool HasNext, Flashcard? NextCard, int Remaining);

/// <summary>Pilote une session de révision : file de cartes, journalisation, progression.</summary>
public interface IReviewSessionService
{
    bool IsActive { get; }
    int SessionId { get; }
    int SubjectId { get; }
    string SubjectName { get; }
    int TotalCards { get; }
    int CompletedCards { get; }
    int CorrectCount { get; }
    int IncorrectCount { get; }
    int CurrentIndex { get; }
    Flashcard? CurrentCard { get; }

    /// <summary>Prépare la file de cartes et ouvre la session en base.</summary>
    Task<bool> BeginAsync(int subjectId, int cardLimit, bool randomize);

    /// <summary>Enregistre la réponse, met à jour le planning SM-2 et avance.</summary>
    Task<ReviewOutcome> SubmitAsync(RecallQuality quality, int durationMs);

    /// <summary>Passe la carte sans l'évaluer.</summary>
    Task<ReviewOutcome> SkipAsync();

    /// <summary>Clôture la session, met à jour le streak et renvoie le bilan.</summary>
    Task<SessionResult> FinishAsync();

    /// <summary>Abandonne la session en cours sans bilan.</summary>
    void Reset();
}
