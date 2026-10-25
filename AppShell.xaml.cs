using FlashRevise.Views;
using Microsoft.Extensions.DependencyInjection;

namespace FlashRevise;

public partial class AppShell : Shell
{
    private readonly IServiceProvider _services;

    public AppShell(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;

        RootContent.ContentTemplate = new DataTemplate(() => services.GetRequiredService<MainTabsPage>());

        RegisterRoutes();
    }

    /// <summary>
    /// Les pages de détail sont résolues par le conteneur DI, ce qui permet
    /// l'injection des ViewModels et des services dans leur constructeur.
    /// </summary>
    private void RegisterRoutes()
    {
        AddRoute<Views.SplashPage>(Routes.Splash);
        AddRoute<Views.OnboardingPage>(Routes.Onboarding);
        AddRoute<Views.Subjects.SubjectDetailPage>(Routes.SubjectDetail);
        AddRoute<Views.Subjects.FlashcardEditPage>(Routes.FlashcardEdit);
        AddRoute<Views.Revision.RevisionConfigPage>(Routes.RevisionConfig);
        AddRoute<Views.Revision.RevisionSessionPage>(Routes.RevisionSession);
        AddRoute<Views.Revision.SessionSummaryPage>(Routes.SessionSummary);
        AddRoute<Views.AI.AiUploadPage>(Routes.AiUpload);
        AddRoute<Views.AI.AiLoadingPage>(Routes.AiLoading);
        AddRoute<Views.AI.AiReviewPage>(Routes.AiReview);
        AddRoute<Views.AI.AiChatPage>(Routes.AiChat);
        AddRoute<Views.Settings.ScheduleRevisionPage>(Routes.ScheduleRevision);
        AddRoute<Views.Settings.RemindersPage>(Routes.Reminders);
        AddRoute<Views.Settings.SubjectStatsPage>(Routes.SubjectStats);
        AddRoute<Views.Settings.GoalsBooksPage>(Routes.GoalsBooks);
        AddRoute<Views.Settings.AchievementsPage>(Routes.Achievements);
        AddRoute<Views.Settings.OfflineStatePage>(Routes.OfflineState);
    }

    private void AddRoute<TPage>(string route) where TPage : Page
    {
        try
        {
            Routing.RegisterRoute(route, new ServiceRouteFactory<TPage>(_services));
        }
        catch (ArgumentException)
        {
            // Android peut recréer l'activité sans nouveau processus : les routes
            // du registre statique sont alors déjà présentes.
        }
    }

    private sealed class ServiceRouteFactory<TPage>(IServiceProvider services) : RouteFactory
        where TPage : Page
    {
        public override Element GetOrCreate() => services.GetRequiredService<TPage>();

        public override Element GetOrCreate(IServiceProvider serviceProvider) =>
            serviceProvider.GetRequiredService<TPage>();
    }
}
