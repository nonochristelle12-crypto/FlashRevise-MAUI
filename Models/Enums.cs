namespace FlashRevise.Models;

/// <summary>Niveau de difficulté déclaré d'une carte.</summary>
public enum DifficultyLevel
{
    Easy = 0,
    Medium = 1,
    Hard = 2
}

/// <summary>Rythme de répétition d'un rappel.</summary>
public enum RepeatPattern
{
    Daily = 0,
    Weekly = 1,
    Weekdays = 2
}

/// <summary>Provenance d'une génération IA.</summary>
public enum AiSourceType
{
    Text = 0,
    Pdf = 1,
    Image = 2
}

/// <summary>État d'un job de génération IA.</summary>
public enum AiJobStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3
}

/// <summary>Nature d'une opération placée dans la file de synchronisation hors-ligne.</summary>
public enum SyncOperation
{
    Create = 0,
    Update = 1,
    Delete = 2
}

/// <summary>Qualité de rappel au sens SM-2 (0 = oubli total, 5 = rappel parfait).</summary>
public enum RecallQuality
{
    Again = 1,
    Hard = 3,
    Good = 4,
    Easy = 5
}
