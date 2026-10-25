using FlashRevise.Services;

namespace FlashRevise;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly AppBootstrapper _bootstrapper;
    private readonly INotificationService _notifications;
    private bool _launched;

    public App(IServiceProvider services, AppBootstrapper bootstrapper, INotificationService notifications)
    {
        InitializeComponent();
        _services = services;
        _bootstrapper = bootstrapper;
        _notifications = notifications;
        _notifications.ReminderTapped += OnReminderTapped;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var shell = new AppShell(_services);
        var window = new Window(shell);
        window.Created += async (_, _) => await LaunchAsync(shell);
        return window;
    }

    private async Task LaunchAsync(AppShell shell)
    {
        if (_launched)
        {
            return;
        }

        _launched = true;
        try
        {
            await shell.GoToAsync(Routes.Splash);
            await _bootstrapper.InitializeAsync();

            var onboarded = Preferences.Default.Get(AppPreferences.OnboardingCompleted, false);
            await shell.GoToAsync(onboarded ? Routes.Main : Routes.Onboarding);
        }
        catch (Exception)
        {
            // Démarrage dégradé : on affiche l'écran principal plutôt qu'un crash.
            await Shell.Current.GoToAsync(Routes.Main);
        }
    }

    private async void OnReminderTapped(object? sender, int subjectId)
    {
        try
        {
            await Shell.Current.GoToAsync($"{Routes.RevisionConfig}?subjectId={subjectId}");
        }
        catch (Exception)
        {
            // Navigation ignorée si l'application n'est pas encore prête.
        }
    }
}
