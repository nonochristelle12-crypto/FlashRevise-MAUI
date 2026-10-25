namespace FlashRevise.Services.Ai;

/// <summary>
/// Conserve la clé Groq dans le SecureStorage de la plateforme (Keystore Android, DPAPI Windows).
/// Aucun repli en clair n'est effectué sur le disque : si le stockage sécurisé est indisponible,
/// la clé reste en mémoire pour la session et l'UI en informe l'utilisateur.
/// </summary>
public sealed class SecureApiKeyStore : IApiKeyStore
{
    private const string KeyName = "flashrevise.groq.api_key";

    private string? _cachedKey;

    public bool IsSecureStorageAvailable { get; private set; } = true;

    public async Task<string?> GetKeyAsync()
    {
        if (!string.IsNullOrWhiteSpace(_cachedKey))
        {
            return _cachedKey;
        }

        try
        {
            _cachedKey = await SecureStorage.Default.GetAsync(KeyName).ConfigureAwait(false);
            IsSecureStorageAvailable = true;
        }
        catch (Exception)
        {
            IsSecureStorageAvailable = false;
        }

        return string.IsNullOrWhiteSpace(_cachedKey) ? null : _cachedKey;
    }

    public async Task SetKeyAsync(string key)
    {
        var trimmed = key.Trim();
        _cachedKey = trimmed;

        try
        {
            await SecureStorage.Default.SetAsync(KeyName, trimmed).ConfigureAwait(false);
            IsSecureStorageAvailable = true;
        }
        catch (Exception)
        {
            IsSecureStorageAvailable = false;
        }
    }

    public async Task ClearAsync()
    {
        _cachedKey = null;
        try
        {
            SecureStorage.Default.Remove(KeyName);
            await Task.CompletedTask.ConfigureAwait(false);
        }
        catch (Exception)
        {
            IsSecureStorageAvailable = false;
        }
    }

    public async Task<bool> HasKeyAsync() => !string.IsNullOrWhiteSpace(await GetKeyAsync().ConfigureAwait(false));
}
