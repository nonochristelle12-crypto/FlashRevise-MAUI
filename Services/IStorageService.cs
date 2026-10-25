using FlashRevise.Models;

namespace FlashRevise.Services;

/// <summary>Fichier importé depuis le stockage de l'appareil.</summary>
public sealed record ImportedFile(string Path, string FileName, string Extension, long SizeBytes, AiSourceType SourceType);

/// <summary>Import et extraction de texte des supports de cours.</summary>
public interface IStorageService
{
    /// <summary>Ouvre le sélecteur de fichiers et copie le document dans le dossier de l'application.</summary>
    Task<ImportedFile?> PickAndImportAsync();

    /// <summary>Extrait le texte exploitable d'un document importé (txt, md, csv, pdf).</summary>
    Task<string> ExtractTextAsync(ImportedFile file, CancellationToken cancellationToken = default);

    /// <summary>Copie une pièce jointe dans le dossier privé de l'application.</summary>
    Task<string> SaveImageAsync(string sourcePath, string suggestedName);

    /// <summary>Copie un flux photo (caméra, galerie) dans le dossier privé de l'application.</summary>
    Task<string> SaveImageAsync(Stream source, string suggestedName);

    string ImportsDirectory { get; }
}
