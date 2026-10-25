using System.Diagnostics;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.View;
using Avalonia;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
using HeroesOfIdlestan.App.Desktop.Audio;
using HeroesOfIdlestan.Core.Render.Runtime;
using HeroesOfIdlestan.Core.UI;
using HeroesOfIdlestan.Core.UI.Views;

namespace HeroesOfIdlestan.App.Android;

/// <summary>Objet Application du processus : construit l'AppBuilder (pendant de <c>Program</c> sur le bureau).</summary>
[Application]
public sealed class AndroidApp : AvaloniaAndroidApplication<App>
{
    public AndroidApp(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer) { }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).LogToTrace();
}

public sealed class App : Avalonia.Application
{
    public override void Initialize() => GameTheme.Apply(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            singleView.MainView = new AndroidGameView();
        base.OnFrameworkInitializationCompleted();
    }
}

[Activity(
    Label = "HeroesOfIdlestan",
    Theme = "@style/Theme.AppCompat.NoActionBar",
    MainLauncher = true,
    // Rotation, multi-fenêtre et clavier ne doivent pas recréer l'activité : une recréation relancerait le runtime.
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize |
                           ConfigChanges.UiMode | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
public sealed class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        EnterImmersiveMode();
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        // Le système réaffiche les barres au retour d'arrière-plan : on les recache.
        if (hasFocus) EnterImmersiveMode();
    }

    /// <summary>Plein écran immersif : barres système cachées, réaffichables d'un glissement depuis le bord.</summary>
    private void EnterImmersiveMode()
    {
        if (Window is not { } window) return;
        if (WindowCompat.GetInsetsController(window, window.DecorView) is not { } controller) return;
        controller.Hide(WindowInsetsCompat.Type.SystemBars());
        controller.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
    }
}

/// <summary>
/// Racine du head Android, pendant de <c>MainWindow</c> et <c>BrowserGameView</c>. Avalonia.Android
/// gère le rendu, le tactile et la densité d'écran.
/// </summary>
public sealed class AndroidGameView : Border
{
    private static readonly TimeSpan WatchdogInterval = TimeSpan.FromSeconds(1);

    /// <summary>Écart au-delà duquel on considère que le processus a été gelé (un frame lent ne compte pas).</summary>
    private const double StallThresholdSeconds = 3.0;

    private readonly GameRuntimeHost _host;
    private readonly Stopwatch _sinceLastWatch = Stopwatch.StartNew();

    public AndroidGameView()
    {
        Background = Brushes.Black;

        // Bruitages coupés par défaut en arrière-plan : un téléphone qui sonne dans la poche surprend.
        var runtime = new GameRuntime(new AndroidFileSystemService(),
            audio: new NativeAudioService(), playSoundInBackground: false);
        _host = new GameRuntimeHost(runtime);
        Child = new GameView(_host);

        // Arrière-plan = activité en pause. La boucle continue tant qu'Android ne gèle pas le processus :
        // les bruitages doivent donc être tus explicitement, et la partie sauvegardée.
        if (Avalonia.Application.Current?.TryGetFeature<IActivatableLifetime>() is { } lifetime)
        {
            lifetime.Deactivated += (_, e) => { if (e.Kind == ActivationKind.Background) _host.SetInBackground(true); };
            lifetime.Activated += (_, e) => { if (e.Kind == ActivationKind.Background) _host.SetInBackground(false); };
        }

        DispatcherTimer.Run(WatchForStalls, WatchdogInterval);
    }

    /// <summary>
    /// Rattrapage du temps perdu quand Android gèle le processus. La boucle tourne encore un moment en
    /// arrière-plan (l'horloge avance normalement), puis Android gèle l'application sans prévenir :
    /// rattraper toute la durée d'absence compterait deux fois le temps déjà joué. Cette sentinelle
    /// tourne sur le thread de la boucle ; si elle a été privée de main, la boucle aussi, et seul cet
    /// écart est versé à la banque.
    /// </summary>
    private bool WatchForStalls()
    {
        double elapsed = _sinceLastWatch.Elapsed.TotalSeconds;
        _sinceLastWatch.Restart();
        if (elapsed > StallThresholdSeconds)
            _host.CreditStalledTime(elapsed - WatchdogInterval.TotalSeconds);
        return true;
    }
}
