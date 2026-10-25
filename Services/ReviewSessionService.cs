using FlashRevise.Data.Repositories;
using FlashRevise.Models;

namespace FlashRevise.Services;

/// <inheritdoc cref="IReviewSessionService"/>
public sealed class ReviewSessionService(
    IFlashcardService flashcardService,
    IReviewLogRepository logs,
    IReviewScheduleRepository schedules,
    IReviewSessionRepository sessions,
    ISubjectRepository subjects,
    ISpacedRepetitionService spacedRepetition,
    IStatsService stats) : IReviewSessionService
{
    private readonly List<Flashcard> _queue = [];
    private DateTime _startedAt;
    private int _xpEarned;

    public bool IsActive { get; private set; }

    public int SessionId { get; private set; }

    public int SubjectId { get; private set; }

    public string SubjectName { get; private set; } = string.Empty;

    public int TotalCards => _queue.Count;

    public int CompletedCards { get; private set; }

    public int CorrectCount { get; private set; }

    public int IncorrectCount { get; private set; }

    public int CurrentIndex => Math.Min(CompletedCards, Math.Max(0, _queue.Count - 1));

    public Flashcard? CurrentCard => CompletedCards < _queue.Count ? _queue[CompletedCards] : null;

    public async Task<bool> BeginAsync(int subjectId, int cardLimit, bool randomize)
    {
        var due = await flashcardService.GetDueAsync(subjectId, cardLimit, randomize).ConfigureAwait(false);
        if (due.Count == 0 && subjectId > 0)
        {
            // Filet de sécurité : aucune échéance en base (première utilisation) → on prend les cartes de la matière.
            due = await flashcardService.GetBySubjectAsync(subjectId).ConfigureAwait(false);
            if (randomize)
            {
                due = due.OrderBy(_ => Random.Shared.Next()).ToList();
            }

            due = cardLimit > 0 ? due.Take(cardLimit).ToList() : due;
        }

        if (due.Count == 0)
        {
            return false;
        }

        _queue.Clear();
        _queue.AddRange(due);
        CompletedCards = 0;
        CorrectCount = 0;
        IncorrectCount = 0;
        _xpEarned = 0;
        _startedAt = DateTime.UtcNow;
        SubjectId = subjectId;

        var subject = await subjects.GetAsync(subjectId).ConfigureAwait(false);
        SubjectName = subject?.Name ?? "Révision libre";

        SessionId = await sessions.StartAsync(new ReviewSession
        {
            SubjectId = subjectId,
            StartedAt = _startedAt
        }).ConfigureAwait(false);

        IsActive = true;
        return true;
    }

    public async Task<ReviewOutcome> SubmitAsync(RecallQuality quality, int durationMs)
    {
        EnsureActive();
        var card = CurrentCard ?? throw new InvalidOperationException("Aucune carte courante.");

        var knewIt = quality >= RecallQuality.Good;
        var now = DateTime.UtcNow;

        await logs.AddAsync(new ReviewLog
        {
            FlashcardId = card.Id,
            SubjectId = card.SubjectId,
            ReviewedAt = now,
            KnewIt = knewIt,
            DurationMs = Math.Max(0, durationMs),
            Quality = quality
        }).ConfigureAwait(false);

        var previous = await schedules.GetByFlashcardAsync(card.Id).ConfigureAwait(false);
        var next = spacedRepetition.ComputeNextReview(previous, quality, now);
        await schedules.SaveAsync(next).ConfigureAwait(false);

        CompletedCards++;
        _xpEarned += knewIt ? 10 : 4;
        if (knewIt)
        {
            CorrectCount++;
        }
        else
        {
            IncorrectCount++;
        }

        return new ReviewOutcome(CurrentCard is not null, CurrentCard, _queue.Count - CompletedCards);
    }

    public Task<ReviewOutcome> SkipAsync()
    {
        EnsureActive();
        CompletedCards++;
        return Task.FromResult(new ReviewOutcome(CurrentCard is not null, CurrentCard, _queue.Count - CompletedCards));
    }

    public async Task<SessionResult> FinishAsync()
    {
        var subjectName = SubjectName;
        var total = _queue.Count;
        var correct = CorrectCount;
        var incorrect = IncorrectCount;

        if (IsActive)
        {
            var session = await sessions.GetAsync(SessionId).ConfigureAwait(false);
            if (session is not null)
            {
                session.EndedAt = DateTime.UtcNow;
                session.CardsReviewed = Math.Max(0, CompletedCards);
                session.CorrectCount = correct;
                session.IncorrectCount = incorrect;
                await sessions.CompleteAsync(session).ConfigureAwait(false);
            }
        }

        var streak = await stats.RegisterReviewDayAsync(Math.Max(0, CompletedCards)).ConfigureAwait(false);
        var xp = _xpEarned + (total > 0 ? 20 : 0);
        await stats.AddXpAsync(xp).ConfigureAwait(false);
        await stats.EvaluateAchievementsAsync().ConfigureAwait(false);

        var result = new SessionResult
        {
            TotalCards = Math.Max(0, CompletedCards),
            CorrectAnswers = correct,
            IncorrectAnswers = incorrect,
            Duration = DateTime.UtcNow - _startedAt,
            XpEarned = xp,
            SubjectName = subjectName,
            StreakDays = streak
        };

        Reset();
        return result;
    }

    public void Reset()
    {
        _queue.Clear();
        IsActive = false;
        SessionId = 0;
        CompletedCards = 0;
        CorrectCount = 0;
        IncorrectCount = 0;
        _xpEarned = 0;
    }

    private void EnsureActive()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Aucune session de révision active.");
        }
    }
}
