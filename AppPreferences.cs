namespace FlashRevise;

/// <summary>Clés de préférences locales (non sensibles).</summary>
public static class AppPreferences
{
    public const string OnboardingCompleted = "flashrevise.onboarding.completed";

    public const string LegacyProfileMigrated = "flashrevise.profile.legacy_migrated";

    /// <summary>URL de base de l'API d'authentification choisie par l'utilisateur.</summary>
    public const string AuthBaseUrl = "flashrevise.auth.base_url";

    /// <summary>Profil public du compte connecté (nom et e-mail, sans jeton).</summary>
    public const string AuthUser = "flashrevise.auth.user";
}
