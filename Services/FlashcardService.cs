using FlashRevise.Data.Repositories;
using FlashRevise.Models;

namespace FlashRevise.Services;

/// <inheritdoc cref="IFlashcardService"/>
public sealed class FlashcardService(
    IFlashcardRepository cards,
    IReviewScheduleRepository schedules,
    ISyncService sync) : IFlashcardService
{
    public Task<List<Flashcard>> GetBySubjectAsync(int subjectId) => cards.GetBySubjectAsync(subjectId);

    public Task<Flashcard?> GetAsync(int id) => cards.GetAsync(id);

    public async Task<int> SaveAsync(Flashcard card)
    {
        var isNew = card.Id == 0;
        var id = await cards.SaveAsync(card).ConfigureAwait(false);
        await sync.EnqueueAsync(nameof(Flashcard), id, isNew ? SyncOperation.Create : SyncOperation.Update, card)
            .ConfigureAwait(false);
        return id;
    }

    public async Task DeleteAsync(int id)
    {
        await cards.DeleteAsync(id).ConfigureAwait(false);
        await sync.EnqueueAsync(nameof(Flashcard), id, SyncOperation.Delete, new { Id = id }).ConfigureAwait(false);
    }

    public async Task<List<Flashcard>> GetDueAsync(int subjectId, int limit, bool randomize = false)
    {
        var dueSchedules = await schedules.GetDueAsync(DateTime.UtcNow).ConfigureAwait(false);
        var ids = dueSchedules
            .Where(s => subjectId == 0 || s.SubjectId == subjectId)
            .Select(s => s.FlashcardId)
            .ToHashSet();

        if (ids.Count == 0)
        {
            return [];
        }

        var subjectCards = subjectId == 0
            ? await cards.GetRandomAsync(int.MaxValue).ConfigureAwait(false)
            : await cards.GetBySubjectAsync(subjectId).ConfigureAwait(false);

        var due = subjectCards.Where(c => ids.Contains(c.Id)).ToList();

        if (randomize)
        {
            due = due.OrderBy(_ => Random.Shared.Next()).ToList();
        }

        return limit > 0 ? due.Take(limit).ToList() : due;
    }

    public async Task<int> SaveGeneratedAsync(
        int subjectId,
        IEnumerable<AiGeneratedCard> generated,
        CancellationToken cancellationToken = default)
    {
        var saved = 0;
        foreach (var card in generated.Where(c => c.IsAccepted))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(card.Question) || string.IsNullOrWhiteSpace(card.Answer))
            {
                continue;
            }

            await SaveAsync(new Flashcard
            {
                SubjectId = subjectId,
                Question = card.Question.Trim(),
                Answer = card.Answer.Trim(),
                Hint = card.Hint.Trim(),
                Difficulty = card.Difficulty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ConfigureAwait(false);
            saved++;
        }

        return saved;
    }

    public Task<int> CountAllAsync() => cards.CountAllAsync();

    public async Task<int> CountDueAsync(int subjectId = 0)
    {
        if (subjectId == 0)
        {
            return await schedules.CountDueAsync(DateTime.UtcNow).ConfigureAwait(false);
        }

        var subjectSchedules = await schedules.GetBySubjectAsync(subjectId).ConfigureAwait(false);
        return subjectSchedules.Count(s => s.NextReviewAt <= DateTime.UtcNow);
    }
}
