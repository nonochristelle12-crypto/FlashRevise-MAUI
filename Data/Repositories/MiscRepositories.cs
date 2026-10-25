using FlashRevise.Data;
using FlashRevise.Models;

namespace FlashRevise.Data.Repositories;

/// <inheritdoc cref="IReminderRepository"/>
public sealed class ReminderRepository(FlashReviseDatabase database) : IReminderRepository
{
    public async Task<List<Reminder>> GetAllAsync()
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<Reminder>().OrderBy(r => r.Time).ToListAsync().ConfigureAwait(false);
    }

    public async Task<Reminder?> GetAsync(int id)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.FindAsync<Reminder>(id).ConfigureAwait(false);
    }

    public async Task<int> SaveAsync(Reminder reminder)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        if (reminder.Id == 0)
        {
            await db.InsertAsync(reminder).ConfigureAwait(false);
        }
        else
        {
            await db.UpdateAsync(reminder).ConfigureAwait(false);
        }

        return reminder.Id;
    }

    public async Task DeleteAsync(int id)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        await db.DeleteAsync<Reminder>(id).ConfigureAwait(false);
    }
}

/// <inheritdoc cref="IUserProfileRepository"/>
public sealed class UserProfileRepository(FlashReviseDatabase database) : IUserProfileRepository
{
    public async Task<UserProfile> GetOrCreateAsync()
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        var profile = await db.FindAsync<UserProfile>(1).ConfigureAwait(false);
        if (profile is null)
        {
            profile = new UserProfile { Id = 1, DailyGoal = 30 };
            await db.InsertAsync(profile).ConfigureAwait(false);
        }

        return profile;
    }

    public async Task<int> SaveAsync(UserProfile profile)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        await db.UpdateAsync(profile).ConfigureAwait(false);
        return profile.Id;
    }
}

/// <inheritdoc cref="IBookRepository"/>
public sealed class BookRepository(FlashReviseDatabase database) : IBookRepository
{
    public async Task<List<Book>> GetAllAsync()
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<Book>().OrderBy(b => b.Title).ToListAsync().ConfigureAwait(false);
    }

    public async Task<int> SaveAsync(Book book)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        if (book.Id == 0)
        {
            await db.InsertAsync(book).ConfigureAwait(false);
        }
        else
        {
            await db.UpdateAsync(book).ConfigureAwait(false);
        }

        return book.Id;
    }

    public async Task DeleteAsync(int id)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        await db.DeleteAsync<Book>(id).ConfigureAwait(false);
    }
}

/// <inheritdoc cref="IAiGenerationJobRepository"/>
public sealed class AiGenerationJobRepository(FlashReviseDatabase database) : IAiGenerationJobRepository
{
    public async Task<int> SaveAsync(AiGenerationJob job)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        if (job.Id == 0)
        {
            await db.InsertAsync(job).ConfigureAwait(false);
        }
        else
        {
            await db.UpdateAsync(job).ConfigureAwait(false);
        }

        return job.Id;
    }

    public async Task<AiGenerationJob?> GetAsync(int id)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.FindAsync<AiGenerationJob>(id).ConfigureAwait(false);
    }

    public async Task<List<AiGenerationJob>> GetRecentAsync(int count)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<AiGenerationJob>()
            .OrderByDescending(j => j.CreatedAt)
            .Take(count)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task DeleteAsync(int id)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        await db.DeleteAsync<AiGenerationJob>(id).ConfigureAwait(false);
    }
}

/// <inheritdoc cref="IAchievementRepository"/>
public sealed class AchievementRepository(FlashReviseDatabase database) : IAchievementRepository
{
    public async Task<List<Achievement>> GetAllAsync()
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<Achievement>().ToListAsync().ConfigureAwait(false);
    }

    public async Task<bool> UnlockAsync(string code)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        var existing = await db.Table<Achievement>()
            .Where(a => a.Code == code)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return false;
        }

        await db.InsertAsync(new Achievement { Code = code, UnlockedAt = DateTime.UtcNow }).ConfigureAwait(false);
        return true;
    }
}

/// <inheritdoc cref="ISyncQueueRepository"/>
public sealed class SyncQueueRepository(FlashReviseDatabase database) : ISyncQueueRepository
{
    public async Task EnqueueAsync(SyncQueueItem item)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        await db.InsertAsync(item).ConfigureAwait(false);
    }

    public async Task<List<SyncQueueItem>> GetAllAsync()
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<SyncQueueItem>().OrderBy(i => i.QueuedAt).ToListAsync().ConfigureAwait(false);
    }

    public async Task<int> CountAsync()
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<SyncQueueItem>().CountAsync().ConfigureAwait(false);
    }

    public async Task RemoveAsync(int id)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        await db.DeleteAsync<SyncQueueItem>(id).ConfigureAwait(false);
    }

    public async Task MarkAttemptAsync(int id, string? error)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        await db.ExecuteAsync(
            "UPDATE SyncQueueItem SET Attempts = Attempts + 1, LastError = ? WHERE Id = ?",
            error, id).ConfigureAwait(false);
    }
}
