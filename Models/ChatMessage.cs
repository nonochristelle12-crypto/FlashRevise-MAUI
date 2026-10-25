namespace FlashRevise.Models;

/// <summary>Rôle d'un message dans une conversation avec l'assistant.</summary>
public enum ChatRole
{
    System = 0,
    User = 1,
    Assistant = 2
}

/// <summary>Message d'une conversation IA (non persisté).</summary>
public sealed class ChatMessage
{
    public ChatRole Role { get; init; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public bool IsUser => Role == ChatRole.User;

    public static ChatMessage User(string content) => new() { Role = ChatRole.User, Content = content };
    public static ChatMessage Assistant(string content) => new() { Role = ChatRole.Assistant, Content = content };
    public static ChatMessage System(string content) => new() { Role = ChatRole.System, Content = content };
}

/// <summary>Carte proposée par l'IA avant validation utilisateur.</summary>
public sealed class AiGeneratedCard
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string Hint { get; set; } = string.Empty;
    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Medium;
    public bool IsAccepted { get; set; } = true;
}
