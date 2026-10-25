using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FlashRevise.Models;
using Microsoft.Extensions.Configuration;

namespace FlashRevise.Services.Auth;

/// <summary>
/// Client de l'API d'authentification. Les jetons sont conservés uniquement
/// dans le SecureStorage de la plateforme ; seul le profil public (nom, e-mail)
/// est mis en cache en clair dans les préférences.
/// </summary>
public sealed class AuthService : IAuthService
{
    private const string AccessTokenKey = "flashrevise.auth.access_token";
    private const string RefreshTokenKey = "flashrevise.auth.refresh_token";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly IConfiguration _configuration;

    private string? _accessToken;
    private string? _refreshToken;

    public AuthService(IConfiguration configuration) => _configuration = configuration;

    public string BaseUrl => ResolveBaseUrl();

    public async Task<string?> GetAccessTokenAsync()
    {
        if (!string.IsNullOrWhiteSpace(_accessToken))
        {
            return _accessToken;
        }

        _accessToken = await ReadSecureAsync(AccessTokenKey);
        return _accessToken;
    }

    public Task<AuthenticatedUser?> GetCurrentUserAsync()
    {
        var json = Preferences.Default.Get(AppPreferences.AuthUser, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return Task.FromResult<AuthenticatedUser?>(null);
        }

        try
        {
            return Task.FromResult(JsonSerializer.Deserialize<AuthenticatedUser>(json, JsonOptions));
        }
        catch (JsonException)
        {
            return Task.FromResult<AuthenticatedUser?>(null);
        }
    }

    public async Task<bool> HasSessionAsync() =>
        !string.IsNullOrWhiteSpace(await GetRefreshTokenAsync());

