using FlashRevise.Models;

namespace FlashRevise.Data.Repositories;

/// <summary>Accès aux matières.</summary>
public interface ISubjectRepository
{
    Task<List<Subject>> GetActiveAsync();
    Task<List<Subject>> GetArchivedAsync();
    Task<Subject?> GetAsync(int id);
    Task<int> SaveAsync(Subject subject);
    Task SetArchivedAsync(int id, bool archived);
    Task DeleteAsync(int id);
    Task<int> CountActiveAsync();
}

/// <summary>Accès aux flashcards.</summary>
public interface IFlashcardRepository
{
    Task<List<Flashcard>> GetBySubjectAsync(int subjectId);
    Task<List<Flashcard>> SearchAsync(string term);
    Task<Flashcard?> GetAsync(int id);
    Task<int> SaveAsync(Flashcard card);
    Task DeleteAsync(int id);
    Task<int> CountBySubjectAsync(int subjectId);
    Task<int> CountAllAsync();
    Task<List<Flashcard>> GetRandomAsync(int count);
}

/// <summary>Accès à l'historique de révision.</summary>
public interface IReviewLogRepository
{
    Task<int> AddAsync(ReviewLog log);
    Task<List<ReviewLog>> GetSinceAsync(DateTime since);
    Task<List<ReviewLog>> GetBetweenAsync(DateTime from, DateTime to);
    Task<int> CountForDayAsync(DateTime day);
    Task<DateTime?> GetLastReviewAsync();
}

/// <summary>Accès aux sessions de révision.</summary>
public interface IReviewSessionRepository
{
    Task<int> StartAsync(ReviewSession session);
    Task CompleteAsync(ReviewSession session);
    Task<List<ReviewSession>> GetRecentAsync(int count);
    Task<ReviewSession?> GetAsync(int id);
}

/// <summary>Accès au planning SM-2.</summary>
public interface IReviewScheduleRepository
{
    Task<ReviewSchedule?> GetByFlashcardAsync(int flashcardId);
    Task<List<ReviewSchedule>> GetDueAsync(DateTime asOf);
    Task<List<ReviewSchedule>> GetBySubjectAsync(int subjectId);
    Task<int> SaveAsync(ReviewSchedule schedule);
    Task<int> CountDueAsync(DateTime asOf);
}

/// <summary>Accès aux rappels planifiés.</summary>
public interface IReminderRepository
{
    Task<List<Reminder>> GetAllAsync();
    Task<Reminder?> GetAsync(int id);
    Task<int> SaveAsync(Reminder reminder);
    Task DeleteAsync(int id);
}

/// <summary>Accès au profil utilisateur.</summary>
public interface IUserProfileRepository
{
    Task<UserProfile> GetOrCreateAsync();
    Task<int> SaveAsync(UserProfile profile);
}

/// <summary>Accès aux livres suivis.</summary>
public interface IBookRepository
{
    Task<List<Book>> GetAllAsync();
    Task<int> SaveAsync(Book book);
    Task DeleteAsync(int id);
}

/// <summary>Accès aux jobs de génération IA.</summary>
public interface IAiGenerationJobRepository
{
    Task<int> SaveAsync(AiGenerationJob job);
    Task<AiGenerationJob?> GetAsync(int id);
    Task<List<AiGenerationJob>> GetRecentAsync(int count);
    Task DeleteAsync(int id);
}

/// <summary>Accès aux succès débloqués.</summary>
public interface IAchievementRepository
{
    Task<List<Achievement>> GetAllAsync();
    Task<bool> UnlockAsync(string code);
}

/// <summary>Accès à la file de synchronisation hors-ligne.</summary>
public interface ISyncQueueRepository
{
    Task EnqueueAsync(SyncQueueItem item);
    Task<List<SyncQueueItem>> GetAllAsync();
    Task<int> CountAsync();
    Task RemoveAsync(int id);
    Task MarkAttemptAsync(int id, string? error);
}
