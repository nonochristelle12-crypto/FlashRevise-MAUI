using Microsoft.Maui.Graphics;

namespace FlashRevise.Models;

/// <summary>Statistiques agrégées d'une matière.</summary>
public sealed class SubjectStats
{
    public int SubjectId { get; init; }
    public string SubjectName { get; init; } = string.Empty;
    public string Color { get; init; } = "#6C4CF1";

    /// <summary>Variante prête à lier, la couleur brute étant stockée en hexadécimal.</summary>
    public Color AccentColor => Microsoft.Maui.Graphics.Color.FromArgb(Color);
    public int TotalCards { get; init; }
    public int DueCards { get; init; }
    public int MasteredCards { get; init; }
    public int ReviewedToday { get; init; }
    public double MasteryRatio => TotalCards == 0 ? 0 : (double)MasteredCards / TotalCards;
    public DateTime? LastReviewedAt { get; init; }
}

/// <summary>Point d'une série temporelle (activité quotidienne, rétention).</summary>
public sealed record DailyPoint(DateTime Date, int Reviews, double Accuracy);

/// <summary>Résumé affiché sur le tableau de bord.</summary>
public sealed class DashboardSnapshot
{
    public string FirstName { get; init; } = string.Empty;
    public int StreakDays { get; init; }
    public int DailyGoal { get; init; }
    public int ReviewedToday { get; init; }
    public int DueToday { get; init; }
    public int UrgentCards { get; init; }
    public int SpacedCards { get; init; }
    public int TotalCards { get; init; }
    public int ActiveSubjects { get; init; }
    public double GlobalMastery { get; init; }
    public double RetentionDelta { get; init; }

    public double GoalRatio => DailyGoal == 0 ? 0 : Math.Clamp((double)ReviewedToday / DailyGoal, 0, 1);
    public int RemainingForGoal => Math.Max(0, DailyGoal - ReviewedToday);
}

/// <summary>Résultat d'une session de révision terminée.</summary>
public sealed class SessionResult
{
    public int TotalCards { get; init; }
    public int CorrectAnswers { get; init; }
    public int IncorrectAnswers { get; init; }
    public TimeSpan Duration { get; init; }
    public int XpEarned { get; init; }
    public string SubjectName { get; init; } = string.Empty;
    public int StreakDays { get; init; }

    public double SuccessRate => TotalCards == 0 ? 0 : (double)CorrectAnswers / TotalCards;
    public double AverageSecondsPerCard => TotalCards == 0 ? 0 : Duration.TotalSeconds / TotalCards;
}