    public async Task<TokenResponse> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            Post("v1/auth/login", new LoginRequest(email.Trim(), password)),
            cancellationToken);
        return await ReadSessionAsync(response, cancellationToken);
    }

    public async Task<TokenResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(Post("v1/auth/register", request), cancellationToken);
        return await ReadSessionAsync(response, cancellationToken);
    }

    public async Task<AuthenticatedUser?> GetAccountAsync(CancellationToken cancellationToken = default)
    {
        var account = await RequestAccountAsync(cancellationToken);
        if (account is not null)
        {
            return account;
        }

        return await RefreshAsync(cancellationToken)
            ? await RequestAccountAsync(cancellationToken)
            : null;
    }

    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            var refreshToken = await GetRefreshTokenAsync();
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return false;
            }

            using var response = await SendAsync(
                Post("v1/auth/refresh", new RefreshTokenRequest(refreshToken)),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
                {
                    ClearSession();
                }

                return false;
            }

            var session = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken);
            if (session is null ||
                string.IsNullOrWhiteSpace(session.AccessToken) ||
                string.IsNullOrWhiteSpace(session.RefreshToken))
            {
                ClearSession();
                return false;
            }

            await StoreSessionAsync(session);
            return true;
        }
        catch (AuthException)
        {
            // Réseau indisponible : on conserve la session pour réessayer plus tard.
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var refreshToken = await GetRefreshTokenAsync();
            var accessToken = await GetAccessTokenAsync();
            if (!string.IsNullOrWhiteSpace(refreshToken) && !string.IsNullOrWhiteSpace(accessToken))
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, Url("v1/auth/logout"))
                {
                    Content = JsonContent.Create(new RefreshTokenRequest(refreshToken), options: JsonOptions)
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                using var response = await SendAsync(request, cancellationToken);
            }
        }
        catch (Exception)
        {
            // La révocation serveur est un meilleur effort : la session locale est effacée quoi qu'il arrive.
        }
        finally
        {
            ClearSession();
        }
    }

    public async Task DeleteAccountAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendAuthorizedAsync(HttpMethod.Delete, "v1/account", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            if (!await RefreshAsync(cancellationToken))
            {
                throw new AuthException("Session expirée, reconnecte-toi avant de supprimer le compte.", "session_expired", 401);
            }

            response = await SendAuthorizedAsync(HttpMethod.Delete, "v1/account", null, cancellationToken);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw await CreateExceptionAsync(response, cancellationToken);
            }
        }

        ClearSession();
    }

    public async Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            Post("v1/auth/forgot-password", new ForgotPasswordRequest(email.Trim())),
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(response, cancellationToken);
        }
    }

    public async Task<bool> CheckServerAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Url("health/live"));
            using var response = await SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void SetBaseUrl(string baseUrl)
    {
        if (!TryCreateHttpUri(baseUrl, out var uri))
        {
            throw new AuthException("Adresse invalide. Exemple : http://192.168.1.10:5047/");
        }

        Preferences.Default.Set(AppPreferences.AuthBaseUrl, Normalize(uri));
    }

    private async Task<AuthenticatedUser?> RequestAccountAsync(CancellationToken cancellationToken)
    {
        var accessToken = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, Url("v1/account"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(response, cancellationToken);
        }

        var account = await response.Content.ReadFromJsonAsync<AuthenticatedUser>(JsonOptions, cancellationToken);
        if (account is not null)
        {
            CacheUser(account);
        }

        return account;
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, Url(path)) { Content = content };
        var accessToken = await GetAccessTokenAsync();
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await SendAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await _http.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AuthException(
                $"Le serveur ne répond pas ({BaseUrl}). Vérifie que l'API est démarrée.");
        }
        catch (HttpRequestException ex)
        {
            throw new AuthException(
                $"Serveur injoignable ({BaseUrl}). Vérifie l'adresse de l'API, le Wi-Fi et que le serveur est démarré.",
                "server_unreachable",
                0,
                ex);
        }
    }

    private async Task<TokenResponse> ReadSessionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(response, cancellationToken);
        }

        var session = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken);
        if (session is null || session.User is null ||
            string.IsNullOrWhiteSpace(session.AccessToken) ||
            string.IsNullOrWhiteSpace(session.RefreshToken))
        {
            throw new AuthException("Réponse du serveur incomplète : aucun jeton reçu.");
        }

        await StoreSessionAsync(session);
        return session;
    }

    private async Task StoreSessionAsync(TokenResponse session)
    {
        _accessToken = session.AccessToken;
        _refreshToken = session.RefreshToken;
        await WriteSecureAsync(AccessTokenKey, session.AccessToken);
        await WriteSecureAsync(RefreshTokenKey, session.RefreshToken);
        CacheUser(session.User);
    }

    private static void CacheUser(AuthenticatedUser user) =>
        Preferences.Default.Set(AppPreferences.AuthUser, JsonSerializer.Serialize(user, JsonOptions));

    private void ClearSession()
    {
        _accessToken = null;
        _refreshToken = null;
        TryRemoveSecure(AccessTokenKey);
        TryRemoveSecure(RefreshTokenKey);
        Preferences.Default.Remove(AppPreferences.AuthUser);
    }

    private async Task<string?> GetRefreshTokenAsync()
    {
        if (!string.IsNullOrWhiteSpace(_refreshToken))
        {
            return _refreshToken;
        }

        _refreshToken = await ReadSecureAsync(RefreshTokenKey);
        return _refreshToken;
    }

    private static async Task<string?> ReadSecureAsync(string key)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(key);
        }
        catch (Exception)
        {
            // Stockage sécurisé indisponible : la session reste en mémoire pour cette exécution.
            return null;
        }
    }

    private static async Task WriteSecureAsync(string key, string value)
    {
        try
        {
            await SecureStorage.Default.SetAsync(key, value);
        }
        catch (Exception)
        {
            // Idem : pas de repli en clair sur le disque.
        }
    }

    private static void TryRemoveSecure(string key)
    {
        try
        {
            SecureStorage.Default.Remove(key);
        }
        catch (Exception)
        {
            // Ignoré.
        }
    }

    private Uri Url(string relativePath) => new(new Uri(BaseUrl), relativePath);

    private HttpRequestMessage Post<T>(string path, T body) =>
        new(HttpMethod.Post, Url(path)) { Content = JsonContent.Create(body, options: JsonOptions) };

    private static async Task<AuthException> CreateExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        ProblemPayload? problem = null;
        try
        {
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(raw) && raw.TrimStart().StartsWith('{'))
            {
                problem = JsonSerializer.Deserialize<ProblemPayload>(raw, JsonOptions);
            }
        }
        catch (Exception)
        {
            // Corps non JSON : on retombe sur le code HTTP.
        }

        var message = problem?.Code switch
        {
            "invalid_credentials" => "E-mail ou mot de passe incorrect.",
            "account_locked" => "Compte verrouillé après 5 tentatives. Réessaie dans 5 minutes.",
            "email_already_used" => "Cette adresse e-mail est déjà utilisée.",
            "consent_required" => "Tu dois accepter les conditions d'utilisation.",
            "validation_failed" => "Informations invalides. Mot de passe : 8 caractères minimum, une majuscule et un chiffre.",
            _ => null
        };

        message ??= response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Session expirée, reconnecte-toi.",
            HttpStatusCode.TooManyRequests => "Trop de tentatives. Patiente une minute avant de réessayer.",
            HttpStatusCode.Conflict => "Cette adresse e-mail est déjà utilisée.",
            HttpStatusCode.BadRequest => "Vérifie les informations saisies.",
            HttpStatusCode.ServiceUnavailable => "Le serveur est indisponible, réessaie plus tard.",
            _ when problem?.Detail is { Length: > 0 } detail => detail,
            _ => $"Erreur du serveur (HTTP {(int)response.StatusCode})."
        };

        return new AuthException(message, problem?.Code, (int)response.StatusCode);
    }

    private string ResolveBaseUrl()
    {
        var stored = Preferences.Default.Get(AppPreferences.AuthBaseUrl, string.Empty);
        if (TryCreateHttpUri(stored, out var storedUri))
        {
            return Normalize(storedUri);
        }

        if (TryCreateHttpUri(_configuration["Auth:BaseUrl"], out var configuredUri))
        {
            return Normalize(configuredUri);
        }

        // 10.0.2.2 = hôte Windows vu depuis l'émulateur Android. Sur un téléphone
        // physique, renseigne l'IP LAN du PC dans Profil → Compte.
        var host = DeviceInfo.Platform == DevicePlatform.Android ? "10.0.2.2" : "localhost";
        return $"http://{host}:5047/";
    }

    private static bool TryCreateHttpUri(string? value, out Uri uri)
    {
        if (Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var parsed) &&
            (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
        {
            uri = parsed;
            return true;
        }

        uri = null!;
        return false;
    }

    private static string Normalize(Uri uri) => uri.AbsoluteUri.TrimEnd('/') + "/";

    private sealed record ProblemPayload(string? Title, string? Detail, string? Code);
}
