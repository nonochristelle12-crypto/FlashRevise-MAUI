using FlashRevise.Data.Repositories;
using FlashRevise.Models;

namespace FlashRevise.Services;

/// <summary>CRUD des rappels et cohérence avec les notifications planifiées.</summary>
public interface IReminderService
{
    Task<List<Reminder>> GetAllAsync();
    Task<Reminder?> GetAsync(int id);
    Task<int> SaveAsync(Reminder reminder);
    Task DeleteAsync(int id);
    Task ToggleAsync(int id, bool enabled);

    /// <summary>Replanifie tous les rappels actifs au démarrage de l'application.</summary>
    Task RestoreAsync();
}
