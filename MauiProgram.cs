using CommunityToolkit.Maui;
using FlashRevise.Data;
using FlashRevise.Data.Repositories;
using FlashRevise.Services;
using FlashRevise.Services.Ai;
using FlashRevise.ViewModels;
using FlashRevise.Views;
using LiveChartsCore.SkiaSharpView.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace FlashRevise;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseLocalNotification()
            .UseSkiaSharp()
            .UseLiveCharts()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("SpaceGrotesk.ttf", "SpaceGrotesk");
                fonts.AddFont("Inter.ttf", "Inter");
                fonts.AddFont("MaterialSymbols.ttf", "MaterialSymbols");
            });

        AddConfiguration(builder);
        AddDataLayer(builder.Services);
        AddServices(builder.Services, builder.Configuration);
        AddViewModels(builder.Services);
        AddPages(builder.Services);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        return app;
    }

    /// <summary>
    /// appsettings.json est embarqué dans le paquet (MauiAsset) : il ne contient
    /// que des paramètres non sensibles, la clé Groq vit dans le SecureStorage.
    /// </summary>
    private static void AddConfiguration(MauiAppBuilder builder)
    {
        using var stream = FileSystem.OpenAppPackageFileAsync("appsettings.json").GetAwaiter().GetResult();
        builder.Configuration.AddJsonStream(stream);
    }

    private static void AddDataLayer(IServiceCollection services)
    {
        services.AddSingleton(_ => new FlashReviseDatabase(
            Path.Combine(FileSystem.AppDataDirectory, FlashReviseDatabase.FileName)));

        services.AddSingleton<ISubjectRepository, SubjectRepository>();
        services.AddSingleton<IFlashcardRepository, FlashcardRepository>();
        services.AddSingleton<IReviewLogRepository, ReviewLogRepository>();
        services.AddSingleton<IReviewSessionRepository, ReviewSessionRepository>();
        services.AddSingleton<IReviewScheduleRepository, ReviewScheduleRepository>();
        services.AddSingleton<IReminderRepository, ReminderRepository>();
        services.AddSingleton<IUserProfileRepository, UserProfileRepository>();
        services.AddSingleton<IBookRepository, BookRepository>();
        services.AddSingleton<IAiGenerationJobRepository, AiGenerationJobRepository>();
        services.AddSingleton<IAchievementRepository, AchievementRepository>();
        services.AddSingleton<ISyncQueueRepository, SyncQueueRepository>();
    }

    private static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var groq = configuration.GetSection(GroqOptions.SectionName);
        services.Configure<GroqOptions>(groq);
        services.Configure<SyncOptions>(configuration.GetSection(SyncOptions.SectionName));

        services.AddSingleton<IApiKeyStore, SecureApiKeyStore>();
        services.AddSingleton<AiDraftStore>();
        services.AddSingleton<SessionOutcomeStore>();
        services.AddSingleton<FlashRevise.Services.INotificationService, NotificationService>();
        services.AddSingleton<IOfflineService, OfflineService>();
        services.AddSingleton<ISyncService, SyncService>();
        services.AddSingleton<ISpacedRepetitionService, Sm2SpacedRepetitionService>();
        services.AddSingleton<IStatsService, StatsService>();
        services.AddSingleton<ISubjectService, SubjectService>();
        services.AddSingleton<IFlashcardService, FlashcardService>();
        services.AddSingleton<IReviewSessionService, ReviewSessionService>();
        services.AddSingleton<IReminderService, ReminderService>();
        services.AddSingleton<IStorageService, StorageService>();
        services.AddSingleton<IBookService, BookService>();
        services.AddSingleton<DataSeeder>();
        services.AddSingleton<AppBootstrapper>();

        services.AddHttpClient<IAiService, GroqAiService>(client =>
        {
            var baseUrl = groq["BaseUrl"];
            client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(baseUrl) ? "https://api.groq.com/openai/v1/" : baseUrl);
            client.Timeout = TimeSpan.FromSeconds(180);
        });

        services.AddHttpClient(nameof(SyncService), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
    }

    private static void AddViewModels(IServiceCollection services)
    {
        services.AddTransient<HomeViewModel>();
        services.AddTransient<SubjectsViewModel>();
        services.AddTransient<StatsViewModel>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<OnboardingViewModel>();
        services.AddTransient<SubjectDetailViewModel>();
        services.AddTransient<FlashcardEditViewModel>();
        services.AddTransient<RevisionConfigViewModel>();
        services.AddTransient<RevisionSessionViewModel>();
        services.AddTransient<SessionSummaryViewModel>();
        services.AddTransient<AiUploadViewModel>();
        services.AddTransient<AiLoadingViewModel>();
        services.AddTransient<AiReviewViewModel>();
        services.AddTransient<AiChatViewModel>();
        services.AddTransient<ScheduleRevisionViewModel>();
        services.AddTransient<RemindersViewModel>();
        services.AddTransient<SubjectStatsViewModel>();
        services.AddTransient<GoalsBooksViewModel>();
        services.AddTransient<AchievementsViewModel>();
        services.AddTransient<OfflineStateViewModel>();
    }

    private static void AddPages(IServiceCollection services)
    {
        services.AddTransient<MainTabsPage>();
        services.AddTransient<SplashPage>();
        services.AddTransient<OnboardingPage>();
        services.AddTransient<Views.Subjects.SubjectDetailPage>();
        services.AddTransient<Views.Subjects.FlashcardEditPage>();
        services.AddTransient<Views.Revision.RevisionConfigPage>();
        services.AddTransient<Views.Revision.RevisionSessionPage>();
        services.AddTransient<Views.Revision.SessionSummaryPage>();
        services.AddTransient<Views.AI.AiUploadPage>();
        services.AddTransient<Views.AI.AiLoadingPage>();
        services.AddTransient<Views.AI.AiReviewPage>();
        services.AddTransient<Views.AI.AiChatPage>();
        services.AddTransient<Views.Settings.ScheduleRevisionPage>();
        services.AddTransient<Views.Settings.RemindersPage>();
        services.AddTransient<Views.Settings.SubjectStatsPage>();
        services.AddTransient<Views.Settings.GoalsBooksPage>();
        services.AddTransient<Views.Settings.AchievementsPage>();
        services.AddTransient<Views.Settings.OfflineStatePage>();
    }
}
