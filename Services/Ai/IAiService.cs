using FlashRevise.Models;

namespace FlashRevise.Services.Ai;

/// <summary>Accès à la clé API Groq, stockée hors du code source.</summary>
public interface IApiKeyStore
{
    Task<string?> GetKeyAsync();
    Task SetKeyAsync(string key);
    Task ClearAsync();
    Task<bool> HasKeyAsync();

    /// <summary>False lorsque le stockage sécurisé de la plateforme est indisponible et qu'un repli a été utilisé.</summary>
    bool IsSecureStorageAvailable { get; }
}

/// <summary>Fonctions IA embarquées (Groq, API compatible OpenAI).</summary>
public interface IAiService
{
    /// <summary>Génère des flashcards structurées à partir d'un texte source.</summary>
    Task<IReadOnlyList<AiGeneratedCard>> GenerateFlashcardsAsync(string sourceText, int count, CancellationToken cancellationToken = default);

    /// <summary>Transcrit le texte, le code et les formules visibles sur une photo ou un scan.</summary>
    Task<string> ExtractImageTextAsync(string imagePath, CancellationToken cancellationToken = default);

    /// <summary>Explique une carte avec un angle pédagogique.</summary>
    Task<string> ExplainCardAsync(Flashcard card, CancellationToken cancellationToken = default);

    /// <summary>Poursuit une conversation avec l'assistant de révision.</summary>
    Task<string> ChatAsync(IReadOnlyList<ChatMessage> history, string newMessage, CancellationToken cancellationToken = default);

    /// <summary>Résume un document long en points clés.</summary>
    Task<string> SummarizeAsync(string longText, CancellationToken cancellationToken = default);

    /// <summary>Variante streaming de <see cref="ChatAsync"/>, token par token.</summary>
    IAsyncEnumerable<string> StreamChatAsync(IReadOnlyList<ChatMessage> history, string newMessage, CancellationToken cancellationToken = default);

    /// <summary>Vérifie que la clé est présente et que le modèle répond.</summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
