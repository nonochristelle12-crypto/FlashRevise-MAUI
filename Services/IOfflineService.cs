namespace FlashRevise.Services;

/// <summary>Suivi de la connectivité et de la file de synchronisation en attente.</summary>
public interface IOfflineService
{
    bool IsOnline { get; }

    int PendingSyncCount { get; }

    /// <summary>Vrai tant que des opérations n'ont pas pu être envoyées au serveur.</summary>
    bool HasPendingWork => PendingSyncCount > 0;

    event EventHandler<bool>? OnlineChanged;

    event EventHandler<int>? PendingSyncCountChanged;

    Task InitializeAsync();

    Task RefreshPendingAsync();
}
