using SQLite;

namespace FlashRevise.Models;

/// <summary>Trace d'une révision de carte, utilisée pour les stats et le streak.</summary>
public class ReviewLog
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int FlashcardId { get; set; }

    [Indexed]
    public int SubjectId { get; set; }

    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;

    public bool KnewIt { get; set; }

    public int DurationMs { get; set; }

    /// <summary>Qualité SM-2 (1 à 5) dérivée du choix de l'utilisateur.</summary>
    public RecallQuality Quality { get; set; } = RecallQuality.Good;
}
