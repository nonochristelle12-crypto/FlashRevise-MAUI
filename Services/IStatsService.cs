using FlashRevise.Models;

namespace FlashRevise.Services;

/// <summary>Agrégations de progression : tableau de bord, séries, maîtrise, succès.</summary>
public interface IStatsService
{
    Task<DashboardSnapshot> GetDashboardAsync();
    Task<List<SubjectStats>> GetSubjectsStatsAsync();
    Task<SubjectStats?> GetSubjectStatsAsync(int subjectId);
    Task<List<DailyPoint>> GetActivityAsync(int days);
    Task<UserProfile> GetProfileAsync();
    Task SaveProfileAsync(UserProfile profile);

    /// <summary>Met à jour la série de jours consécutifs et renvoie la valeur courante.</summary>
    Task<int> RegisterReviewDayAsync(int cardsReviewed);

    Task AddXpAsync(int xp);
    Task EvaluateAchievementsAsync();
    Task<List<AchievementItem>> GetAchievementsAsync();
}
