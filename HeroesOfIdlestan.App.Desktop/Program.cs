using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using HeroesOfIdlestan.App.Desktop.Audio;
using HeroesOfIdlestan.Core.Render.Runtime;
using HeroesOfIdlestan.Core.UI;
using HeroesOfIdlestan.Core.UI.Views;

namespace HeroesOfIdlestan.App.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Référencée par le designer Avalonia et par Main.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();
}

public sealed class App : Application
{
    public override void Initialize() => GameTheme.Apply(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}

/// <summary>Fenêtre unique : crée le runtime avec les services du bureau et le confie à l'hôte.</summary>
public sealed class MainWindow : Window
{
    private readonly GameRuntimeHost _host;
    private readonly GameView _view;

    public MainWindow()
    {
        Title = "HeroesOfIdlestan";
        Width = 1280;
        Height = 720;
        Background = Brushes.Black;

        var runtime = new GameRuntime(new DesktopFileSystemService(), audio: new NativeAudioService());
        _host = new GameRuntimeHost(runtime);
        _view = new GameView(_host);
        Content = _view;

        // Fenêtre sans focus = arrière-plan : les bruitages s'y taisent si le joueur l'a choisi.
        Activated += (_, _) => _host.SetInBackground(false);
        Deactivated += (_, _) => _host.SetInBackground(true);
    }

    protected override void OnClosed(EventArgs e)
    {
        _view.Dispose();
        _host.Dispose(); // sauvegarde une dernière fois
        base.OnClosed(e);
    }
}
