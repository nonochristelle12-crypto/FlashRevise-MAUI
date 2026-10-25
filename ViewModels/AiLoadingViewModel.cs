using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Data.Repositories;
using FlashRevise.Models;
using FlashRevise.Services;
using FlashRevise.Services.Ai;

namespace FlashRevise.ViewModels;

/// <summary>Écran d'attente pendant la génération IA ; le job est persisté pour survivre à un redémarrage.</summary>
public partial class AiLoadingViewModel : BaseViewModel
{
    private static readonly string[] Steps =
    [
        "Lecture du support…",
        "Repérage des notions clés…",
        "Rédaction des questions…",
        "Vérification des réponses…",
        "Dernière mise en forme…"
    ];

    private readonly IAiService _ai;
    private readonly IAiGenerationJobRepository _jobs;
    private readonly AiDraftStore _draft;

    private CancellationTokenSource? _cancellation;
    private IDispatcherTimer? _timer;
    private int _stepIndex;
    private int _elapsedSeconds;
    private bool _started;

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial string ProgressCaption { get; set; } = "0%";

    [ObservableProperty]
    public partial string StatusText { get; set; } = Steps[0];

    [ObservableProperty]
    public partial string SubjectName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SourceLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RequestLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ElapsedLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial bool IsDone { get; set; }

    public AiLoadingViewModel(IAiService ai, IAiGenerationJobRepository jobs, AiDraftStore draft)
    {
        _ai = ai;
        _jobs = jobs;
        _draft = draft;
        Title = "Génération en cours";
    }

    public bool CanRetry => !IsRunning && HasError;

    partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(CanRetry));

    /// <summary>Démarre la génération une seule fois par affichage de l'écran.</summary>
    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _ = RunAsync();
    }

    [RelayCommand]
    private Task RetryAsync()
    {
        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        _started = true;
        return RunAsync();
    }

    private async Task RunAsync()
    {
        ClearError();

        var isImage = _draft.SourceType == AiSourceType.Image &&
                      !string.IsNullOrWhiteSpace(_draft.SourceImagePath);

        if (_draft.SubjectId == 0 ||
            (isImage && !File.Exists(_draft.SourceImagePath)) ||
            (!isImage && string.IsNullOrWhiteSpace(_draft.SourceText)))
        {
            ErrorMessage = "Aucun contenu à traiter. Reviens à l'écran précédent pour choisir un cours ou scanner une photo.";
            IsRunning = false;
            OnPropertyChanged(nameof(CanRetry));
            return;
        }

        SubjectName = _draft.SubjectName;
        SourceLabel = _draft.SourceName;
        RequestLabel = _draft.RequestedCount == 1 ? "1 carte demandée" : $"{_draft.RequestedCount} cartes demandées";

        _cancellation = new CancellationTokenSource();
        IsRunning = true;
        IsDone = false;
        Progress = 0;
        ProgressCaption = "0%";
        ElapsedLabel = string.Empty;
        _stepIndex = 0;
        _elapsedSeconds = 0;
        StatusText = isImage ? "Lecture de la photo…" : Steps[0];
        StartTimer();

        var job = new AiGenerationJob
        {
            SourceType = _draft.SourceType,
            SourcePath = _draft.SourceName,
            Status = AiJobStatus.Running,
            RequestedCount = _draft.RequestedCount,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await _jobs.SaveAsync(job);
            _draft.JobId = job.Id;

            IReadOnlyList<AiGeneratedCard> cards;
            if (isImage)
            {
                var extracted = await _ai.ExtractImageTextAsync(_draft.SourceImagePath, _cancellation.Token);
                _draft.SourceText = extracted;
                StatusText = "Photo lue, rédaction des cartes…";
                cards = await _ai.GenerateFlashcardsAsync(extracted, _draft.RequestedCount, _cancellation.Token);
            }
            else
            {
                cards = await _ai.GenerateFlashcardsAsync(_draft.SourceText, _draft.RequestedCount, _cancellation.Token);
            }

            job.Status = AiJobStatus.Completed;
            job.ResultJson = JsonSerializer.Serialize(cards);
            await _jobs.SaveAsync(job);

            _draft.Cards = cards.ToList();
            _draft.IsReady = true;
            _draft.ErrorMessage = null;

            StopTimer();
            Progress = 1;
            ProgressCaption = "100%";
            ElapsedLabel = string.Empty;
            StatusText = "Cartes prêtes !";
            IsDone = true;

            await Task.Delay(400);
            if (!_cancellation.IsCancellationRequested)
            {
                await Shell.Current.GoToAsync(Routes.AiReview);
            }
        }
        catch (OperationCanceledException) when (_cancellation is { IsCancellationRequested: true })
        {
            job.Status = AiJobStatus.Failed;
            job.ErrorMessage = "Génération annulée.";
            await TrySaveJobAsync(job);
            StatusText = "Génération annulée";
            ProgressCaption = "—";
        }
        catch (OperationCanceledException ex)
        {
            job.Status = AiJobStatus.Failed;
            job.ErrorMessage = "Délai dépassé.";
            await TrySaveJobAsync(job);
            _draft.ErrorMessage = ex.Message;
            ShowFailure("Délai d'attente dépassé. Vérifie ta connexion puis réessaie.");
        }
        catch (AiNotConfiguredException ex)
        {
            job.Status = AiJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            await TrySaveJobAsync(job);
            _draft.ErrorMessage = ex.Message;
            ShowFailure(ex.Message);
        }
        catch (Exception ex)
        {
            job.Status = AiJobStatus.Failed;
            job.ErrorMessage = Truncate(ex.Message);
            await TrySaveJobAsync(job);
            _draft.ErrorMessage = ex.Message;
            ShowFailure(ex.Message);
        }
        finally
        {
            StopTimer();
            IsRunning = false;
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }

    private void ShowFailure(string message)
    {
        StopTimer();
        StatusText = "La génération a échoué";
        ProgressCaption = "Erreur";
        ElapsedLabel = string.Empty;
        ErrorMessage = message;
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        _cancellation?.Cancel();
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");

    private async Task TrySaveJobAsync(AiGenerationJob job)
    {
        try
        {
            await _jobs.SaveAsync(job);
        }
        catch (Exception)
        {
            // Le job est un journal secondaire : l'erreur affichée à l'utilisateur reste prioritaire.
        }
    }

    private void StartTimer()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        _timer = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) =>
        {
            if (!IsRunning)
            {
                return;
            }

            _elapsedSeconds++;
            // Progression asymptotique : elle avance sans jamais bloquer l'utilisateur sur un faux 90 %.
            Progress = Math.Min(0.97, Progress + ((0.97 - Progress) * 0.055));
            ProgressCaption = $"{Math.Round(Progress * 100)}%";
            ElapsedLabel = $"{_elapsedSeconds} s écoulées";

            if (_elapsedSeconds == 40)
            {
                StatusText = "L'IA termine la rédaction…";
            }
            else if (_elapsedSeconds == 70)
            {
                StatusText = "Encore un instant, la réponse arrive…";
            }
            else if (_elapsedSeconds < 8)
            {
                _stepIndex = Math.Min(Steps.Length - 2, _stepIndex + 1);
                StatusText = Steps[_stepIndex];
            }
        };
        _timer.Start();
    }

    private void StopTimer()
    {
        if (_timer is null)
        {
            return;
        }

        _timer.Stop();
        _timer = null;
    }

    private static string Truncate(string message) =>
        message.Length <= 400 ? message : message[..400];
}
