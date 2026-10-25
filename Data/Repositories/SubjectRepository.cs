using FlashRevise.Data;
using FlashRevise.Models;

namespace FlashRevise.Data.Repositories;

/// <inheritdoc cref="ISubjectRepository"/>
public sealed class SubjectRepository(FlashReviseDatabase database) : ISubjectRepository
{
    public async Task<List<Subject>> GetActiveAsync()
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<Subject>()
            .Where(s => s.ArchivedAt == null)
            .OrderBy(s => s.Name)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<List<Subject>> GetArchivedAsync()
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<Subject>()
            .Where(s => s.ArchivedAt != null)
            .OrderBy(s => s.Name)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<Subject?> GetAsync(int id)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.FindAsync<Subject>(id).ConfigureAwait(false);
    }

    public async Task<int> SaveAsync(Subject subject)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        if (subject.Id == 0)
        {
            await db.InsertAsync(subject).ConfigureAwait(false);
        }
        else
        {
            await db.UpdateAsync(subject).ConfigureAwait(false);
        }

        return subject.Id;
    }

    public async Task SetArchivedAsync(int id, bool archived)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        await db.ExecuteAsync(
            "UPDATE Subject SET ArchivedAt = ? WHERE Id = ?",
            archived ? DateTime.UtcNow : (object?)null,
            id).ConfigureAwait(false);
    }

    public async Task DeleteAsync(int id)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        await db.ExecuteAsync("DELETE FROM ReviewSchedule WHERE SubjectId = ?", id).ConfigureAwait(false);
        await db.ExecuteAsync("DELETE FROM Flashcard WHERE SubjectId = ?", id).ConfigureAwait(false);
        await db.ExecuteAsync("DELETE FROM Reminder WHERE SubjectId = ?", id).ConfigureAwait(false);
        await db.ExecuteAsync("DELETE FROM Subject WHERE Id = ?", id).ConfigureAwait(false);
    }

    public async Task<int> CountActiveAsync()
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<Subject>().Where(s => s.ArchivedAt == null).CountAsync().ConfigureAwait(false);
    }
}
