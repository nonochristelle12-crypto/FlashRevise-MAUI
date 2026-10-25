using FlashRevise.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace FlashRevise.Services;

/// <inheritdoc cref="IOfflineService"/>
public sealed class OfflineService : IOfflineService
{
    private readonly ISyncQueueRepository _queue;
    private readonly ILogger<OfflineService> _logger;
    private bool _isOnline = true;
    private int _pending;

    public OfflineService(ISyncQueueRepository queue, ILogger<OfflineService> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    public bool IsOnline => _isOnline;

    public int PendingSyncCount => _pending;

    public event EventHandler<bool>? OnlineChanged;

    public event EventHandler<int>? PendingSyncCountChanged;

    public async Task InitializeAsync()
    {
        _isOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        await RefreshPendingAsync().ConfigureAwait(false);
    }

    public async Task RefreshPendingAsync()
    {
        try
        {
            var count = await _queue.CountAsync().ConfigureAwait(false);
            if (count != _pending)
            {
                _pending = count;
                PendingSyncCountChanged?.Invoke(this, _pending);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lecture de la file de synchronisation impossible");
        }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        var online = e.NetworkAccess == NetworkAccess.Internet;
        if (online == _isOnline)
        {
            return;
        }

        _isOnline = online;
        OnlineChanged?.Invoke(this, online);
    }
}
