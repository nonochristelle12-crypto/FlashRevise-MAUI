using SQLite;

namespace FlashRevise.Models;

/// <summary>Succès débloqué par l'utilisateur.</summary>
public class Achievement
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Unique = true), MaxLength(60)]
    public string Code { get; set; } = string.Empty;

    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Définition statique d'un succès (catalogue, non persisté).</summary>
public sealed record AchievementDefinition(string Code, string Title, string Description, string Icon, int Target);

/// <summary>Succès du catalogue avec son état de déblocage.</summary>
public sealed class AchievementItem
{
    public required AchievementDefinition Definition { get; init; }
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedAt { get; set; }
    public int Progress { get; set; }

    public double ProgressRatio => Definition.Target <= 0
        ? (IsUnlocked ? 1 : 0)
        : Math.Clamp((double)Progress / Definition.Target, 0, 1);
}
