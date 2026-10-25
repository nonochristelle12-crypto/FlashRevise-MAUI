using FlashRevise.Models;

namespace FlashRevise.Services;

/// <summary>
/// Conserve le bilan de la dernière session entre l'écran de révision et l'écran de synthèse
/// (évite de faire transiter un objet complexe dans les paramètres de navigation Shell).
/// </summary>
public sealed class SessionOutcomeStore
{
    public SessionResult? Last { get; set; }
}
