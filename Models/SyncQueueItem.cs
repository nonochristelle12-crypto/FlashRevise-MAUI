using SQLite;

namespace FlashRevise.Models;

/// <summary>Opération en attente de synchronisation lorsque l'appareil est hors-ligne.</summary>
public class SyncQueueItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [MaxLength(60)]
    public string Entity { get; set; } = string.Empty;

    public int EntityId { get; set; }

    public SyncOperation Operation { get; set; }

    /// <summary>Charge utile JSON de l'entité au moment de la mise en file.</summary>
    public string Payload { get; set; } = string.Empty;

    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;

    public int Attempts { get; set; }

    [MaxLength(300)]
    public string? LastError { get; set; }
}
