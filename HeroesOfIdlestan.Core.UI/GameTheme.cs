using Avalonia;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace HeroesOfIdlestan.Core.UI;

/// <summary>Thème de l'overlay : Fluent, variante sombre imposée (sinon Fluent suit le réglage de l'OS).</summary>
public static class GameTheme
{
    public static void Apply(Application app)
    {
        app.RequestedThemeVariant = ThemeVariant.Dark;
        app.Styles.Add(new FluentTheme());
    }
}
