using SQLite;

namespace FlashRevise.Models;

/// <summary>Carte de révision recto/verso.</summary>
public class Flashcard
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SubjectId { get; set; }

    public string Question { get; set; } = string.Empty;

    public string Answer { get; set; } = string.Empty;

    /// <summary>Astuce mnémotechnique optionnelle (champ « tip » des maquettes).</summary>
    public string Hint { get; set; } = string.Empty;

    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Medium;

    /// <summary>Tags séparés par des virgules, tel que stocké par sqlite-net.</summary>
    [MaxLength(300)]
    public string Tags { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? ImagePath { get; set; }

    [Ignore]
    public IReadOnlyList<string> TagList => string.IsNullOrWhiteSpace(Tags)
        ? Array.Empty<string>()
        : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
