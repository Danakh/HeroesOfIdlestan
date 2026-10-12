using Avalonia;
using Avalonia.Browser;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using HeroesOfIdlestan.Browser.Services;
using HeroesOfIdlestan.Render.Runtime;
using HeroesOfIdlestan.UI;
using HeroesOfIdlestan.UI.Views;

namespace HeroesOfIdlestan.Browser;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        // L'interop d'abord : le runtime lit la sauvegarde dès la construction de la vue.
        await BrowserInterop.InitializeAsync();
        await BuildAvaloniaApp().StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<App>()
        .LogToTrace();
}

public sealed class App : Application
{
    public override void Initialize() => GameTheme.Apply(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Pas de fenêtre à gérer : le jeu est la vue unique de la page.
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            singleView.MainView = new BrowserGameView();
        base.OnFrameworkInitializationCompleted();
    }
}

/// <summary>
/// Racine du head navigateur, pendant de <c>MainWindow</c> : crée le runtime avec les services du
/// navigateur et affiche la vue partagée. Le rendu, l'input et le DPI sont gérés par Avalonia.Browser.
/// </summary>
public sealed class BrowserGameView : Border
{
    private readonly GameRuntimeHost _host;

    public BrowserGameView()
    {
        Background = Brushes.Black;

        var runtime = new GameRuntime(new BrowserFileSystemService(), audio: new BrowserAudioService());
        _host = new GameRuntimeHost(runtime);
        Child = new GameView(_host);

        // Un onglet en arrière-plan voit ses timers bridés par le navigateur : le temps perdu est
        // versé à la banque au retour, et les bruitages se taisent pendant l'absence.
        BrowserInterop.RegisterVisibilityHandler(hiddenSeconds => _host.CreditStalledTime(hiddenSeconds));
        BrowserInterop.RegisterHiddenHandler(hidden => _host.SetInBackground(hidden));

        BrowserInterop.AppReady();
    }
}
