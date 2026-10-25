using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashRevise.Models;
using FlashRevise.Services;

namespace FlashRevise.ViewModels;

/// <summary>Succès du catalogue, prêt à afficher.</summary>
public sealed class AchievementRow
{
    private static readonly Dictionary<string, string> IconKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["flag"] = "IconFlag",
        ["local_fire_department"] = "IconLocalFireDepartment",
        ["bolt"] = "IconBolt",
        ["military_tech"] = "IconMilitaryTech",
        ["psychology"] = "IconPsychology",
        ["auto_awesome"] = "IconAutoAwesome",
        ["auto_stories"] = "IconAutoStories",
        ["school"] = "IconSchool",
        ["star"] = "IconStar"
    };

    public AchievementRow(AchievementItem item, string glyph)
    {
        Item = item;
        Glyph = glyph;
    }

    public AchievementItem Item { get; }

    public string Glyph { get; }

    public string Title => Item.Definition.Title;

    public string Description => Item.Definition.Description;

    public bool IsUnlocked => Item.IsUnlocked;

    public double Ratio => Item.ProgressRatio;

    public string BadgeLabel => IsUnlocked ? "Débloqué" : "Verrouillé";

    public string ProgressLabel => IsUnlocked
        ? Item.UnlockedAt is null
            ? "Succès débloqué"
            : $"Débloqué le {Item.UnlockedAt.Value.ToLocalTime():d MMM yyyy}"
        : $"{Math.Min(Item.Progress, Item.Definition.Target)} / {Item.Definition.Target}";

    public static string ResolveGlyph(string iconName)
    {
        var key = IconKeys.TryGetValue(iconName, out var mapped) ? mapped : "IconStar";
        return Application.Current?.Resources.TryGetValue(key, out var value) == true && value is string glyph
            ? glyph
            : string.Empty;
    }
}

/// <summary>Catalogue des succès avec progression et état de déblocage.</summary>
public partial class AchievementsViewModel : BaseViewModel
{
    private readonly IStatsService _stats;

    [ObservableProperty]
    public partial string UnlockedLabel { get; set; } = "0 / 0";

    [ObservableProperty]
    public partial double CompletionRatio { get; set; }

    [ObservableProperty]
    public partial string CompletionLabel { get; set; } = "0 %";

    public AchievementsViewModel(IStatsService stats)
    {
        _stats = stats;
        Title = "Succès";
    }

    public ObservableCollection<AchievementRow> Rows { get; } = [];

    public bool HasRows => Rows.Count > 0;

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
            await _stats.EvaluateAchievementsAsync();
            var items = await _stats.GetAchievementsAsync();

            Rows.Clear();
            foreach (var item in items)
            {
                Rows.Add(new AchievementRow(item, AchievementRow.ResolveGlyph(item.Definition.Icon)));
            }

            var unlocked = Rows.Count(r => r.IsUnlocked);
            UnlockedLabel = $"{unlocked} / {Rows.Count}";
            CompletionRatio = Rows.Count == 0 ? 0 : (double)unlocked / Rows.Count;
            CompletionLabel = $"{Math.Round(CompletionRatio * 100)} %";
            OnPropertyChanged(nameof(HasRows));
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
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
