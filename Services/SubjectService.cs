using FlashRevise.Data.Repositories;
using FlashRevise.Models;

namespace FlashRevise.Services;

/// <inheritdoc cref="ISubjectService"/>
public sealed class SubjectService(ISubjectRepository repository, ISyncService sync) : ISubjectService
{
    public Task<List<Subject>> GetSubjectsAsync(bool includeArchived = false) =>
        includeArchived ? repository.GetArchivedAsync() : repository.GetActiveAsync();

    public Task<Subject?> GetAsync(int id) => repository.GetAsync(id);

    public async Task<int> SaveAsync(Subject subject)
    {
        var isNew = subject.Id == 0;
        var id = await repository.SaveAsync(subject).ConfigureAwait(false);
        await sync.EnqueueAsync(nameof(Subject), id, isNew ? SyncOperation.Create : SyncOperation.Update, subject)
            .ConfigureAwait(false);
        return id;
    }

    public async Task SetArchivedAsync(int id, bool archived)
    {
        await repository.SetArchivedAsync(id, archived).ConfigureAwait(false);
        var subject = await repository.GetAsync(id).ConfigureAwait(false);
        if (subject is not null)
        {
            await sync.EnqueueAsync(nameof(Subject), id, SyncOperation.Update, subject).ConfigureAwait(false);
        }
    }

    public async Task DeleteAsync(int id)
    {
        await repository.DeleteAsync(id).ConfigureAwait(false);
        await sync.EnqueueAsync(nameof(Subject), id, SyncOperation.Delete, new { Id = id }).ConfigureAwait(false);
    }

    public Task<int> CountActiveAsync() => repository.CountActiveAsync();
}
