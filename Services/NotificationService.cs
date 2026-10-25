using FlashRevise.Models;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using Plugin.LocalNotification.EventArgs;

namespace FlashRevise.Services;

/// <inheritdoc cref="INotificationService"/>
public sealed class NotificationService : INotificationService
{
    private const int NotificationIdOffset = 1000;

    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
        LocalNotificationCenter.Current.NotificationActionTapped += OnNotificationActionTapped;
    }

    public event EventHandler<int>? ReminderTapped;

    public async Task<bool> RequestPermissionAsync()
    {
        try
        {
            return await LocalNotificationCenter.Current.RequestNotificationPermission().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Permission de notification refusée ou indisponible");
            return false;
        }
    }

    public Task ScheduleReminderAsync(Reminder reminder, string subjectName)
    {
        if (!reminder.IsEnabled)
        {
            return CancelReminderAsync(reminder.NotificationId);
        }

        var notificationId = reminder.Id + NotificationIdOffset;
        reminder.NotificationId = notificationId;

        var notifyTime = NextOccurrence(reminder.Time, reminder.RepeatPattern);
        var request = new NotificationRequest
        {
            NotificationId = notificationId,
            Title = "FlashRevise · C'est l'heure de réviser",
            Description = $"Ta série t'attend : quelques minutes sur « {subjectName} » suffisent.",
            ReturningData = reminder.SubjectId.ToString(),
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = notifyTime,
                RepeatType = MapRepeatType(reminder.RepeatPattern)
            }
        };

        LocalNotificationCenter.Current.Show(request);
        return Task.CompletedTask;
    }

    public Task CancelReminderAsync(int notificationId)
    {
        if (notificationId <= 0)
        {
            return Task.CompletedTask;
        }

        try
        {
            LocalNotificationCenter.Current.Cancel(notificationId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Annulation de la notification {Id} impossible", notificationId);
        }

        return Task.CompletedTask;
    }

    public async Task RescheduleAllAsync(IEnumerable<(Reminder Reminder, string SubjectName)> reminders)
    {
        foreach (var (reminder, subjectName) in reminders)
        {
            await ScheduleReminderAsync(reminder, subjectName).ConfigureAwait(false);
        }
    }

    private void OnNotificationActionTapped(NotificationActionEventArgs e)
    {
        var payload = e.Request?.ReturningData;
        if (int.TryParse(payload, out var subjectId))
        {
            ReminderTapped?.Invoke(this, subjectId);
        }
    }

    private static DateTimeOffset NextOccurrence(TimeSpan time, RepeatPattern pattern)
    {
        var now = DateTimeOffset.Now;
        var candidate = new DateTimeOffset(now.Year, now.Month, now.Day, time.Hours, time.Minutes, 0, now.Offset);
        if (candidate <= now)
        {
            candidate = pattern == RepeatPattern.Weekly ? candidate.AddDays(7) : candidate.AddDays(1);
        }

        return candidate;
    }

    /// <summary>
    /// Le plugin ne gère nativement que quotidien / hebdomadaire : le mode « jours ouvrés »
    /// est planifié quotidiennement et filtré côté notification (voir README, limites connues).
    /// </summary>
    private static NotificationRepeat MapRepeatType(RepeatPattern pattern) => pattern switch
    {
        RepeatPattern.Weekly => NotificationRepeat.Weekly,
        _ => NotificationRepeat.Daily
    };
}
