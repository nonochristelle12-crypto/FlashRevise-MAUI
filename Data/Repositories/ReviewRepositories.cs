using FlashRevise.Data;
using FlashRevise.Models;

namespace FlashRevise.Data.Repositories;

/// <inheritdoc cref="IReviewLogRepository"/>
public sealed class ReviewLogRepository(FlashReviseDatabase database) : IReviewLogRepository
{
    public async Task<int> AddAsync(ReviewLog log)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        await db.InsertAsync(log).ConfigureAwait(false);
        return log.Id;
    }

    public async Task<List<ReviewLog>> GetSinceAsync(DateTime since)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<ReviewLog>()
            .Where(l => l.ReviewedAt >= since)
            .OrderBy(l => l.ReviewedAt)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<List<ReviewLog>> GetBetweenAsync(DateTime from, DateTime to)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<ReviewLog>()
            .Where(l => l.ReviewedAt >= from && l.ReviewedAt <= to)
            .OrderBy(l => l.ReviewedAt)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<int> CountForDayAsync(DateTime day)
    {
        var from = day.Date;
        var to = from.AddDays(1);
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<ReviewLog>()
            .Where(l => l.ReviewedAt >= from && l.ReviewedAt < to)
            .CountAsync()
            .ConfigureAwait(false);
    }

    public async Task<DateTime?> GetLastReviewAsync()
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        var last = await db.Table<ReviewLog>()
            .OrderByDescending(l => l.ReviewedAt)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        return last?.ReviewedAt;
    }
}

/// <inheritdoc cref="IReviewSessionRepository"/>
public sealed class ReviewSessionRepository(FlashReviseDatabase database) : IReviewSessionRepository
{
    public async Task<int> StartAsync(ReviewSession session)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        await db.InsertAsync(session).ConfigureAwait(false);
        return session.Id;
    }

    public async Task CompleteAsync(ReviewSession session)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        await db.UpdateAsync(session).ConfigureAwait(false);
    }

    public async Task<List<ReviewSession>> GetRecentAsync(int count)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<ReviewSession>()
            .OrderByDescending(s => s.StartedAt)
            .Take(count)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<ReviewSession?> GetAsync(int id)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.FindAsync<ReviewSession>(id).ConfigureAwait(false);
    }
}

/// <inheritdoc cref="IReviewScheduleRepository"/>
public sealed class ReviewScheduleRepository(FlashReviseDatabase database) : IReviewScheduleRepository
{
    public async Task<ReviewSchedule?> GetByFlashcardAsync(int flashcardId)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<ReviewSchedule>()
            .Where(s => s.FlashcardId == flashcardId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
    }

    public async Task<List<ReviewSchedule>> GetDueAsync(DateTime asOf)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<ReviewSchedule>()
            .Where(s => s.NextReviewAt <= asOf)
            .OrderBy(s => s.NextReviewAt)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<List<ReviewSchedule>> GetBySubjectAsync(int subjectId)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<ReviewSchedule>()
            .Where(s => s.SubjectId == subjectId)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<int> SaveAsync(ReviewSchedule schedule)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        if (schedule.Id == 0)
        {
            await db.InsertAsync(schedule).ConfigureAwait(false);
        }
        else
        {
            await db.UpdateAsync(schedule).ConfigureAwait(false);
        }

        return schedule.Id;
    }

    public async Task<int> CountDueAsync(DateTime asOf)
    {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        return await db.Table<ReviewSchedule>()
            .Where(s => s.NextReviewAt <= asOf)
            .CountAsync()
            .ConfigureAwait(false);
    }
}
