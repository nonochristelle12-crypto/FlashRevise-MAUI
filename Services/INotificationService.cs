using FlashRevise.Models;

namespace FlashRevise.Services;

/// <summary>Planification des rappels locaux (quotidiens / hebdomadaires).</summary>
public interface INotificationService
{
    /// <summary>Demande la permission d'afficher des notifications si nécessaire.</summary>
    Task<bool> RequestPermissionAsync();

    /// <summary>Planifie (ou replanifie) le rappel d'une matière.</summary>
    Task ScheduleReminderAsync(Reminder reminder, string subjectName);

    /// <summary>Annule un rappel déjà planifié.</summary>
    Task CancelReminderAsync(int notificationId);

    /// <summary>Annule puis replanifie tous les rappels actifs.</summary>
    Task RescheduleAllAsync(IEnumerable<(Reminder Reminder, string SubjectName)> reminders);

    /// <summary>Déclenché quand l'utilisateur tape une notification de rappel ; transporte l'id de matière.</summary>
    event EventHandler<int>? ReminderTapped;
}
