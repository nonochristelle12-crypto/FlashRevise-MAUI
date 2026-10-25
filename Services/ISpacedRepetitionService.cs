using FlashRevise.Models;

namespace FlashRevise.Services;

/// <summary>Planification des révisions selon l'algorithme SM-2 (variante Anki).</summary>
public interface ISpacedRepetitionService
{
    /// <summary>Calcule le nouvel état de planification d'une carte après une réponse.</summary>
    ReviewSchedule ComputeNextReview(ReviewSchedule? previous, RecallQuality quality, DateTime now);

    /// <summary>Une carte est considérée maîtrisée à partir de 3 rappels réussis et 21 jours d'intervalle.</summary>
    bool IsMastered(ReviewSchedule? schedule);

    /// <summary>Qualité SM-2 correspondant au bouton de notation choisi.</summary>
    RecallQuality MapButton(RecallQuality button) => button;
}
