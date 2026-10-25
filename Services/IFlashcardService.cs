using FlashRevise.Models;

namespace FlashRevise.Services;

/// <summary>Gestion des flashcards et de leur planification.</summary>
public interface IFlashcardService
{
    Task<List<Flashcard>> GetBySubjectAsync(int subjectId);
    Task<Flashcard?> GetAsync(int id);
    Task<int> SaveAsync(Flashcard card);
    Task DeleteAsync(int id);
    Task<List<Flashcard>> GetDueAsync(int subjectId, int limit, bool randomize = false);
    Task<int> SaveGeneratedAsync(int subjectId, IEnumerable<AiGeneratedCard> cards, CancellationToken cancellationToken = default);
    Task<int> CountAllAsync();
    Task<int> CountDueAsync(int subjectId = 0);
}
