using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using HeroesOfIdlestan.Core.Presentation.ViewModels;

namespace HeroesOfIdlestan.Core.UI.Views;

/// <summary>
/// Menu principal, construit en code et lié à <see cref="MenuViewModel"/> : un voile sur toute la
/// fenêtre (il prend les clics destinés au champ de bataille) et une colonne de boutons au centre.
/// </summary>
public sealed class MenuView : Border
{
    private const double ButtonWidth = 240;

    public MenuView(MenuViewModel viewModel)
    {
        DataContext = viewModel;
        Background = new SolidColorBrush(Color.FromArgb(0xC0, 0x10, 0x14, 0x1A));
        this.Bind(IsVisibleProperty, new Binding(nameof(MenuViewModel.Visible)));

        var title = new TextBlock
        {
            FontSize = 32,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16),
        };
        title.Bind(TextBlock.TextProperty, new Binding(nameof(MenuViewModel.Title)));

        var resume = MenuButton(nameof(MenuViewModel.ContinueLabel), viewModel.Continue);
        resume.Bind(IsVisibleProperty, new Binding(nameof(MenuViewModel.CanContinue)));

        Child = new StackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                title,
                resume,
                MenuButton(nameof(MenuViewModel.NewGameLabel), viewModel.NewGame),
                MenuButton(nameof(MenuViewModel.DebugGameLabel), viewModel.DebugGame),
            },
        };
    }

    private static Button MenuButton(string labelProperty, Action click)
    {
        var button = new Button
        {
            Width = ButtonWidth,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(12, 8),
        };
        button.Bind(ContentControl.ContentProperty, new Binding(labelProperty));
        button.Click += (_, _) => click();
        return button;
    }
}
