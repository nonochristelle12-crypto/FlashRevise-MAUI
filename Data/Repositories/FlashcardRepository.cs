using FlashRevise.Data;
using FlashRevise.Models;

namespace FlashRevise.Data.Repositories;

/// <inheritdoc cref="IFlashcardRepository"/>
public sealed class FlashcardRepository(FlashReviseDatabase database) : IFlashcardRepository
{
    public async Task<List<Flashcard>> GetBySubjectAsync(int subjectId)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<Flashcard>()
            .Where(c => c.SubjectId == subjectId)
            .OrderBy(c => c.Id)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<List<Flashcard>> SearchAsync(string term)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(term))
        {
            return await db.Table<Flashcard>().ToListAsync().ConfigureAwait(false);
        }

        var pattern = $"%{term.Trim()}%";
        return await db.QueryAsync<Flashcard>(
            "SELECT * FROM Flashcard WHERE Question LIKE ? OR Answer LIKE ? OR Tags LIKE ?",
            pattern, pattern, pattern).ConfigureAwait(false);
    }

    public async Task<Flashcard?> GetAsync(int id)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.FindAsync<Flashcard>(id).ConfigureAwait(false);
    }

    public async Task<int> SaveAsync(Flashcard card)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        card.UpdatedAt = DateTime.UtcNow;
        if (card.Id == 0)
        {
            await db.InsertAsync(card).ConfigureAwait(false);
            var schedule = new ReviewSchedule
            {
                FlashcardId = card.Id,
                SubjectId = card.SubjectId,
                NextReviewAt = DateTime.UtcNow,
                EaseFactor = 2.5,
                IntervalDays = 0,
                Repetitions = 0
            };
            await db.InsertAsync(schedule).ConfigureAwait(false);
        }
        else
        {
            await db.UpdateAsync(card).ConfigureAwait(false);
        }

        return card.Id;
    }

    public async Task DeleteAsync(int id)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        await db.ExecuteAsync("DELETE FROM ReviewSchedule WHERE FlashcardId = ?", id).ConfigureAwait(false);
        await db.ExecuteAsync("DELETE FROM ReviewLog WHERE FlashcardId = ?", id).ConfigureAwait(false);
        await db.ExecuteAsync("DELETE FROM Flashcard WHERE Id = ?", id).ConfigureAwait(false);
    }

    public async Task<int> CountBySubjectAsync(int subjectId)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<Flashcard>().Where(c => c.SubjectId == subjectId).CountAsync().ConfigureAwait(false);
    }

    public async Task<int> CountAllAsync()
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<Flashcard>().CountAsync().ConfigureAwait(false);
    }

    public async Task<List<Flashcard>> GetRandomAsync(int count)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.QueryAsync<Flashcard>(
            "SELECT * FROM Flashcard ORDER BY RANDOM() LIMIT ?", count).ConfigureAwait(false);
    }
}
