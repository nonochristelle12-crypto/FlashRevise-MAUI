using FlashRevise.Models;

namespace FlashRevise.Services;

/// <inheritdoc cref="ISpacedRepetitionService"/>
public sealed class Sm2SpacedRepetitionService : ISpacedRepetitionService
{
    private const double MinimumEaseFactor = 1.3;
    private const double InitialEaseFactor = 2.5;

    /// <inheritdoc />
    public ReviewSchedule ComputeNextReview(ReviewSchedule? previous, RecallQuality quality, DateTime now)
    {
        var schedule = previous ?? new ReviewSchedule
        {
            EaseFactor = InitialEaseFactor,
            Repetitions = 0,
            IntervalDays = 0,
            NextReviewAt = now
        };

        var grade = Math.Clamp((int)quality, 0, 5);
        var easeFactor = schedule.EaseFactor <= 0 ? InitialEaseFactor : schedule.EaseFactor;

        // EF' = EF + (0.1 − (5 − q) × (0.08 + (5 − q) × 0.02))
        var delta = 5 - grade;
        easeFactor += 0.1 - delta * (0.08 + delta * 0.02);
        easeFactor = Math.Max(MinimumEaseFactor, easeFactor);

        int intervalDays;
        int repetitions;

        if (grade < 3)
        {
            // Échec : la carte repart du début, revue dès le lendemain.
            repetitions = 0;
            intervalDays = 1;
            schedule.Lapses++;
        }
        else
        {
            repetitions = schedule.Repetitions + 1;
            intervalDays = repetitions switch
            {
                1 => 1,
                2 => 6,
                _ => (int)Math.Round(Math.Max(1, schedule.IntervalDays) * easeFactor, MidpointRounding.AwayFromZero)
            };
        }

        schedule.EaseFactor = Math.Round(easeFactor, 3);
        schedule.Repetitions = repetitions;
        schedule.IntervalDays = Math.Clamp(intervalDays, 1, 3650);
        schedule.LastReviewedAt = now;
        schedule.NextReviewAt = now.Date.AddDays(schedule.IntervalDays).AddHours(4);

        return schedule;
    }

    /// <inheritdoc />
    public bool IsMastered(ReviewSchedule? schedule) =>
        schedule is not null && schedule.Repetitions >= 3 && schedule.IntervalDays >= 21;
}
