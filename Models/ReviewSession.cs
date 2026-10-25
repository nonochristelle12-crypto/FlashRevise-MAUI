using SQLite;

namespace FlashRevise.Models;

/// <summary>Session de révision d'une matière, du premier au dernier geste.</summary>
public class ReviewSession
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SubjectId { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? EndedAt { get; set; }

    public int CardsReviewed { get; set; }

    public int CorrectCount { get; set; }

    public int IncorrectCount { get; set; }

    [Ignore]
    public int DurationSeconds => EndedAt is null
        ? 0
        : (int)(EndedAt.Value - StartedAt).TotalSeconds;

    [Ignore]
    public double SuccessRate => CardsReviewed == 0 ? 0 : (double)CorrectCount / CardsReviewed;
}
