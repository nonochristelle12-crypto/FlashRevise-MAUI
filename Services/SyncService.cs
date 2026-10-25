using System.Net.Http.Json;
using System.Text.Json;
using FlashRevise.Data.Repositories;
using FlashRevise.Models;
using FlashRevise.Services.Ai;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlashRevise.Services;

/// <summary>Options de synchronisation serveur (endpoint laissé vide tant qu'aucun backend n'existe).</summary>
public sealed class SyncOptions
{
    public const string SectionName = "Sync";

    public string Endpoint { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint);
}

/// <inheritdoc cref="ISyncService"/>
public sealed class SyncService : ISyncService
{
    private readonly ISyncQueueRepository _queue;
    private readonly IOfflineService _offline;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SyncOptions _options;
    private readonly ILogger<SyncService> _logger;

    public SyncService(
        ISyncQueueRepository queue,
        IOfflineService offline,
        IHttpClientFactory httpClientFactory,
        IOptions<SyncOptions> options,
        ILogger<SyncService> logger)
    {
        _queue = queue;
        _offline = offline;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;

        _offline.OnlineChanged += async (_, online) =>
        {
            if (online)
            {
                await FlushAsync().ConfigureAwait(false);
            }
        };
    }

    public bool IsRemoteConfigured => _options.IsConfigured;

    public async Task<int> FlushAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured || !_offline.IsOnline)
        {
            return 0;
        }

        var items = await _queue.GetAllAsync().ConfigureAwait(false);
        if (items.Count == 0)
        {
            return 0;
        }

        var client = _httpClientFactory.CreateClient(nameof(SyncService));
        client.BaseAddress ??= new Uri(_options.Endpoint);

        var sent = 0;
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var response = await client.PostAsJsonAsync("sync", item, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    await _queue.RemoveAsync(item.Id).ConfigureAwait(false);
                    sent++;
                }
                else
                {
                    await _queue.MarkAttemptAsync(item.Id, $"HTTP {(int)response.StatusCode}").ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Échec de synchronisation de l'opération {Id}", item.Id);
                await _queue.MarkAttemptAsync(item.Id, ex.Message).ConfigureAwait(false);
                break;
            }
        }

        await _offline.RefreshPendingAsync().ConfigureAwait(false);
        return sent;
    }

    public async Task EnqueueAsync(string entity, int entityId, SyncOperation operation, object payload)
    {
        await _queue.EnqueueAsync(new SyncQueueItem
        {
            Entity = entity,
            EntityId = entityId,
            Operation = operation,
            Payload = JsonSerializer.Serialize(payload),
            QueuedAt = DateTime.UtcNow
        }).ConfigureAwait(false);

        await _offline.RefreshPendingAsync().ConfigureAwait(false);

        if (_offline.IsOnline)
        {
            await FlushAsync().ConfigureAwait(false);
        }
    }
}
