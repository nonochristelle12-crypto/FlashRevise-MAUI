using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Models;
using FlashRevise.Services;

namespace FlashRevise.ViewModels;

/// <summary>Livre affiché avec sa progression.</summary>
public partial class BookRow : ObservableObject
{
    public BookRow(Book book) => Book = book;

    public Book Book { get; }

    public string Title => Book.Title;

    public double Ratio => Book.Progress;

    public string ProgressLabel => $"{Math.Round(Book.Progress * 100)} %";

    public string StatusLabel => Book.Progress >= 1
        ? "Terminé"
        : Book.Progress <= 0
            ? "Pas commencé"
            : "En cours";

    public void Refresh()
    {
        OnPropertyChanged(nameof(Ratio));
        OnPropertyChanged(nameof(ProgressLabel));
        OnPropertyChanged(nameof(StatusLabel));
    }
}

/// <summary>Objectif quotidien et bibliothèque de livres suivis.</summary>
public partial class GoalsBooksViewModel : BaseViewModel
{
    private readonly IBookService _books;
    private readonly IStatsService _stats;

    [ObservableProperty]
    public partial string DailyGoalText { get; set; } = "30";

    [ObservableProperty]
    public partial string NewBookTitle { get; set; } = string.Empty;

    public GoalsBooksViewModel(IBookService books, IStatsService stats)
    {
        _books = books;
        _stats = stats;
        Title = "Objectifs et livres";
    }

    public ObservableCollection<BookRow> Books { get; } = [];

    public bool HasBooks => Books.Count > 0;

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
            var profile = await _stats.GetProfileAsync();
            DailyGoalText = profile.DailyGoal.ToString();

            var books = await _books.GetAllAsync();
            Books.Clear();
            foreach (var book in books)
            {
                Books.Add(new BookRow(book));
            }

            OnPropertyChanged(nameof(HasBooks));
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
    private async Task SaveGoalAsync()
    {
        ClearError();
        try
        {
            var profile = await _stats.GetProfileAsync();
            profile.DailyGoal = int.TryParse(DailyGoalText, out var goal)
                ? Math.Clamp(goal, 1, 500)
                : profile.DailyGoal;
            await _stats.SaveProfileAsync(profile);
            DailyGoalText = profile.DailyGoal.ToString();
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    [RelayCommand]
    private async Task AddBookAsync()
    {
        var title = NewBookTitle.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            ErrorMessage = "Donne un titre au livre avant de l'ajouter.";
            return;
        }

        ClearError();
        try
        {
            var book = new Book { Title = title, Progress = 0, CreatedAt = DateTime.UtcNow };
            await _books.SaveAsync(book);
            Books.Add(new BookRow(book));
            NewBookTitle = string.Empty;
            OnPropertyChanged(nameof(HasBooks));
            await _stats.EvaluateAchievementsAsync();
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    [RelayCommand]
    private Task IncrementAsync(BookRow? row) => UpdateProgressAsync(row, 0.1);

    [RelayCommand]
    private Task DecrementAsync(BookRow? row) => UpdateProgressAsync(row, -0.1);

    private async Task UpdateProgressAsync(BookRow? row, double step)
    {
        if (row is null)
        {
            return;
        }

        ClearError();
        try
        {
            var progress = await _books.UpdateProgressAsync(row.Book.Id, row.Book.Progress + step);
            row.Book.Progress = progress;
            row.Refresh();
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(BookRow? row)
    {
        if (row is null)
        {
            return;
        }

        ClearError();
        try
        {
            await _books.DeleteAsync(row.Book.Id);
            Books.Remove(row);
            OnPropertyChanged(nameof(HasBooks));
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    [RelayCommand]
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
