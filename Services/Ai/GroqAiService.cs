using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlashRevise.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;

namespace FlashRevise.Services.Ai;

/// <inheritdoc cref="IAiService"/>
public sealed class GroqAiService : IAiService
{
    private const string FlashcardsSystemPrompt =
        "Tu es un générateur de flashcards pour étudiants francophones. " +
        "Tu produis des cartes atomiques, sans ambiguïté, qui testent une seule notion à la fois. " +
        "Tu réponds TOUJOURS en JSON strict, sans texte autour, sans balises Markdown.";

    private const string AssistantSystemPrompt =
        "Tu es l'assistant de révision de FlashRevise. Tu réponds en français, de façon concise et structurée. " +
        "Tu peux expliquer une notion, proposer une méthode de mémorisation ou créer un mini-quiz. " +
        "Si l'étudiant te donne du contexte de cours, appuie-toi dessus plutôt que sur tes connaissances générales.";

    private const string OcrPrompt =
        "Tu transcris fidèlement le contenu pédagogique visible sur cette photo (page de cours, tableau, notes manuscrites ou code). " +
        "Restitue tout le texte lisible : titres, paragraphes, listes, tableaux, formules et code source en conservant l'indentation et le langage. " +
        "N'ajoute aucun commentaire, aucune introduction et aucune conclusion. " +
        "Si l'image est totalement illisible, réponds uniquement : [ILLISIBLE].";

    private readonly HttpClient _httpClient;
    private readonly IApiKeyStore _apiKeys;
    private readonly GroqOptions _options;
    private readonly ILogger<GroqAiService> _logger;

    public GroqAiService(
        HttpClient httpClient,
        IApiKeyStore apiKeys,
        IOptions<GroqOptions> options,
        ILogger<GroqAiService> logger)
    {
        _httpClient = httpClient;
        _apiKeys = apiKeys;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AiGeneratedCard>> GenerateFlashcardsAsync(
        string sourceText,
        int count,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceText);
        count = Math.Clamp(count, 1, 40);

        var truncated = sourceText.Length > 14000 ? sourceText[..14000] : sourceText;
        var prompt =
            $"À partir du contenu ci-dessous, génère exactement {count} flashcards de révision.\n" +
            "Contraintes :\n" +
            "- question : une seule interrogation, 25 mots maximum ;\n" +
            "- answer : la réponse attendue, 60 mots maximum ;\n" +
            "- hint : une astuce mnémotechnique courte, ou une chaîne vide si aucune n'est pertinente ;\n" +
            "- difficulty : \"Easy\", \"Medium\" ou \"Hard\".\n" +
            "Format de sortie attendu (objet JSON) :\n" +
            "{\"cards\":[{\"question\":\"...\",\"answer\":\"...\",\"hint\":\"...\",\"difficulty\":\"Medium\"}]}\n\n" +
            "CONTENU :\n" + truncated;

        var messages = new List<WireMessage>
        {
            new("system", FlashcardsSystemPrompt),
            new("user", prompt)
        };

        var json = await CompleteAsync(messages, temperature: 0.3, cancellationToken).ConfigureAwait(false);
        var cards = ParseGeneratedCards(json);

        if (cards.Count == 0)
        {
            throw new AiServiceException("L'IA n'a renvoyé aucune carte exploitable. Réessayez avec un texte plus court ou plus explicite.");
        }

        return cards.Take(count).ToList();
    }

