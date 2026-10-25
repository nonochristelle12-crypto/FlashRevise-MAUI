using SQLite;

namespace FlashRevise.Models;

/// <summary>Rappel planifié pour une matière.</summary>
public class Reminder
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SubjectId { get; set; }

    /// <summary>Heure locale de déclenchement.</summary>
    public TimeSpan Time { get; set; } = new(19, 0, 0);

    public RepeatPattern RepeatPattern { get; set; } = RepeatPattern.Daily;

    public bool IsEnabled { get; set; } = true;

    public DateTime? LastFiredAt { get; set; }

    /// <summary>Identifiant retourné par le plugin de notifications, pour pouvoir annuler.</summary>
    public int NotificationId { get; set; }
}
