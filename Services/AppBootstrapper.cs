using FlashRevise.Data;
using Microsoft.Extensions.Logging;

namespace FlashRevise.Services;

/// <summary>Initialisation unique de l'application : base de données, contenu de départ, rappels, sync.</summary>
public sealed class AppBootstrapper(
    FlashReviseDatabase database,
    DataSeeder seeder,
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

        await TryAsync(() => seeder.SeedAsync(), "seed");
        await TryAsync(() => offline.InitializeAsync(), "offline");
        await TryAsync(() => reminders.RestoreAsync(), "reminders");
        await TryAsync(() => sync.FlushAsync(), "sync");
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
