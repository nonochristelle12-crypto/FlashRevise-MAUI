using SQLite;

namespace FlashRevise.Models;

/// <summary>État SM-2 d'une carte : prochaine échéance, intervalle, facteur de facilité.</summary>
public class ReviewSchedule
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Unique = true)]
    public int FlashcardId { get; set; }

    [Indexed]
    public int SubjectId { get; set; }

    public DateTime NextReviewAt { get; set; } = DateTime.UtcNow;

    public int IntervalDays { get; set; }

    public double EaseFactor { get; set; } = 2.5;

    public int Repetitions { get; set; }

    public int Lapses { get; set; }

    public DateTime? LastReviewedAt { get; set; }
}
