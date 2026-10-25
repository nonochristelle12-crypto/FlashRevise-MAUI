using FlashRevise.Data.Repositories;
using FlashRevise.Models;

namespace FlashRevise.Services;

/// <inheritdoc cref="IReminderService"/>
public sealed class ReminderService(
    IReminderRepository reminders,
    ISubjectRepository subjects,
    INotificationService notifications) : IReminderService
{
    public Task<List<Reminder>> GetAllAsync() => reminders.GetAllAsync();

    public Task<Reminder?> GetAsync(int id) => reminders.GetAsync(id);

    public async Task<int> SaveAsync(Reminder reminder)
    {
        var id = await reminders.SaveAsync(reminder).ConfigureAwait(false);
        var subject = await subjects.GetAsync(reminder.SubjectId).ConfigureAwait(false);
        await notifications.ScheduleReminderAsync(reminder, subject?.Name ?? "ta matière").ConfigureAwait(false);
        await reminders.SaveAsync(reminder).ConfigureAwait(false);
        return id;
    }

    public async Task DeleteAsync(int id)
    {
        var reminder = await reminders.GetAsync(id).ConfigureAwait(false);
        if (reminder is not null)
        {
            await notifications.CancelReminderAsync(reminder.NotificationId).ConfigureAwait(false);
        }

        await reminders.DeleteAsync(id).ConfigureAwait(false);
    }

    public async Task ToggleAsync(int id, bool enabled)
    {
        var reminder = await reminders.GetAsync(id).ConfigureAwait(false);
        if (reminder is null)
        {
            return;
        }

        reminder.IsEnabled = enabled;
        await SaveAsync(reminder).ConfigureAwait(false);
    }

    public async Task RestoreAsync()
    {
        await notifications.RequestPermissionAsync().ConfigureAwait(false);
        var active = (await reminders.GetAllAsync().ConfigureAwait(false)).Where(r => r.IsEnabled);
        var pairs = new List<(Reminder, string)>();
        foreach (var reminder in active)
        {
            var subject = await subjects.GetAsync(reminder.SubjectId).ConfigureAwait(false);
            pairs.Add((reminder, subject?.Name ?? "ta matière"));
        }

        await notifications.RescheduleAllAsync(pairs).ConfigureAwait(false);
    }
}
