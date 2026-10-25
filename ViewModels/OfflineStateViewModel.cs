using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Data.Repositories;
using FlashRevise.Models;
using FlashRevise.Services;

namespace FlashRevise.ViewModels;

/// <summary>Opération en attente de synchronisation, présentée à l'utilisateur.</summary>
public sealed class PendingOperationRow
{
    public PendingOperationRow(SyncQueueItem item)
    {
        EntityLabel = item.Entity switch
        {
            nameof(Flashcard) => "Carte",
            nameof(Subject) => "Matière",
            nameof(ReviewSchedule) => "Planification",
            _ => item.Entity
        };
        OperationLabel = item.Operation switch
        {
            SyncOperation.Create => "Création",
            SyncOperation.Update => "Modification",
            _ => "Suppression"
        };
        QueuedLabel = item.QueuedAt.ToLocalTime().ToString("d MMM · HH:mm", CultureInfo.CurrentCulture);
        HasError = !string.IsNullOrWhiteSpace(item.LastError);
        AttemptsLabel = item.Attempts == 0 ? string.Empty : $"{item.Attempts} tentative(s)";
        ErrorLabel = item.LastError ?? string.Empty;
    }

    public string EntityLabel { get; }
    public string OperationLabel { get; }
    public string QueuedLabel { get; }
    public string AttemptsLabel { get; }
    public string ErrorLabel { get; }
    public bool HasError { get; }
}

/// <summary>État hors-ligne : connectivité, file d'attente et synchronisation manuelle.</summary>
public partial class OfflineStateViewModel : BaseViewModel
{
    private readonly IOfflineService _offline;
    private readonly ISyncService _sync;
    private readonly ISyncQueueRepository _queue;

    private bool _subscribed;

    [ObservableProperty]
    public partial bool IsOnline { get; set; }

    [ObservableProperty]
    public partial int PendingCount { get; set; }

    [ObservableProperty]
    public partial bool IsSyncing { get; set; }

    [ObservableProperty]
    public partial string StatusTitle { get; set; } = "Vérification…";

    [ObservableProperty]
    public partial string StatusDetail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PendingLabel { get; set; } = "Aucune opération en attente";

    [ObservableProperty]
    public partial string RemoteLabel { get; set; } = "Mode local";

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    public OfflineStateViewModel(IOfflineService offline, ISyncService sync, ISyncQueueRepository queue)
    {
        _offline = offline;
        _sync = sync;
        _queue = queue;
        Title = "Mode hors-ligne";
        Subscribe();
    }

    public ObservableCollection<PendingOperationRow> Operations { get; } = [];

    public bool HasOperations => Operations.Count > 0;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool IsRemoteConfigured => _sync.IsRemoteConfigured;

    public bool CanSync => IsOnline && IsRemoteConfigured && PendingCount > 0 && !IsSyncing;

    public string EmptyLabel => IsOnline
        ? "Tout est synchronisé." 
        : "Aucune opération en attente : tu peux réviser sereinement hors-ligne.";

    partial void OnIsOnlineChanged(bool value)
    {
        UpdateStatus();
        NotifyComputed();
    }

    partial void OnPendingCountChanged(int value)
    {
        PendingLabel = value switch
        {
            0 => "Aucune opération en attente",
            1 => "1 opération en attente",
            _ => $"{value} opérations en attente"
        };
        UpdateStatus();
        NotifyComputed();
    }

    partial void OnIsSyncingChanged(bool value)
    {
        NotifyComputed();
        SyncNowCommand.NotifyCanExecuteChanged();
    }

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatusMessage));

    private bool CanSyncNow() => _sync.IsRemoteConfigured && !IsSyncing;

    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        _offline.OnlineChanged += OnOnlineChanged;
        _offline.PendingSyncCountChanged += OnPendingChanged;
        _subscribed = true;
    }

    /// <summary>Détache le ViewModel des événements du service (appelé à la fermeture de l'écran).</summary>
    public void Stop()
    {
        if (!_subscribed)
        {
            return;
        }

        _offline.OnlineChanged -= OnOnlineChanged;
        _offline.PendingSyncCountChanged -= OnPendingChanged;
        _subscribed = false;
    }

    private void OnOnlineChanged(object? sender, bool online) => MainThread.BeginInvokeOnMainThread(() => IsOnline = online);

    private void OnPendingChanged(object? sender, int count)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            PendingCount = count;
            _ = LoadOperationsAsync();
        });
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
        StatusMessage = null;
        try
        {
            await _offline.RefreshPendingAsync();
            IsOnline = _offline.IsOnline;
            PendingCount = _offline.PendingSyncCount;
            RemoteLabel = _sync.IsRemoteConfigured
                ? "Synchronisation distante configurée"
                : "Mode local · aucune synchronisation distante";
            await LoadOperationsAsync();
            UpdateStatus();
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

    private async Task LoadOperationsAsync()
    {
        try
        {
            var items = await _queue.GetAllAsync();
            Operations.Clear();
            foreach (var item in items.OrderByDescending(i => i.QueuedAt).Take(20))
            {
                Operations.Add(new PendingOperationRow(item));
            }

            PendingCount = items.Count;
            OnPropertyChanged(nameof(HasOperations));
            OnPropertyChanged(nameof(EmptyLabel));
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSyncNow))]
    private async Task SyncNowAsync()
    {
        if (IsSyncing)
        {
            return;
        }

        ClearError();
        StatusMessage = null;

        if (!_sync.IsRemoteConfigured)
        {
            ErrorMessage = "Aucun endpoint de synchronisation n'est configuré : l'app fonctionne en mode 100 % local.";
            return;
        }

        if (!IsOnline)
        {
            ErrorMessage = "Pas de connexion réseau. La synchronisation reprendra automatiquement au retour du réseau.";
            return;
        }

        IsSyncing = true;
        try
        {
            var sent = await _sync.FlushAsync();
            await _offline.RefreshPendingAsync();
            await LoadOperationsAsync();
            StatusMessage = sent == 0
                ? "Rien à envoyer : la file est vide ou le serveur est indisponible."
                : sent == 1
                    ? "1 opération synchronisée."
                    : $"{sent} opérations synchronisées.";
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");

    private void UpdateStatus()
    {
        if (!IsOnline)
        {
            StatusTitle = "Hors ligne";
            StatusDetail = PendingCount == 0
                ? "Tes révisions restent disponibles. Les changements seront envoyés au retour du réseau."
                : $"{PendingLabel}. Ils seront synchronisés dès que le réseau revient.";
            return;
        }

        StatusTitle = "Connecté";
        StatusDetail = PendingCount == 0
            ? "Tout est à jour. Les données locales sont synchronisées."
            : $"{PendingLabel}. Lance la synchronisation pour les envoyer.";
    }

    private void NotifyComputed()
    {
        OnPropertyChanged(nameof(CanSync));
        SyncNowCommand.NotifyCanExecuteChanged();
    }
}
