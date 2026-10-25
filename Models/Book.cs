using SQLite;

namespace FlashRevise.Models;

/// <summary>Livre suivi dans l'objectif d'étude, découpé en chapitres.</summary>
public class Book
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Liste JSON des chapitres (titre + nombre de pages ou de cartes).</summary>
    public string ChaptersJson { get; set; } = "[]";

    /// <summary>Progression de 0 à 1.</summary>
    public double Progress { get; set; }

    public int? ReminderId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
