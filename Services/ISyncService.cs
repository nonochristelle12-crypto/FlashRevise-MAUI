namespace FlashRevise.Services;

/// <summary>Synchronisation différée : les opérations locales sont rejouées à la reconnexion.</summary>
public interface ISyncService
{
    /// <summary>Vrai lorsque l'application est en mode purement local (aucun endpoint configuré).</summary>
    bool IsRemoteConfigured { get; }

    /// <summary>Rejoue la file d'attente. Retourne le nombre d'opérations envoyées.</summary>
    Task<int> FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>Place une opération en file pour envoi ultérieur.</summary>
    Task EnqueueAsync(string entity, int entityId, Models.SyncOperation operation, object payload);
}
