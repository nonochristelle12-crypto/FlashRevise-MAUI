using SQLite;

namespace FlashRevise.Models;

/// <summary>Matière (deck) regroupant des flashcards.</summary>
public class Subject
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(9)]
    public string Color { get; set; } = "#6C4CF1";

    [MaxLength(64)]
    public string Icon { get; set; } = "menu_book";

    [MaxLength(400)]
    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ArchivedAt { get; set; }

    [Ignore]
    public bool IsArchived => ArchivedAt is not null;
}
