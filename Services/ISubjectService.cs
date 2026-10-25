using FlashRevise.Models;

namespace FlashRevise.Services;

/// <summary>Gestion des matières et de leur cycle de vie.</summary>
public interface ISubjectService
{
    Task<List<Subject>> GetSubjectsAsync(bool includeArchived = false);
    Task<Subject?> GetAsync(int id);
    Task<int> SaveAsync(Subject subject);
    Task SetArchivedAsync(int id, bool archived);
    Task DeleteAsync(int id);
    Task<int> CountActiveAsync();
}
