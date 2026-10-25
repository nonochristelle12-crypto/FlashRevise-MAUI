using FlashRevise.Data.Repositories;
using FlashRevise.Models;

namespace FlashRevise.Services;

/// <summary>
/// Insère un jeu de matières et de cartes de démonstration au premier lancement,
/// pour que les écrans ne soient pas vides avant la première saisie.
/// </summary>
public sealed class DataSeeder(
    ISubjectRepository subjects,
    IFlashcardRepository cards,
    IUserProfileRepository profiles)
{
    public async Task SeedAsync()
    {
        var existing = await subjects.GetActiveAsync().ConfigureAwait(false);
        if (existing.Count > 0)
        {
            return;
        }

        var profile = await profiles.GetOrCreateAsync().ConfigureAwait(false);
        if (profile.DailyGoal <= 0)
        {
            profile.DailyGoal = 30;
            await profiles.SaveAsync(profile).ConfigureAwait(false);
        }

        var maths = await subjects.SaveAsync(new Subject
        {
            Name = "Mathématiques",
            Icon = "functions",
            Color = "#6C4CF1",
            Description = "Algèbre, analyse et géométrie — Terminale."
        }).ConfigureAwait(false);

        var biology = await subjects.SaveAsync(new Subject
        {
            Name = "Biologie",
            Icon = "biotech",
            Color = "#34D399",
            Description = "Biologie cellulaire et métabolisme."
        }).ConfigureAwait(false);

        var physics = await subjects.SaveAsync(new Subject
        {
            Name = "Physique-Chimie",
            Icon = "science",
            Color = "#22D3EE",
            Description = "Thermodynamique, ondes et chimie des solutions."
        }).ConfigureAwait(false);

        await AddCardsAsync(maths, [
            ("Quel est l'énoncé formel du théorème de Pythagore ?",
                "Dans un triangle rectangle, le carré de l'hypoténuse est égal à la somme des carrés des deux autres côtés : a² + b² = c².",
                "Pense à un carré posé sur chaque côté.", DifficultyLevel.Easy, "Algèbre"),
            ("Quelle est la dérivée de ln(x) sur ]0 ; +∞[ ?",
                "1/x.", "La fonction inverse est sa propre dérivée primitive.", DifficultyLevel.Medium, "Analyse"),
            ("Comment reconnaît-on une suite arithmético-géométrique ?",
                "Par une relation de récurrence u(n+1) = a·u(n) + b avec a ≠ 1 ; on la résout en cherchant un point fixe.",
                "Cherche le point fixe, puis pose v(n) = u(n) − l.", DifficultyLevel.Hard, "Analyse")
        ]).ConfigureAwait(false);

        await AddCardsAsync(biology, [
            ("Cycle de Krebs : où se déroule-t-il ?",
                "Dans la matrice mitochondriale, chez les eucaryotes.", "Matrice = le milieu gélatineux interne de la mitochondrie.",
                DifficultyLevel.Medium, "Métabolisme"),
            ("Quel est le bilan énergétique net en ATP de la respiration aérobie d'un glucose ?",
                "Environ 30 à 32 ATP, selon la navette utilisée pour les NADH cytosoliques.",
                "Glycolyse 2, Krebs 2, phosphorylation oxydative ~26 à 28.", DifficultyLevel.Hard, "Bioénergétique"),
            ("Quel est le rôle principal de l'ATP synthase lors de la phosphorylation oxydative ?",
                "Produire de l'ATP à partir d'ADP et de phosphate inorganique (Pi) en exploitant le gradient électrochimique de protons transmembranaire.",
                "Pense au rotor hydraulique d'un barrage : les protons font tourner la sous-unité F0.",
                DifficultyLevel.Hard, "Métabolisme")
        ]).ConfigureAwait(false);

        await AddCardsAsync(physics, [
            ("Quelle est l'unité SI de la résistance électrique ?",
                "L'ohm (Ω).", "Georg Simon Ohm — la loi qui porte son nom.", DifficultyLevel.Easy, "Électricité"),
            ("Qu'exprime le premier principe de la thermodynamique ?",
                "La conservation de l'énergie : ΔU = Q + W pour un système fermé au repos.",
                "« Rien ne se perd, rien ne se crée » appliqué à l'énergie.", DifficultyLevel.Medium, "Thermo"),
            ("Quelle est la relation entre fréquence et longueur d'onde d'une onde progressive ?",
                "λ = v / f, où v est la célérité de l'onde et f sa fréquence.", "La vitesse impose une longueur d'onde par période.",
                DifficultyLevel.Medium, "Ondes")
        ]).ConfigureAwait(false);
    }

    private async Task AddCardsAsync(
        int subjectId,
        IReadOnlyList<(string Question, string Answer, string Hint, DifficultyLevel Difficulty, string Tag)> entries)
    {
        foreach (var (question, answer, hint, difficulty, tag) in entries)
        {
            await cards.SaveAsync(new Flashcard
            {
                SubjectId = subjectId,
                Question = question,
                Answer = answer,
                Hint = hint,
                Difficulty = difficulty,
                Tags = tag,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ConfigureAwait(false);
        }
    }
}
