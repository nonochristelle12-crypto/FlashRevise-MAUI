using FlashRevise.Data;
using FlashRevise.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace FlashRevise.Services;

/// <summary>Initialisation unique de l'application : base de données, contenu de départ, rappels, sync.</summary>
public sealed class AppBootstrapper(
    FlashReviseDatabase database,
    DataSeeder seeder,
    IUserProfileRepository profiles,
    IReminderService reminders,
    IOfflineService offline,
    ISyncService sync,
    ILogger<AppBootstrapper> logger)
{
    private Task? _initialization;

    public Task InitializeAsync() => _initialization ??= RunAsync();

    private async Task RunAsync()
    {
        await database.GetConnectionAsync().ConfigureAwait(false);

        await TryAsync(MigrateLegacyProfileAsync, "profil");
        await TryAsync(() => seeder.SeedAsync(), "seed");
        await TryAsync(() => offline.InitializeAsync(), "offline");
        await TryAsync(() => reminders.RestoreAsync(), "reminders");
        await TryAsync(() => sync.FlushAsync(), "sync");
    }

    /// <summary>
    /// Les versions précédentes préremplissaient le profil avec un prénom de démonstration.
    /// On repasse une seule fois sur un profil invité, sans toucher aux profils déjà personnalisés.
    /// </summary>
    private async Task MigrateLegacyProfileAsync()
    {
        if (Preferences.Default.Get(AppPreferences.LegacyProfileMigrated, false))
        {
            return;
        }

        var profile = await profiles.GetOrCreateAsync().ConfigureAwait(false);
        if (profile.TotalXp == 0 && string.Equals(profile.Name, "Thomas", StringComparison.Ordinal))
        {
            profile.Name = string.Empty;
            await profiles.SaveAsync(profile).ConfigureAwait(false);
        }

        Preferences.Default.Set(AppPreferences.LegacyProfileMigrated, true);
    }

    private async Task TryAsync(Func<Task> action, string step)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Étape d'initialisation « {Step} » en échec", step);
        }
    }
}
