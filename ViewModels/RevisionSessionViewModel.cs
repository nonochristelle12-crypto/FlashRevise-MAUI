using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Models;
using FlashRevise.Services;

namespace FlashRevise.ViewModels;

/// <summary>Session de révision : question, révélation, auto-évaluation et enchaînement.</summary>
public partial class RevisionSessionViewModel : BaseViewModel
{
    private readonly IReviewSessionService _session;
    private readonly SessionOutcomeStore _outcomes;
    private readonly Stopwatch _stopwatch = new();
    private IDispatcherTimer? _timer;
    private DateTime _cardShownAt;

    [ObservableProperty]
    public partial string Question { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Answer { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Hint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SubjectName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsAnswerVisible { get; set; }

    [ObservableProperty]
    public partial double ProgressRatio { get; set; }

    [ObservableProperty]
    public partial string ProgressLabel { get; set; } = "0 / 0";

    [ObservableProperty]
    public partial string RemainingLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ElapsedLabel { get; set; } = "00:00";

    [ObservableProperty]
    public partial bool HasHint { get; set; }

    [ObservableProperty]
    public partial bool IsProcessing { get; set; }

    [ObservableProperty]
    public partial bool IsSessionReady { get; set; }

    public RevisionSessionViewModel(IReviewSessionService session, SessionOutcomeStore outcomes)
    {
        _session = session;
        _outcomes = outcomes;
        Title = "Révision";
    }

    public bool IsLastCard => _session.CompletedCards >= _session.TotalCards - 1;

    public void Start()
    {
        if (!_session.IsActive)
        {
            ErrorMessage = "Aucune session active. Relance la configuration de révision.";
            return;
        }

        SubjectName = _session.SubjectName;
        IsSessionReady = true;
        _stopwatch.Restart();
        ShowCard(_session.CurrentCard);
        UpdateProgress();

        _timer ??= CreateTimer();
        _timer?.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _stopwatch.Stop();
    }

    private IDispatcherTimer? CreateTimer()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return null;
        }

        var timer = dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += (_, _) => ElapsedLabel = FormatElapsed(_stopwatch.Elapsed);
        return timer;
    }

    private void ShowCard(Flashcard? card)
    {
        if (card is null)
        {
            Question = string.Empty;
            Answer = string.Empty;
            Hint = string.Empty;
            HasHint = false;
            return;
        }

        Question = card.Question;
        Answer = card.Answer;
        Hint = card.Hint;
        HasHint = !string.IsNullOrWhiteSpace(card.Hint);
        IsAnswerVisible = false;
        _cardShownAt = DateTime.UtcNow;
    }

    private void UpdateProgress()
    {
        var total = Math.Max(1, _session.TotalCards);
        ProgressRatio = Math.Clamp((double)_session.CompletedCards / total, 0, 1);
        ProgressLabel = $"{Math.Min(_session.CompletedCards + 1, _session.TotalCards)} / {_session.TotalCards}";
        RemainingLabel = $"{_session.TotalCards - _session.CompletedCards} restantes";
        OnPropertyChanged(nameof(IsLastCard));
    }

    [RelayCommand]
    private void Flip() => IsAnswerVisible = !IsAnswerVisible;

    [RelayCommand]
    private async Task KnewAsync() => await SubmitAsync(RecallQuality.Good);

    [RelayCommand]
    private async Task DidntKnowAsync() => await SubmitAsync(RecallQuality.Again);

    private async Task SubmitAsync(RecallQuality quality)
    {
        if (IsProcessing || !IsSessionReady)
        {
            return;
        }

        IsProcessing = true;
        ClearError();
        try
        {
            var durationMs = (int)(DateTime.UtcNow - _cardShownAt).TotalMilliseconds;
            var outcome = await _session.SubmitAsync(quality, durationMs);

            if (!outcome.HasNext)
            {
                await CompleteAsync();
                return;
            }

            ShowCard(outcome.NextCard);
            UpdateProgress();
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task SkipAsync()
    {
        if (IsProcessing || !IsSessionReady)
        {
            return;
        }

        IsProcessing = true;
        try
        {
            var outcome = await _session.SkipAsync();
            if (!outcome.HasNext)
            {
                await CompleteAsync();
                return;
            }

            ShowCard(outcome.NextCard);
            UpdateProgress();
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task CompleteAsync()
    {
        Stop();
        var result = await _session.FinishAsync();
        _outcomes.Last = result;
        await Shell.Current.GoToAsync(Routes.SessionSummary);
    }

    [RelayCommand]
    private async Task QuitAsync()
    {
        Stop();
        _session.Reset();
        await Shell.Current.GoToAsync("..");
    }

    private static string FormatElapsed(TimeSpan elapsed) =>
        $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
}
