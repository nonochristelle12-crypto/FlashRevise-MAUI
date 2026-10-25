using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Models;
using FlashRevise.Services;
using FlashRevise.Services.Ai;
using Microsoft.Maui.Media;

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
    public partial string SubjectNameInput { get; set; } = string.Empty;

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
    public partial bool IsImageSource { get; set; }

    [ObservableProperty]
    public partial bool CanScan { get; set; }

    [ObservableProperty]
    public partial string FilePreviewPath { get; set; } = string.Empty;

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

    partial void OnSelectedSubjectIndexChanged(int value)
    {
        if (value >= 0 && value < Subjects.Count)
        {
            SubjectNameInput = Subjects[value].Name;
        }
    }

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

            if (string.IsNullOrWhiteSpace(SubjectNameInput) && Subjects.Count > 0)
            {
                SelectedSubjectIndex = 0;
                SubjectNameInput = Subjects[0].Name;
            }

            CanScan = MediaPicker.Default.IsCaptureSupported;
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
            IsFileMode = true;
            FileName = file.FileName;
            FileMeta = $"{FormatSize(file.SizeBytes)} · {file.Extension.TrimStart('.').ToUpperInvariant()}";
            IsImageSource = file.SourceType == AiSourceType.Image;
            FilePreviewPath = IsImageSource ? file.Path : string.Empty;
            HasFile = true;
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    [RelayCommand]
    private async Task ScanPhotoAsync()
    {
        if (!MediaPicker.Default.IsCaptureSupported)
        {
            ErrorMessage = "La caméra n'est pas disponible sur cet appareil. Utilise « Galerie ».";
            return;
        }

        ClearError();
        try
        {
            var photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = "Scanner un document"
            });

            if (photo is not null)
            {
                await AttachPhotoAsync(photo, "Document scanné");
            }
        }
        catch (PermissionException)
        {
            ErrorMessage = "Autorise l'accès à l'appareil photo dans les réglages Android.";
        }
        catch (FeatureNotSupportedException)
        {
            ErrorMessage = "La caméra n'est pas disponible ici. Choisis une photo dans la galerie.";
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    [RelayCommand]
    private async Task PickPhotoAsync()
    {
        ClearError();
        try
        {
            var photos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
            {
                Title = "Choisir une photo de cours"
            });

            var photo = photos?.FirstOrDefault();
            if (photo is not null)
            {
                await AttachPhotoAsync(photo, "Photo de cours");
            }
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    private async Task AttachPhotoAsync(FileResult photo, string displayName)
    {
        var extension = Path.GetExtension(photo.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".jpg";
        }

        await using var stream = await photo.OpenReadAsync();
        var path = await _storage.SaveImageAsync(stream, $"scan_{DateTime.UtcNow:yyyyMMddHHmmss}{extension}");

        _importedFile = new ImportedFile(path, displayName, extension, new FileInfo(path).Length, AiSourceType.Image);
        IsFileMode = true;
        IsImageSource = true;
        FileName = displayName;
        FileMeta = "Photo · lecture IA du texte et du code";
        FilePreviewPath = path;
        HasFile = true;
    }

    [RelayCommand]
    private void ClearFile()
    {
        _importedFile = null;
        FileName = string.Empty;
        FileMeta = string.Empty;
        IsImageSource = false;
        FilePreviewPath = string.Empty;
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

        var subjectName = SubjectNameInput.Trim();
        if (subjectName.Length < 2)
        {
            ErrorMessage = "Indique le nom de la matière ou du paquet de cartes à créer.";
            return;
        }

        if (IsFileMode && _importedFile is null)
        {
            ErrorMessage = "Importe un document, scanne une photo ou repasse en mode texte libre.";
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
            var imagePath = string.Empty;

            if (IsFileMode && _importedFile is not null)
            {
                sourceType = _importedFile.SourceType;
                sourceName = _importedFile.FileName;

                if (sourceType == AiSourceType.Image)
                {
                    imagePath = _importedFile.Path;
                    if (!File.Exists(imagePath))
                    {
                        ErrorMessage = "La photo importée est introuvable. Scanne à nouveau le document.";
                        return;
                    }
                }
                else
                {
                    IsExtracting = true;
                    text = (await _storage.ExtractTextAsync(_importedFile)).Trim();
                }
            }

            if (sourceType != AiSourceType.Image && text.Length < MinimumTextLength)
            {
                ErrorMessage = "Le document ne contient pas assez de texte exploitable. Essaie un autre fichier.";
                return;
            }

            var subject = await ResolveSubjectAsync(subjectName);

            _draft.Reset();
            _draft.SubjectId = subject.Id;
            _draft.SubjectName = subject.Name;
            _draft.SourceName = sourceName;
            _draft.SourceText = text;
            _draft.SourceImagePath = imagePath;
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

    /// <summary>Réutilise la matière portant ce nom, ou la crée à la volée.</summary>
    private async Task<Subject> ResolveSubjectAsync(string name)
    {
        var subject = Subjects.FirstOrDefault(s =>
            string.Equals(s.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));

        if (subject is null)
        {
            var all = await _subjects.GetSubjectsAsync();
            subject = all.FirstOrDefault(s =>
                string.Equals(s.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
        }

        if (subject is not null)
        {
            return subject;
        }

        subject = new Subject
        {
            Name = name,
            Color = "#6C4CF1",
            Icon = "auto_awesome",
            Description = "Matière créée depuis la génération IA"
        };

        subject.Id = await _subjects.SaveAsync(subject);
        Subjects.Add(subject);
        SubjectNames.Add(subject.Name);
        OnPropertyChanged(nameof(HasSubjects));
        return subject;
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
