using SQLite;

namespace FlashRevise.Models;

/// <summary>Profil unique de l'utilisateur (ligne id = 1).</summary>
public class UserProfile
{
    [PrimaryKey]
    public int Id { get; set; } = 1;

    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Chemin local de la photo de profil copiée dans les données de l'application.</summary>
    [MaxLength(260)]
    public string AvatarPath { get; set; } = string.Empty;

    public int DailyGoal { get; set; } = 30;

    public int Streak { get; set; }

    public int LongestStreak { get; set; }

    public DateTime? LastReviewDate { get; set; }

    public int TotalXp { get; set; }
}
