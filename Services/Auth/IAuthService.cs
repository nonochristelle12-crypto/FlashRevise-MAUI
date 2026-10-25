using FlashRevise.Models;

namespace FlashRevise.Services.Auth;

/// <summary>Client de l'API d'authentification FlashRevise (/v1/auth, /v1/account).</summary>
public interface IAuthService
{
    /// <summary>URL de base actuellement utilisée (préférence locale ou configuration).</summary>
    string BaseUrl { get; }

    Task<string?> GetAccessTokenAsync();

    Task<AuthenticatedUser?> GetCurrentUserAsync();

    Task<bool> HasSessionAsync();

    Task<TokenResponse> LoginAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<TokenResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<AuthenticatedUser?> GetAccountAsync(CancellationToken cancellationToken = default);

    Task<bool> RefreshAsync(CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);

    Task DeleteAccountAsync(CancellationToken cancellationToken = default);

    Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> CheckServerAsync(CancellationToken cancellationToken = default);

    /// <summary>Définit l'URL de l'API (http/https absolu) et la conserve localement.</summary>
    void SetBaseUrl(string baseUrl);
}

public sealed class AuthException(
    string message,
    string? code = null,
    int statusCode = 0,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string? Code { get; } = code;

    public int StatusCode { get; } = statusCode;
}
