namespace FlashRevise.Models;

/// <summary>Dtos alignés sur FlashRevise.Contracts de l'API (JSON camelCase).</summary>
public sealed record AuthenticatedUser(string Id, string DisplayName, string Email);

public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds,
    AuthenticatedUser User);

public sealed record LoginRequest(string Email, string Password);

public sealed record RegisterRequest(
    string DisplayName,
    string Email,
    string Password,
    string StudyLevel,
    bool ConsentTerms);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record ForgotPasswordRequest(string Email);
