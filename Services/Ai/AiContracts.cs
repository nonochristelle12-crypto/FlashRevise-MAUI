namespace FlashRevise.Services.Ai;

/// <summary>Configuration du fournisseur Groq, liée à la section « Groq » d'appsettings.json.</summary>
public sealed class GroqOptions
{
    public const string SectionName = "Groq";

    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1/";

    /// <summary>Modèle de chat utilisé pour la génération, les explications et l'assistant.</summary>
    public string Model { get; set; } = "openai/gpt-oss-120b";

    /// <summary>Modèle multimodal utilisé pour lire le texte d'une photo ou d'un scan.</summary>
    public string VisionModel { get; set; } = "meta-llama/llama-4-scout-17b-16e-instruct";

    /// <summary>Modèles vision essayés si le premier n'est plus disponible sur la clé.</summary>
    public string[] VisionFallbackModels { get; set; } = ["meta-llama/llama-4-maverick-17b-128e-instruct"];

    public int TimeoutSeconds { get; set; } = 90;

    public int MaxRetries { get; set; } = 3;

    /// <summary>Budget de tokens par réponse. Les modèles à raisonnement consomment une partie du budget.</summary>
    public int MaxOutputTokens { get; set; } = 4096;
}

/// <summary>Erreur renvoyée par le service IA, avec un message exploitable par l'UI.</summary>
public class AiServiceException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>La clé API n'a pas encore été configurée par l'utilisateur.</summary>
public sealed class AiNotConfiguredException()
    : AiServiceException("Aucune clé API Groq n'est configurée. Ouvrez Profil › Clé API IA pour l'ajouter.");

/// <summary>L'API a refusé la requête (clé invalide, quota, modèle indisponible…).</summary>
public sealed class AiApiException(string message, int statusCode, Exception? innerException = null)
    : AiServiceException(message, innerException)
{
    public int StatusCode { get; } = statusCode;
}
