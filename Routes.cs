namespace FlashRevise;

/// <summary>Noms de routes Shell, centralisés pour éviter les chaînes magiques.</summary>
public static class Routes
{
    public const string Main = "//main";
    public const string Splash = "splash";
    public const string Onboarding = "onboarding";
    public const string SubjectDetail = "subject";
    public const string FlashcardEdit = "flashcard";
    public const string RevisionConfig = "revision-config";
    public const string RevisionSession = "revision-session";
    public const string SessionSummary = "summary";
    public const string AiUpload = "ai-upload";
    public const string AiLoading = "ai-loading";
    public const string AiReview = "ai-review";
    public const string AiChat = "ai-chat";
    public const string ScheduleRevision = "schedule";
    public const string Reminders = "reminders";
    public const string SubjectStats = "subject-stats";
    public const string GoalsBooks = "goals";
    public const string Achievements = "achievements";
    public const string OfflineState = "offline";

    public static string WithId(string route, int id) => $"{route}?id={id}";
}
