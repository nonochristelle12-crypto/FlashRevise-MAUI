using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Models;
using FlashRevise.Services;
using FlashRevise.Services.Ai;

namespace FlashRevise.ViewModels;

/// <summary>Saisie d'un texte de cours ou import d'un document avant génération IA.</summary>
public partial class AiUploadViewModel : BaseViewModel
{
    private const int MinimumTextLength = 40;

    private readonly ISubjectService _subjects;
    private readonly IStorageService _storage;
    private readonly IApiKeyStore _apiKeys;
    private readonly IAiService _ai;
    private readonly AiDraftStore _draft;

    private ImportedFile? _importedFile;

    [ObservableProperty]
    public partial int SelectedSubjectIndex { get; set; }

    [ObservableProperty]
    public partial int SelectedCountIndex { get; set; } = 1;

    [ObservableProperty]
    public partial string SourceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsFileMode { get; set; }

    [ObservableProperty]
    public partial string FileName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FileMeta { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasFile { get; set; }

    [ObservableProperty]
    public partial bool IsExtracting { get; set; }

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

    public AiUploadViewModel(
        ISubjectService subjects,
        IStorageService storage,
        IApiKeyStore apiKeys,
        IAiService ai,
        AiDraftStore draft)
    {
        _subjects = subjects;
        _storage = storage;
        _apiKeys = apiKeys;
        _ai = ai;
        _draft = draft;
        Title = "Générer avec l'IA";
    }

    public ObservableCollection<Subject> Subjects { get; } = [];

    public ObservableCollection<string> SubjectNames { get; } = [];

    public IReadOnlyList<CountOption> CountOptions { get; } =
    [
        new(5, "5 cartes"),
        new(10, "10 cartes"),
        new(15, "15 cartes"),
        new(20, "20 cartes")
    ];

    public IReadOnlyList<string> CountLabels { get; } = ["5 cartes", "10 cartes", "15 cartes", "20 cartes"];

    public bool HasSubjects => Subjects.Count > 0;

    public bool HasText => !string.IsNullOrWhiteSpace(SourceText);

    public string TextCounter => $"{SourceText.Length} caractères";

    public string FileBadge => HasFile ? "Fichier prêt" : "Aucun fichier";

    partial void OnSourceTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasText));
        OnPropertyChanged(nameof(TextCounter));
    }

    partial void OnHasFileChanged(bool value) => OnPropertyChanged(nameof(FileBadge));

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
                ApiKeyMessage = "Clé validée : la génération IA est prête.";
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

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ClearError();
        try
        {
            var subjects = await _subjects.GetSubjectsAsync();
            Subjects.Clear();
            SubjectNames.Clear();
            foreach (var subject in subjects)
            {
                Subjects.Add(subject);
                SubjectNames.Add(subject.Name);
            }

            OnPropertyChanged(nameof(HasSubjects));
            HasApiKey = await _apiKeys.HasKeyAsync();
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportFileAsync()
    {
        if (IsExtracting)
        {
            return;
        }

        ClearError();
        try
        {
            var file = await _storage.PickAndImportAsync();
            if (file is null)
            {
                return;
            }

            _importedFile = file;
            FileName = file.FileName;
            FileMeta = $"{FormatSize(file.SizeBytes)} · {file.Extension.TrimStart('.').ToUpperInvariant()}";
            HasFile = true;
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    [RelayCommand]
    private void ClearFile()
    {
        _importedFile = null;
        FileName = string.Empty;
        FileMeta = string.Empty;
        HasFile = false;
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ClearError();

        if (Subjects.Count == 0)
        {
            ErrorMessage = "Crée d'abord une matière pour y ranger les cartes générées.";
            return;
        }

        if (IsFileMode && _importedFile is null)
        {
            ErrorMessage = "Importe un document ou repasse en mode texte libre.";
            return;
        }

        if (!IsFileMode && SourceText.Trim().Length < MinimumTextLength)
        {
            ErrorMessage = $"Ajoute au moins {MinimumTextLength} caractères de cours pour obtenir des cartes pertinentes.";
            return;
        }

        if (!HasApiKey)
        {
            ErrorMessage = "Aucune clé API configurée. Ouvre Profil › Clé API IA pour l'ajouter.";
            return;
        }

        IsBusy = true;
        try
        {
            var text = SourceText.Trim();
            var sourceType = AiSourceType.Text;
            var sourceName = "Texte saisi";

            if (IsFileMode && _importedFile is not null)
            {
                IsExtracting = true;
                text = (await _storage.ExtractTextAsync(_importedFile)).Trim();
                sourceType = _importedFile.SourceType;
                sourceName = _importedFile.FileName;
            }

            if (text.Length < MinimumTextLength)
            {
                ErrorMessage = "Le document ne contient pas assez de texte exploitable. Essaie un autre fichier.";
                return;
            }

            var subject = Subjects[Math.Clamp(SelectedSubjectIndex, 0, Subjects.Count - 1)];

            _draft.Reset();
            _draft.SubjectId = subject.Id;
            _draft.SubjectName = subject.Name;
            _draft.SourceName = sourceName;
            _draft.SourceText = text;
            _draft.RequestedCount = CountOptions[Math.Clamp(SelectedCountIndex, 0, CountOptions.Count - 1)].Value;
            _draft.SourceType = sourceType;

            await Shell.Current.GoToAsync(Routes.AiLoading);
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            IsExtracting = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static Task OpenChatAsync() => Shell.Current.GoToAsync(Routes.AiChat);

    [RelayCommand]
    private static Task AddSubjectAsync() => Shell.Current.GoToAsync(Routes.WithId(Routes.SubjectDetail, 0));

    [RelayCommand]
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / (1024d * 1024d):0.#} Mo",
        >= 1024 => $"{bytes / 1024d:0.#} Ko",
        _ => $"{bytes} o"
    };
}
