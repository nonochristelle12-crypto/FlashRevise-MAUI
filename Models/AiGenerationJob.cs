using SQLite;

namespace FlashRevise.Models;

/// <summary>Job de génération de flashcards par l'IA, persisté pour survivre à un redémarrage.</summary>
public class AiGenerationJob
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public AiSourceType SourceType { get; set; } = AiSourceType.Text;

    [MaxLength(500)]
    public string SourcePath { get; set; } = string.Empty;

    public AiJobStatus Status { get; set; } = AiJobStatus.Queued;

    /// <summary>Cartes générées, sérialisées en JSON.</summary>
    public string ResultJson { get; set; } = "[]";

    [MaxLength(400)]
    public string? ErrorMessage { get; set; }

    public int RequestedCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
