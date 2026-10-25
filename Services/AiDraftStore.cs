using FlashRevise.Models;

namespace FlashRevise.Services;

/// <summary>Brouillon de génération IA partagé entre l'écran de chargement et l'écran de validation.</summary>
public sealed class AiDraftStore
{
    public int JobId { get; set; }
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string SourceText { get; set; } = string.Empty;
    public string SourceImagePath { get; set; } = string.Empty;
    public int RequestedCount { get; set; } = 10;
    public AiSourceType SourceType { get; set; } = AiSourceType.Text;
    public bool IsReady { get; set; }
    public string? ErrorMessage { get; set; }
    public List<AiGeneratedCard> Cards { get; set; } = [];

    public void Reset()
    {
        JobId = 0;
        SubjectId = 0;
        SubjectName = string.Empty;
        SourceName = string.Empty;
        SourceText = string.Empty;
        SourceImagePath = string.Empty;
        RequestedCount = 10;
        SourceType = AiSourceType.Text;
        IsReady = false;
        ErrorMessage = null;
        Cards = [];
    }
}
