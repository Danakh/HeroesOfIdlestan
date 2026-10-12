using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using HeroesOfIdlestan.UI;

namespace HeroesOfIdlestan.AnimationTool;

/// <summary>
/// Banc d'essai des animations. Lit le dossier passé en argument, sinon <c>assets/animation/</c> du
/// dépôt (pour que « Recharger » prenne une retouche sans recompiler), sinon la copie posée à côté
/// de l'exécutable.
/// </summary>
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.Root = args.Length > 0 ? Path.GetFullPath(args[0]) : FindAssetsRoot();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();

    private static string FindAssetsRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "HeroesOfIdlestan.slnx")))
                return Path.Combine(dir.FullName, "assets", "animation");
        }
        return Path.Combine(AppContext.BaseDirectory, "assets", "animation");
    }
}

public sealed class App : Application
{
    public static string Root { get; set; } = "";

    public override void Initialize() => GameTheme.Apply(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow(Root);
        base.OnFrameworkInitializationCompleted();
    }
}

public sealed class MainWindow : Window
{
    private readonly MainView _view;

    public MainWindow(string root)
    {
        Title = "HeroesOfIdlestan — Animations";
        Width = 1100;
        Height = 700;
        Background = Brushes.Black;
        Content = _view = new MainView(root);
    }

    protected override void OnClosed(EventArgs e)
    {
        _view.Dispose();
        base.OnClosed(e);
    }
}