    public async Task<string> ExtractImageTextAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            throw new AiServiceException("La photo est introuvable. Reprends le scan ou choisis une autre image.");
        }

        var (dataUrl, mimeType) = await BuildImageDataUrlAsync(imagePath, cancellationToken).ConfigureAwait(false);
        var models = GetVisionModels();
        AiApiException? lastError = null;

        try
        {
            foreach (var model in models)
            {
                try
                {
                    var content = await CompleteVisionAsync(model, dataUrl, mimeType, cancellationToken).ConfigureAwait(false);
                    if (content.Contains("[ILLISIBLE]", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new AiServiceException(
                            "La photo est trop floue ou illisible. Reprends-la bien à plat, avec plus de lumière.");
                    }

                    if (content.Length < 10)
                    {
                        throw new AiServiceException(
                            "Aucun texte exploitable n'a été détecté sur la photo. Cadre mieux le document et réessaie.");
                    }

                    return content;
                }
                catch (AiApiException ex) when (IsModelUnavailable(ex))
                {
                    _logger.LogWarning(ex, "Modèle vision {Model} indisponible, essai du suivant", model);
                    lastError = ex;
                }
            }
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiServiceException(
                "La lecture de la photo a dépassé le délai imparti. Vérifie ta connexion puis réessaie.", ex);
        }

        throw new AiServiceException(
            "Aucun modèle de lecture d'image n'est disponible sur ta clé Groq. Vérifie la configuration vision dans l'app.",
            lastError);
    }

    public async Task<string> ExplainCardAsync(Flashcard card, CancellationToken cancellationToken = default)
    {
        var messages = new List<WireMessage>
        {
            new("system", AssistantSystemPrompt),
            new("user",
                $"Explique la carte suivante à un étudiant qui vient de se tromper.\n" +
                $"Question : {card.Question}\nRéponse : {card.Answer}\n" +
                "Donne : 1) l'idée clé en une phrase, 2) une explication en 2 ou 3 phrases, 3) un moyen mnémotechnique. " +
                "N'utilise pas de titres Markdown.")
        };

        return await CompleteAsync(messages, temperature: 0.4, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ChatAsync(
        IReadOnlyList<ChatMessage> history,
        string newMessage,
        CancellationToken cancellationToken = default)
    {
        var messages = BuildConversation(history, newMessage);
        return await CompleteAsync(messages, temperature: 0.5, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> SummarizeAsync(string longText, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(longText);
        var truncated = longText.Length > 16000 ? longText[..16000] : longText;

        var messages = new List<WireMessage>
        {
            new("system", "Tu résumes des supports de cours pour des étudiants. Tu réponds en français, en puces courtes."),
            new("user", "Résume ce document en 5 à 8 points clés, puis liste les notions qui méritent une flashcard.\n\n" + truncated)
        };

        return await CompleteAsync(messages, temperature: 0.3, cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        IReadOnlyList<ChatMessage> history,
        string newMessage,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = BuildConversation(history, newMessage);
        using var timeout = CreateTimeoutSource(cancellationToken);
        using var request = await CreateRequestAsync(messages, temperature: 0.5, stream: true, timeout.Token).ConfigureAwait(false);
        using var response = await SendWithRetryAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
            .ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var buffer = new StringBuilder();
        while (await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var payload = line[5..].Trim();
            if (payload is "[DONE]")
            {
                break;
            }

            var delta = TryReadStreamDelta(payload);
            if (!string.IsNullOrEmpty(delta))
            {
                buffer.Append(delta);
                yield return delta;
            }
        }

        if (buffer.Length == 0)
        {
            throw new AiServiceException("Le flux de réponse est resté vide. Vérifiez le quota Groq ou réessayez.");
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CreateTimeoutSource(cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Get, "models");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await RequireKeyAsync().ConfigureAwait(false));
            using var response = await _httpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or AiServiceException)
        {
            _logger.LogWarning(ex, "Test de connexion Groq échoué");
            return false;
        }
    }

    private static List<WireMessage> BuildConversation(IReadOnlyList<ChatMessage> history, string newMessage)
    {
        var messages = new List<WireMessage> { new("system", AssistantSystemPrompt) };
        foreach (var message in history)
        {
            if (message.Role == ChatRole.System || string.IsNullOrWhiteSpace(message.Content))
            {
                continue;
            }

            messages.Add(new WireMessage(message.IsUser ? "user" : "assistant", message.Content));
        }

        messages.Add(new WireMessage("user", newMessage));
        return messages;
    }

    private async Task<string> CompleteAsync(
        IReadOnlyList<WireMessage> messages,
        double temperature,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CreateTimeoutSource(cancellationToken);
            using var request = await CreateRequestAsync(messages, temperature, stream: false, timeout.Token).ConfigureAwait(false);
            using var response = await SendWithRetryAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token)
                .ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            var content = TryReadMessageContent(body);

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Réponse Groq vide. Fin de génération : {Finish}", TryReadFinishReason(body));
                throw new AiServiceException(
                    "L'IA a renvoyé une réponse vide (budget de tokens probablement épuisé par le raisonnement). Reformulez ou réduisez la demande.");
            }

            return content.Trim();
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiServiceException(
                "Groq n'a pas répondu dans le délai imparti (connexion lente ou quota). Réessaie dans un instant.", ex);
        }
    }

    private async Task<string> CompleteVisionAsync(
        string model,
        string imageDataUrl,
        string mimeType,
        CancellationToken cancellationToken)
    {
        using var timeout = CreateTimeoutSource(cancellationToken);
        using var request = await CreateVisionRequestAsync(model, imageDataUrl, mimeType, timeout.Token).ConfigureAwait(false);
        using var response = await SendWithRetryAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token)
            .ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        var content = TryReadMessageContent(body);

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("Réponse vision Groq vide. Fin de génération : {Finish}", TryReadFinishReason(body));
            throw new AiServiceException("L'IA n'a pas réussi à lire la photo. Reprends-la avec un meilleur cadrage.");
        }

        return content.Trim();
    }

    private async Task<HttpRequestMessage> CreateVisionRequestAsync(
        string model,
        string imageDataUrl,
        string mimeType,
        CancellationToken cancellationToken)
    {
        var key = await RequireKeyAsync().ConfigureAwait(false);
        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["messages"] = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = OcrPrompt },
                        new { type = "image_url", image_url = new { url = imageDataUrl } }
                    }
                }
            },
            ["temperature"] = 0.1,
            ["max_tokens"] = _options.MaxOutputTokens,
            ["stream"] = false
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _logger.LogDebug("Envoi d'une image {Mime} au modèle vision {Model}", mimeType, model);
        return request;
    }

    private IReadOnlyList<string> GetVisionModels()
    {
        var models = new List<string>();
        if (!string.IsNullOrWhiteSpace(_options.VisionModel))
        {
            models.Add(_options.VisionModel.Trim());
        }

        foreach (var fallback in _options.VisionFallbackModels)
        {
            if (!string.IsNullOrWhiteSpace(fallback) &&
                !models.Contains(fallback.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                models.Add(fallback.Trim());
            }
        }

        return models.Count > 0
            ? models
            : ["meta-llama/llama-4-scout-17b-16e-instruct"];
    }

    private static bool IsModelUnavailable(AiApiException exception) =>
        exception.StatusCode == 404 ||
        (exception.StatusCode == 400 && exception.Message.Contains("model", StringComparison.OrdinalIgnoreCase));

    /// <summary>Réduit la photo avant l'envoi : Groq refuse les images trop lourdes.</summary>
    private static async Task<(string DataUrl, string MimeType)> BuildImageDataUrlAsync(
        string imagePath,
        CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(imagePath, cancellationToken).ConfigureAwait(false);
        var mimeType = Path.GetExtension(imagePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };

        try
        {
            using var source = new MemoryStream(bytes, writable: false);
            var image = PlatformImage.FromStream(source);
            const int maxDimension = 1600;

            if (bytes.Length > 3_500_000 || image.Width > maxDimension || image.Height > maxDimension)
            {
                image = image.Downsize(maxDimension, true);
                using var resized = new MemoryStream();
                image.Save(resized, ImageFormat.Jpeg, 0.82f);
                bytes = resized.ToArray();
                mimeType = "image/jpeg";
            }
        }
        catch (Exception)
        {
            // Format exotique (HEIC…) : on envoie l'original et Groq décidera.
        }

        return ($"data:{mimeType};base64,{Convert.ToBase64String(bytes)}", mimeType);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        IReadOnlyList<WireMessage> messages,
        double temperature,
        bool stream,
        CancellationToken cancellationToken)
    {
        var key = await RequireKeyAsync().ConfigureAwait(false);
        var payload = new Dictionary<string, object?>
        {
            ["model"] = _options.Model,
            ["messages"] = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
            ["temperature"] = temperature,
            ["max_tokens"] = _options.MaxOutputTokens,
            ["stream"] = stream
        };

        if (_options.Model.Contains("gpt-oss", StringComparison.OrdinalIgnoreCase))
        {
            payload["reasoning_effort"] = "low";
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(stream ? "text/event-stream" : "application/json"));
        return request;
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpRequestMessage request,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        var payload = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        while (true)
        {
            attempt++;
            var current = attempt == 1 ? request : CloneWithPayload(request, payload!);
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(current, completionOption, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (attempt <= _options.MaxRetries)
            {
                _logger.LogWarning(ex, "Erreur réseau Groq, tentative {Attempt}", attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(600 * Math.Pow(2, attempt - 1)), cancellationToken).ConfigureAwait(false);
                continue;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new AiServiceException("Délai d'attente dépassé lors de l'appel à Groq.", ex);
            }

            if (IsTransient(response.StatusCode) && attempt <= _options.MaxRetries)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMilliseconds(600 * Math.Pow(2, attempt - 1));
                _logger.LogWarning("Groq a répondu {Status}, nouvelle tentative dans {Delay}", (int)response.StatusCode, retryAfter);
                response.Dispose();
                await Task.Delay(retryAfter, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var status = (int)response.StatusCode;
                var message = status switch
                {
                    401 or 403 => "Clé Groq refusée. Vérifie-la dans Profil › Clé API IA.",
                    429 => "Quota Groq atteint. Attends une minute puis réessaie.",
                    404 => "Le modèle Groq configuré est introuvable. Vérifie appsettings.json.",
                    _ => TryReadErrorMessage(errorBody) ?? $"Erreur Groq ({status})."
                };

                response.Dispose();
                throw new AiApiException(message, status);
            }

            return response;
        }
    }

    private static HttpRequestMessage CloneWithPayload(HttpRequestMessage original, string payload)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status == HttpStatusCode.TooManyRequests || (int)status >= 500;

    private async Task<string> RequireKeyAsync()
    {
        var key = await _apiKeys.GetKeyAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new AiNotConfiguredException();
        }

        return key;
    }

    private CancellationTokenSource CreateTimeoutSource(CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        return source;
    }

    private static string? TryReadMessageContent(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                return null;
            }

            var message = choices[0].GetProperty("message");
            return message.TryGetProperty("content", out var content) ? content.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryReadFinishReason(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var choices = document.RootElement.GetProperty("choices");
            return choices.GetArrayLength() == 0 ? null : choices[0].GetProperty("finish_reason").GetString();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? TryReadErrorMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error) &&
                   error.TryGetProperty("message", out var message)
                ? message.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryReadStreamDelta(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var choices = document.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() == 0)
            {
                return null;
            }

            return choices[0].TryGetProperty("delta", out var delta) &&
                   delta.TryGetProperty("content", out var content)
                ? content.GetString()
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static List<AiGeneratedCard> ParseGeneratedCards(string raw)
    {
        var json = ExtractJsonObject(raw);
        if (json is null)
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var array = root.TryGetProperty("cards", out var cards) ? cards : root;
            if (array.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var results = new List<AiGeneratedCard>();
            foreach (var item in array.EnumerateArray())
            {
                var question = item.TryGetProperty("question", out var q) ? q.GetString() : null;
                var answer = item.TryGetProperty("answer", out var a) ? a.GetString() : null;
                if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
                {
                    continue;
                }

                results.Add(new AiGeneratedCard
                {
                    Question = question.Trim(),
                    Answer = answer.Trim(),
                    Hint = item.TryGetProperty("hint", out var h) ? h.GetString()?.Trim() ?? string.Empty : string.Empty,
                    Difficulty = ParseDifficulty(item)
                });
            }

            return results;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static DifficultyLevel ParseDifficulty(JsonElement item)
    {
        if (!item.TryGetProperty("difficulty", out var value))
        {
            return DifficultyLevel.Medium;
        }

        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return Enum.TryParse<DifficultyLevel>(text, ignoreCase: true, out var parsed)
            ? parsed
            : DifficultyLevel.Medium;
    }

    private static string? ExtractJsonObject(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        return start >= 0 && end > start ? raw[start..(end + 1)] : null;
    }

    private sealed record WireMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);
}
