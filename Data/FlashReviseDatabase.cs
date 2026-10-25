using FlashRevise.Models;
using SQLite;

namespace FlashRevise.Data;

/// <summary>
/// Connexion SQLite unique de l'application. sqlite-net-pcl a été retenu plutôt qu'EF Core :
/// empreinte mémoire réduite et démarrage à froid nettement plus rapide sur Android,
/// pour un modèle de données plat sans relations complexes.
/// </summary>
public sealed class FlashReviseDatabase
{
    public const string FileName = "flashrevise.db3";

    private readonly string _databasePath;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private SQLiteAsyncConnection? _connection;

    public FlashReviseDatabase(string databasePath)
    {
        _databasePath = databasePath;
    }

    public string DatabasePath => _databasePath;

    public async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_connection is not null)
        {
            return _connection;
        }

        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_connection is null)
            {
                var flags = SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache;
                var connection = new SQLiteAsyncConnection(_databasePath, flags);
                await connection.CreateTableAsync<Subject>().ConfigureAwait(false);
                await connection.CreateTableAsync<Flashcard>().ConfigureAwait(false);
                await connection.CreateTableAsync<ReviewLog>().ConfigureAwait(false);
                await connection.CreateTableAsync<ReviewSession>().ConfigureAwait(false);
                await connection.CreateTableAsync<ReviewSchedule>().ConfigureAwait(false);
                await connection.CreateTableAsync<Reminder>().ConfigureAwait(false);
                await connection.CreateTableAsync<UserProfile>().ConfigureAwait(false);
                await connection.CreateTableAsync<Book>().ConfigureAwait(false);
                await connection.CreateTableAsync<AiGenerationJob>().ConfigureAwait(false);
                await connection.CreateTableAsync<Achievement>().ConfigureAwait(false);
                await connection.CreateTableAsync<SyncQueueItem>().ConfigureAwait(false);
                _connection = connection;
            }
        }
        finally
        {
            _initLock.Release();
        }

        return _connection;
    }
}
