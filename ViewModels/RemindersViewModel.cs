using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Models;
using FlashRevise.Services;

namespace FlashRevise.ViewModels;

/// <summary>Rappel affiché dans la liste des notifications planifiées.</summary>
public partial class ReminderRow : ObservableObject
{
    public ReminderRow(Reminder reminder, string subjectName)
    {
        Reminder = reminder;
        SubjectName = subjectName;
    }

    public Reminder Reminder { get; }

    public string SubjectName { get; }

    public string TimeLabel => Reminder.Time.ToString(@"hh\:mm");

    public string RepeatLabel => Reminder.RepeatPattern switch
    {
        RepeatPattern.Weekly => "Chaque semaine",
        RepeatPattern.Weekdays => "Jours ouvrés",
        _ => "Chaque jour"
    };

    public string StatusLabel => IsEnabled ? "Actif" : "En pause";

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    partial void OnIsEnabledChanged(bool value) => OnPropertyChanged(nameof(StatusLabel));
}

/// <summary>Liste des rappels de révision, avec activation rapide.</summary>
public partial class RemindersViewModel : BaseViewModel
{
    private readonly IReminderService _reminders;
    private readonly ISubjectService _subjects;
    private readonly INotificationService _notifications;

    public RemindersViewModel(
        IReminderService reminders,
        ISubjectService subjects,
        INotificationService notifications)
    {
        _reminders = reminders;
        _subjects = subjects;
        _notifications = notifications;
        Title = "Rappels";
    }

    public ObservableCollection<ReminderRow> Rows { get; } = [];

    public bool HasReminders => Rows.Count > 0;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ClearError();
        try
        {
            var subjects = await _subjects.GetSubjectsAsync(includeArchived: true);
            var byId = subjects.ToDictionary(s => s.Id, s => s.Name);
            var reminders = await _reminders.GetAllAsync();

            Rows.Clear();
            foreach (var reminder in reminders.OrderBy(r => r.Time))
            {
                var name = byId.TryGetValue(reminder.SubjectId, out var subjectName)
                    ? subjectName
                    : "Matière supprimée";
                Rows.Add(new ReminderRow(reminder, name) { IsEnabled = reminder.IsEnabled });
            }

            OnPropertyChanged(nameof(HasReminders));
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SetEnabledAsync(ReminderRow row, bool enabled)
    {
        if (row.Reminder.IsEnabled == enabled)
        {
            return;
        }

        ClearError();
        try
        {
            if (enabled)
            {
                var granted = await _notifications.RequestPermissionAsync();
                if (!granted)
                {
                    ErrorMessage = "Les notifications sont refusées par le système : autorise-les pour activer ce rappel.";
                    row.IsEnabled = false;
                    return;
                }
            }

            await _reminders.ToggleAsync(row.Reminder.Id, enabled);
            row.Reminder.IsEnabled = enabled;
            row.IsEnabled = enabled;
        }
        catch (Exception ex)
        {
            row.IsEnabled = row.Reminder.IsEnabled;
            ReportError(ex);
        }
    }

    [RelayCommand]
    private async Task EditAsync(ReminderRow? row)
    {
        if (row is null)
        {
            return;
        }

        await Shell.Current.GoToAsync($"{Routes.ScheduleRevision}?subjectId={row.Reminder.SubjectId}");
    }

    [RelayCommand]
    private async Task DeleteAsync(ReminderRow? row)
    {
        if (row is null)
        {
            return;
        }

        try
        {
            await _reminders.DeleteAsync(row.Reminder.Id);
            Rows.Remove(row);
            OnPropertyChanged(nameof(HasReminders));
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    [RelayCommand]
    private static Task AddAsync() => Shell.Current.GoToAsync(Routes.ScheduleRevision);

    [RelayCommand]
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
