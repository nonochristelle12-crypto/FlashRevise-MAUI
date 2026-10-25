using System.Text;
using FlashRevise.Models;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace FlashRevise.Services;

/// <inheritdoc cref="IStorageService"/>
public sealed class StorageService : IStorageService
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".markdown", ".csv", ".json"
    };

    private static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase) { ".pdf" };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".heic"
    };

    private readonly ILogger<StorageService> _logger;

    public StorageService(ILogger<StorageService> logger)
    {
        _logger = logger;
        ImportsDirectory = Path.Combine(FileSystem.AppDataDirectory, "imports");
        Directory.CreateDirectory(ImportsDirectory);
    }

    public string ImportsDirectory { get; }

    public async Task<ImportedFile?> PickAndImportAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Choisir un support de cours"
        }).ConfigureAwait(false);

        if (result is null)
        {
            return null;
        }

        var extension = Path.GetExtension(result.FileName);
        var destination = Path.Combine(ImportsDirectory, $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Sanitize(result.FileName)}");

        await using var source = await result.OpenReadAsync().ConfigureAwait(false);
        await using var target = File.Create(destination);
        await source.CopyToAsync(target).ConfigureAwait(false);

        var sourceType = TextExtensions.Contains(extension) || PdfExtensions.Contains(extension)
            ? (PdfExtensions.Contains(extension) ? AiSourceType.Pdf : AiSourceType.Text)
            : ImageExtensions.Contains(extension)
                ? AiSourceType.Image
                : AiSourceType.Text;

        return new ImportedFile(destination, result.FileName, extension, new FileInfo(destination).Length, sourceType);
    }

    public async Task<string> ExtractTextAsync(ImportedFile file, CancellationToken cancellationToken = default)
    {
        if (TextExtensions.Contains(file.Extension))
        {
            return await File.ReadAllTextAsync(file.Path, cancellationToken).ConfigureAwait(false);
        }

        if (PdfExtensions.Contains(file.Extension))
        {
            return await Task.Run(() => ExtractPdfText(file.Path), cancellationToken).ConfigureAwait(false);
        }

        if (ImageExtensions.Contains(file.Extension))
        {
            throw new NotSupportedException(
                "L'extraction de texte depuis une image n'est pas disponible hors-ligne. " +
                "Collez le texte du document ou utilisez un PDF ; l'OCR Windows sera activable dans une prochaine version.");
        }

        throw new NotSupportedException($"Format non pris en charge : {file.Extension}.");
    }

    public async Task<string> SaveImageAsync(string sourcePath, string suggestedName)
    {
        Directory.CreateDirectory(ImportsDirectory);
        var destination = Path.Combine(ImportsDirectory, $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Sanitize(suggestedName)}");
        await using var source = File.OpenRead(sourcePath);
        await using var target = File.Create(destination);
        await source.CopyToAsync(target).ConfigureAwait(false);
        return destination;
    }

    /// <summary>
    /// PdfPig est utilisé plutôt qu'un OCR : il lit directement la couche texte des PDF,
    /// fonctionne sur Android et Windows, et reste hors-ligne.
    /// </summary>
    private string ExtractPdfText(string path)
    {
        var builder = new StringBuilder();
        try
        {
            using var document = PdfDocument.Open(path);
            foreach (var page in document.GetPages())
            {
                builder.AppendLine(page.Text);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Extraction PDF impossible pour {Path}", path);
            throw new InvalidOperationException(
                "Ce PDF n'a pas de couche texte exploitable (document scanné).", ex);
        }

        var text = builder.ToString().Trim();
        if (text.Length == 0)
        {
            throw new InvalidOperationException("Ce PDF ne contient pas de texte sélectionnable.");
        }

        return text;
    }

    private static string Sanitize(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(fileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return cleaned.Length > 80 ? cleaned[^80..] : cleaned;
    }
}
