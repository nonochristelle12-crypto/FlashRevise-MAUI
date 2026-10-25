using FlashRevise.Data.Repositories;
using FlashRevise.Models;

namespace FlashRevise.Services;

/// <inheritdoc cref="IStatsService"/>
public sealed class StatsService(
    ISubjectRepository subjects,
    IFlashcardRepository cards,
    IReviewLogRepository logs,
    IReviewScheduleRepository schedules,
    IReviewSessionRepository sessions,
    IUserProfileRepository profiles,
    IAchievementRepository achievements,
    IAiGenerationJobRepository aiJobs,
    IBookRepository books,
    ISpacedRepetitionService spacedRepetition) : IStatsService
{
    private static readonly AchievementDefinition[] Catalog =
    [
        new("first_session", "Premier pas", "Terminer une première session de révision.", "flag", 1),
        new("streak_7", "Semaine solide", "Réviser 7 jours d'affilée.", "local_fire_department", 7),
        new("streak_30", "Discipline d'acier", "Réviser 30 jours d'affilée.", "bolt", 30),
        new("reviews_100", "Centurion", "Réviser 100 cartes au total.", "military_tech", 100),
        new("mastered_50", "Mémoire longue", "Maîtriser 50 cartes.", "psychology", 50),
        new("ai_first", "Copilote IA", "Générer des cartes avec l'IA.", "auto_awesome", 1),
        new("library_1", "Lecteur", "Suivre un livre dans tes objectifs.", "auto_stories", 1)
    ];

    public async Task<DashboardSnapshot> GetDashboardAsync()
    {
        var profile = await profiles.GetOrCreateAsync().ConfigureAwait(false);
        var activeSubjects = await subjects.GetActiveAsync().ConfigureAwait(false);
        var totalCards = await cards.CountAllAsync().ConfigureAwait(false);
        var reviewedToday = await logs.CountForDayAsync(DateTime.Today).ConfigureAwait(false);
        var due = await schedules.CountDueAsync(DateTime.UtcNow).ConfigureAwait(false);

        var allSchedules = await schedules.GetDueAsync(DateTime.UtcNow.AddYears(100)).ConfigureAwait(false);
        var mastered = allSchedules.Count(s => spacedRepetition.IsMastered(s));
        var urgent = allSchedules.Count(s => s.NextReviewAt <= DateTime.UtcNow && (s.Lapses > 0 || s.Repetitions == 0));

        var recent = await logs.GetSinceAsync(DateTime.UtcNow.AddDays(-14)).ConfigureAwait(false);
        var retentionDelta = ComputeRetentionDelta(recent);

        return new DashboardSnapshot
        {
            FirstName = string.IsNullOrWhiteSpace(profile.Name) ? "toi" : profile.Name,
            StreakDays = profile.Streak,
            DailyGoal = profile.DailyGoal,
            ReviewedToday = reviewedToday,
            DueToday = due,
            UrgentCards = urgent,
            SpacedCards = Math.Max(0, due - urgent),
            TotalCards = totalCards,
            ActiveSubjects = activeSubjects.Count,
            GlobalMastery = totalCards == 0 ? 0 : (double)mastered / totalCards,
            RetentionDelta = retentionDelta
        };
    }

    public async Task<List<SubjectStats>> GetSubjectsStatsAsync()
    {
        var active = await subjects.GetActiveAsync().ConfigureAwait(false);
        var result = new List<SubjectStats>(active.Count);

        foreach (var subject in active)
        {
            var stats = await GetSubjectStatsAsync(subject.Id).ConfigureAwait(false);
            if (stats is not null)
            {
                result.Add(stats);
            }
        }

        return result;
    }

    public async Task<SubjectStats?> GetSubjectStatsAsync(int subjectId)
    {
        var subject = await subjects.GetAsync(subjectId).ConfigureAwait(false);
        if (subject is null)
        {
            return null;
        }

        var subjectCards = await cards.GetBySubjectAsync(subjectId).ConfigureAwait(false);
        var subjectSchedules = await schedules.GetBySubjectAsync(subjectId).ConfigureAwait(false);
        var todayLogs = await logs.GetBetweenAsync(DateTime.Today, DateTime.Today.AddDays(1)).ConfigureAwait(false);

        return new SubjectStats
        {
            SubjectId = subjectId,
            SubjectName = subject.Name,
            Color = subject.Color,
            TotalCards = subjectCards.Count,
            DueCards = subjectSchedules.Count(s => s.NextReviewAt <= DateTime.UtcNow),
            MasteredCards = subjectSchedules.Count(spacedRepetition.IsMastered),
            ReviewedToday = todayLogs.Count(l => l.SubjectId == subjectId),
            LastReviewedAt = subjectSchedules
                .Where(s => s.LastReviewedAt is not null)
                .Select(s => s.LastReviewedAt)
                .Max()
        };
    }

    public async Task<List<DailyPoint>> GetActivityAsync(int days)
    {
        days = Math.Clamp(days, 1, 90);
        var from = DateTime.Today.AddDays(-(days - 1));
        var recent = await logs.GetBetweenAsync(from, DateTime.Now).ConfigureAwait(false);

        return Enumerable.Range(0, days)
            .Select(offset =>
            {
                var day = from.AddDays(offset);
                var dayLogs = recent.Where(l => l.ReviewedAt.Date == day).ToList();
                var accuracy = dayLogs.Count == 0 ? 0 : (double)dayLogs.Count(l => l.KnewIt) / dayLogs.Count;
                return new DailyPoint(day, dayLogs.Count, accuracy);
            })
            .ToList();
    }

    public Task<UserProfile> GetProfileAsync() => profiles.GetOrCreateAsync();

    public Task SaveProfileAsync(UserProfile profile) => profiles.SaveAsync(profile);

    public async Task<int> RegisterReviewDayAsync(int cardsReviewed)
    {
        var profile = await profiles.GetOrCreateAsync().ConfigureAwait(false);
        var today = DateTime.Today;
        var last = profile.LastReviewDate?.Date;

        if (last == today)
        {
            return profile.Streak;
        }

        profile.Streak = last == today.AddDays(-1) ? profile.Streak + 1 : 1;
        profile.LongestStreak = Math.Max(profile.LongestStreak, profile.Streak);
        profile.LastReviewDate = DateTime.Now;
        await profiles.SaveAsync(profile).ConfigureAwait(false);

        _ = cardsReviewed;
        return profile.Streak;
    }

    public async Task AddXpAsync(int xp)
    {
        if (xp <= 0)
        {
            return;
        }

        var profile = await profiles.GetOrCreateAsync().ConfigureAwait(false);
        profile.TotalXp += xp;
        await profiles.SaveAsync(profile).ConfigureAwait(false);
    }

    public async Task EvaluateAchievementsAsync()
    {
        var profile = await profiles.GetOrCreateAsync().ConfigureAwait(false);
        var allLogs = await logs.GetSinceAsync(DateTime.MinValue).ConfigureAwait(false);
        var allSchedules = await schedules.GetDueAsync(DateTime.UtcNow.AddYears(100)).ConfigureAwait(false);
        var mastered = allSchedules.Count(spacedRepetition.IsMastered);
        var sessionCount = (await sessions.GetRecentAsync(1).ConfigureAwait(false)).Count;
        var aiCount = (await aiJobs.GetRecentAsync(50).ConfigureAwait(false)).Count;
        var bookCount = (await books.GetAllAsync().ConfigureAwait(false)).Count;

        await TryUnlockAsync("first_session", sessionCount >= 1).ConfigureAwait(false);
        await TryUnlockAsync("streak_7", profile.Streak >= 7).ConfigureAwait(false);
        await TryUnlockAsync("streak_30", profile.Streak >= 30).ConfigureAwait(false);
        await TryUnlockAsync("reviews_100", allLogs.Count >= 100).ConfigureAwait(false);
        await TryUnlockAsync("mastered_50", mastered >= 50).ConfigureAwait(false);
        await TryUnlockAsync("ai_first", aiCount >= 1).ConfigureAwait(false);
        await TryUnlockAsync("library_1", bookCount >= 1).ConfigureAwait(false);
    }

    public async Task<List<AchievementItem>> GetAchievementsAsync()
    {
        var unlocked = await achievements.GetAllAsync().ConfigureAwait(false);
        var profile = await profiles.GetOrCreateAsync().ConfigureAwait(false);
        var allLogs = await logs.GetSinceAsync(DateTime.MinValue).ConfigureAwait(false);
        var allSchedules = await schedules.GetDueAsync(DateTime.UtcNow.AddYears(100)).ConfigureAwait(false);
        var mastered = allSchedules.Count(spacedRepetition.IsMastered);
        var sessionCount = (await sessions.GetRecentAsync(1).ConfigureAwait(false)).Count;
        var aiCount = (await aiJobs.GetRecentAsync(50).ConfigureAwait(false)).Count;
        var bookCount = (await books.GetAllAsync().ConfigureAwait(false)).Count;

        int ProgressFor(string code) => code switch
        {
            "first_session" => sessionCount,
            "streak_7" or "streak_30" => profile.Streak,
            "reviews_100" => allLogs.Count,
            "mastered_50" => mastered,
            "ai_first" => aiCount,
            "library_1" => bookCount,
            _ => 0
        };

        return Catalog.Select(definition =>
        {
            var record = unlocked.FirstOrDefault(a => a.Code == definition.Code);
            return new AchievementItem
            {
                Definition = definition,
                IsUnlocked = record is not null,
                UnlockedAt = record?.UnlockedAt,
                Progress = ProgressFor(definition.Code)
            };
        }).ToList();
    }

    private async Task TryUnlockAsync(string code, bool condition)
    {
        if (condition)
        {
            await achievements.UnlockAsync(code).ConfigureAwait(false);
        }
    }

    private static double ComputeRetentionDelta(List<ReviewLog> recent)
    {
        var now = DateTime.UtcNow;
        var current = recent.Where(l => l.ReviewedAt >= now.AddDays(-7)).ToList();
        var previous = recent.Where(l => l.ReviewedAt < now.AddDays(-7)).ToList();
        if (current.Count == 0 || previous.Count == 0)
        {
            return 0;
        }

        var currentRate = (double)current.Count(l => l.KnewIt) / current.Count;
        var previousRate = (double)previous.Count(l => l.KnewIt) / previous.Count;
        return Math.Round((currentRate - previousRate) * 100, 0);
    }
}
