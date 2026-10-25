using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Models;
using FlashRevise.Services.Ai;

namespace FlashRevise.ViewModels;

/// <summary>Message affiché dans la conversation avec l'assistant.</summary>
public partial class ChatBubble : ObservableObject
{
    public ChatBubble(string text, bool isUser)
    {
        IsUser = isUser;
        Text = text;
        Timestamp = DateTime.Now;
    }

    [ObservableProperty]
    public partial string Text { get; set; }

    [ObservableProperty]
    public partial bool IsStreaming { get; set; }

    public bool IsUser { get; }

    public bool IsAssistant => !IsUser;

    public DateTime Timestamp { get; }

    public string AuthorLabel => IsUser ? "Toi" : "Assistant FlashRevise";

    public string TimeLabel => Timestamp.ToString("HH:mm");

    public bool IsEmpty => string.IsNullOrEmpty(Text);

    public bool ShowTyping => IsStreaming && IsEmpty;

    partial void OnTextChanged(string value) => OnPropertyChanged(nameof(IsEmpty));

    partial void OnIsStreamingChanged(bool value) => OnPropertyChanged(nameof(ShowTyping));
}

/// <summary>Conversation libre avec l'assistant de révision, en streaming.</summary>
public partial class AiChatViewModel : BaseViewModel
{
    private readonly IAiService _ai;
    private readonly IApiKeyStore _apiKeys;
    private readonly List<ChatMessage> _history = [];

    private CancellationTokenSource? _cancellation;
    private string? _lastPrompt;

    [ObservableProperty]
    public partial string Input { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsStreaming { get; set; }

    [ObservableProperty]
    public partial bool HasApiKey { get; set; } = true;

    [ObservableProperty]
    public partial string ApiKeyInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ApiKeyMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsTestingKey { get; set; }

    public bool HasNoApiKey => !HasApiKey;

    public bool HasApiKeyMessage => !string.IsNullOrWhiteSpace(ApiKeyMessage);

    public AiChatViewModel(IAiService ai, IApiKeyStore apiKeys)
    {
        _ai = ai;
        _apiKeys = apiKeys;
        Title = "Assistant IA";
    }

    public ObservableCollection<ChatBubble> Messages { get; } = [];

    public bool HasMessages => Messages.Count > 0;

    public bool ShowSuggestions => Messages.Count == 0 && !IsStreaming;

    public bool CanSend => !IsStreaming && !string.IsNullOrWhiteSpace(Input);

    partial void OnInputChanged(string value) => OnPropertyChanged(nameof(CanSend));

    partial void OnHasApiKeyChanged(bool value) => OnPropertyChanged(nameof(HasNoApiKey));

    partial void OnApiKeyMessageChanged(string value) => OnPropertyChanged(nameof(HasApiKeyMessage));

    [RelayCommand]
    private async Task SaveApiKeyAsync()
    {
        var key = ApiKeyInput.Trim();
        if (key.Length < 20 || !key.StartsWith("gsk_", StringComparison.Ordinal))
        {
            ApiKeyMessage = "Clé invalide : une clé Groq commence par gsk_.";
            return;
        }

        IsTestingKey = true;
        try
        {
            await _apiKeys.SetKeyAsync(key);
            var connected = await _ai.TestConnectionAsync();
            HasApiKey = await _apiKeys.HasKeyAsync();

            if (connected)
            {
                ApiKeyInput = string.Empty;
                ApiKeyMessage = "Clé validée : tu peux discuter avec l'assistant.";
            }
            else
            {
                ApiKeyMessage = "Clé enregistrée, mais Groq ne répond pas. Vérifie la clé et ta connexion, puis retente.";
            }
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            IsTestingKey = false;
        }
    }

    partial void OnIsStreamingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSend));
        OnPropertyChanged(nameof(ShowSuggestions));
        SendCommand.NotifyCanExecuteChanged();
        RetryCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        ClearError();
        try
        {
            HasApiKey = await _apiKeys.HasKeyAsync();
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    private bool CanSendMessage() => !IsStreaming;

    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    private async Task SendAsync(string? preset)
    {
        var text = string.IsNullOrWhiteSpace(preset) ? Input.Trim() : preset.Trim();
        if (string.IsNullOrWhiteSpace(text) || IsStreaming)
        {
            return;
        }

        ClearError();

        if (!HasApiKey)
        {
            HasApiKey = await _apiKeys.HasKeyAsync();
            if (!HasApiKey)
            {
                ErrorMessage = "Aucune clé API configurée. Ouvre Profil › Clé API IA pour l'ajouter.";
                return;
            }
        }

        Input = string.Empty;
        _lastPrompt = text;
        Messages.Add(new ChatBubble(text, isUser: true));
        OnPropertyChanged(nameof(HasMessages));
        OnPropertyChanged(nameof(ShowSuggestions));

        var reply = new ChatBubble(string.Empty, isUser: false) { IsStreaming = true };
        Messages.Add(reply);

        IsStreaming = true;
        _cancellation = new CancellationTokenSource();
        var buffer = new StringBuilder();

        try
        {
            await foreach (var delta in _ai.StreamChatAsync(_history, text, _cancellation.Token))
            {
                buffer.Append(delta);
                reply.Text = buffer.ToString();
            }

            if (buffer.Length == 0)
            {
                Messages.Remove(reply);
                ErrorMessage = "L'assistant n'a renvoyé aucune réponse. Réessaie dans un instant.";
                return;
            }

            _history.Add(ChatMessage.User(text));
            _history.Add(ChatMessage.Assistant(buffer.ToString()));
        }
        catch (OperationCanceledException)
        {
            if (buffer.Length > 0)
            {
                reply.Text = buffer.ToString();
            }
            else
            {
                Messages.Remove(reply);
            }
        }
        catch (AiNotConfiguredException ex)
        {
            Messages.Remove(reply);
            HasApiKey = false;
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            Messages.Remove(reply);
            ReportError(ex);
        }
        finally
        {
            reply.IsStreaming = false;
            IsStreaming = false;
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    private Task RetryAsync()
    {
        if (string.IsNullOrWhiteSpace(_lastPrompt) || Messages.Count == 0)
        {
            return Task.CompletedTask;
        }

        var prompt = _lastPrompt;
        Messages.RemoveAt(Messages.Count - 1);
        OnPropertyChanged(nameof(HasMessages));
        OnPropertyChanged(nameof(ShowSuggestions));
        return SendAsync(prompt);
    }

    [RelayCommand]
    private void Stop()
    {
        _cancellation?.Cancel();
    }

    [RelayCommand]
    private void Clear()
    {
        _cancellation?.Cancel();
        _history.Clear();
        Messages.Clear();
        ErrorMessage = null;
        OnPropertyChanged(nameof(HasMessages));
        OnPropertyChanged(nameof(ShowSuggestions));
    }

    [RelayCommand]
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");

    [RelayCommand]
    private Task AskMemoryAsync() =>
        SendAsync("Explique-moi simplement la différence entre mémoire à court terme et mémoire à long terme.");

    [RelayCommand]
    private Task AskQuizAsync() =>
        SendAsync("Crée un mini-quiz de 3 questions avec les réponses sur la photosynthèse.");

    [RelayCommand]
    private Task AskMethodAsync() =>
        SendAsync("Propose une méthode concrète pour retenir des dates clés sur plusieurs semaines.");
}
