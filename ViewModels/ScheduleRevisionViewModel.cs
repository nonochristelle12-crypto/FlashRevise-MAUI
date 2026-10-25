using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Models;
using FlashRevise.Services;

namespace FlashRevise.ViewModels;

/// <summary>Planification d'un rappel de révision récurrent pour une matière.</summary>
public partial class ScheduleRevisionViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IReminderService _reminders;
    private readonly ISubjectService _subjects;
    private readonly INotificationService _notifications;

    private Reminder? _existing;
    private bool _loadingSelection;
    private int _requestedSubjectId;

    [ObservableProperty]
    public partial int SelectedSubjectIndex { get; set; }

    [ObservableProperty]
    public partial int RepeatIndex { get; set; }

    [ObservableProperty]
    public partial TimeSpan Time { get; set; } = new(19, 0, 0);

    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = true;

    [ObservableProperty]
    public partial bool IsExisting { get; set; }

    public ScheduleRevisionViewModel(
        IReminderService reminders,
        ISubjectService subjects,
        INotificationService notifications)
    {
        _reminders = reminders;
        _subjects = subjects;
        _notifications = notifications;
        Title = "Planifier un rappel";
    }

    public ObservableCollection<Subject> Subjects { get; } = [];

    public ObservableCollection<string> SubjectNames { get; } = [];

    public IReadOnlyList<string> RepeatLabels { get; } = ["Chaque jour", "Chaque semaine"];

    public bool HasSubjects => Subjects.Count > 0;

    public string PreviewLabel
    {
        get
        {
            var pattern = RepeatIndex == 1 ? "Chaque semaine" : "Chaque jour";
            return $"{pattern} à {Time.Hours:00}:{Time.Minutes:00}";
        }
    }

    public string TargetLabel => SelectedSubjectIndex >= 0 && SelectedSubjectIndex < Subjects.Count
        ? Subjects[SelectedSubjectIndex].Name
        : "Aucune matière sélectionnée";

    partial void OnTimeChanged(TimeSpan value) => OnPropertyChanged(nameof(PreviewLabel));

    partial void OnRepeatIndexChanged(int value) => OnPropertyChanged(nameof(PreviewLabel));

    partial void OnSelectedSubjectIndexChanged(int value)
    {
        OnPropertyChanged(nameof(TargetLabel));
        if (!_loadingSelection)
        {
            _ = LoadReminderAsync();
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _requestedSubjectId = query.TryGetValue("subjectId", out var raw) && raw is not null
            ? Convert.ToInt32(raw)
            : 0;

        _ = LoadAsync();
    }

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
            var subjects = await _subjects.GetSubjectsAsync();
            _loadingSelection = true;
            Subjects.Clear();
            SubjectNames.Clear();
            foreach (var subject in subjects)
            {
                Subjects.Add(subject);
                SubjectNames.Add(subject.Name);
            }

            var index = _requestedSubjectId > 0
                ? Subjects.ToList().FindIndex(s => s.Id == _requestedSubjectId)
                : 0;
            SelectedSubjectIndex = index >= 0 ? index : 0;
            _loadingSelection = false;

            OnPropertyChanged(nameof(HasSubjects));
            OnPropertyChanged(nameof(TargetLabel));
            await LoadReminderAsync();
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

    private async Task LoadReminderAsync()
    {
        if (SelectedSubjectIndex < 0 || SelectedSubjectIndex >= Subjects.Count)
        {
            _existing = null;
            IsExisting = false;
            return;
        }

        try
        {
            var subjectId = Subjects[SelectedSubjectIndex].Id;
            var all = await _reminders.GetAllAsync();
            _existing = all.FirstOrDefault(r => r.SubjectId == subjectId);
            IsExisting = _existing is not null;

            if (_existing is null)
            {
                Time = new TimeSpan(19, 0, 0);
                RepeatIndex = 0;
                IsEnabled = true;
            }
            else
            {
                Time = _existing.Time;
                RepeatIndex = _existing.RepeatPattern == RepeatPattern.Weekly ? 1 : 0;
                IsEnabled = _existing.IsEnabled;
            }
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ClearError();

        if (Subjects.Count == 0)
        {
            ErrorMessage = "Crée d'abord une matière pour planifier un rappel.";
            return;
        }

        IsBusy = true;
        try
        {
            var subject = Subjects[Math.Clamp(SelectedSubjectIndex, 0, Subjects.Count - 1)];
            var reminder = _existing ?? new Reminder { SubjectId = subject.Id };
            reminder.SubjectId = subject.Id;
            reminder.Time = Time;
            reminder.RepeatPattern = RepeatIndex == 1 ? RepeatPattern.Weekly : RepeatPattern.Daily;
            reminder.IsEnabled = IsEnabled;

            var granted = await _notifications.RequestPermissionAsync();
            await _reminders.SaveAsync(reminder);
            _existing = reminder;
            IsExisting = true;

            if (!granted)
            {
                ErrorMessage = "Rappel enregistré, mais les notifications sont refusées par le système. Autorise-les dans les réglages de l'appareil.";
                return;
            }

            await Shell.Current.GoToAsync("..");
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

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_existing is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _reminders.DeleteAsync(_existing.Id);
            _existing = null;
            IsExisting = false;
            await Shell.Current.GoToAsync("..");
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

    [RelayCommand]
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
