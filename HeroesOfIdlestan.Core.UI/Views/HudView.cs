using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using HeroesOfIdlestan.Core.Presentation.ViewModels;

namespace HeroesOfIdlestan.Core.UI.Views;

/// <summary>
/// Barre du haut, construite en code (pas de XAML) et liée à <see cref="HudViewModel"/>. Le bouton
/// « Log » clignote tant qu'un message du journal n'a pas été vu, et déplie le journal sous la barre.
/// </summary>
public sealed class HudView : Border
{
    private static readonly IBrush LogHighlight = new SolidColorBrush(Color.FromRgb(0xD9, 0x8C, 0x1F));

    public HudView(HudViewModel viewModel)
    {
        DataContext = viewModel;
        Background = new SolidColorBrush(Color.FromArgb(0xD0, 0x10, 0x14, 0x1A));
        Padding = new Thickness(12, 6);
        VerticalAlignment = VerticalAlignment.Top;

        var gold = new TextBlock { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        gold.Bind(TextBlock.TextProperty, new Binding(nameof(HudViewModel.GoldLabel)));

        var zone = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        zone.Bind(TextBlock.TextProperty, new Binding(nameof(HudViewModel.ZoneLabel)));

        var hero = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        hero.Bind(TextBlock.TextProperty, new Binding(nameof(HudViewModel.HeroLabel)));

        var bank = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray };
        bank.Bind(TextBlock.TextProperty, new Binding(nameof(HudViewModel.BankLabel)));

        var autoAdvance = new Button();
        autoAdvance.Bind(ContentControl.ContentProperty, new Binding(nameof(HudViewModel.AutoAdvanceLabel)));
        autoAdvance.Click += (_, _) => viewModel.ToggleAutoAdvance();

        var pause = new Button();
        pause.Bind(ContentControl.ContentProperty, new Binding(nameof(HudViewModel.PauseLabel)));
        pause.Click += (_, _) => viewModel.TogglePause();

        var speeds = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        foreach (var speed in new[] { 1, 3, 10 })
        {
            var b = new Button { Content = $"x{speed}" };
            b.Click += (_, _) => viewModel.SetSpeed(speed);
            speeds.Children.Add(b);
        }

        var save = new Button();
        save.Bind(ContentControl.ContentProperty, new Binding(nameof(HudViewModel.SaveLabel)));
        save.Click += (_, _) => viewModel.Save();

        var sound = new Button();
        sound.Bind(ContentControl.ContentProperty, new Binding(nameof(HudViewModel.SoundLabel)));
        sound.Bind(IsVisibleProperty, new Binding(nameof(HudViewModel.SoundAvailable)));
        sound.Click += (_, _) => viewModel.ToggleSound();

        var menu = new Button();
        menu.Bind(ContentControl.ContentProperty, new Binding(nameof(HudViewModel.MenuLabel)));
        menu.Click += (_, _) => viewModel.ReturnToMenu();

        var log = new Button();
        log.Bind(ContentControl.ContentProperty, new Binding(nameof(HudViewModel.LogLabel)));
        log.Click += (_, _) => viewModel.ToggleLog();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(HudViewModel.LogHighlighted)) return;
            if (viewModel.LogHighlighted) log.Background = LogHighlight;
            else log.ClearValue(BackgroundProperty);
        };

        var logText = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        logText.Bind(TextBlock.TextProperty, new Binding(nameof(HudViewModel.LogText)));
        var logPanel = new Border
        {
            Margin = new Thickness(0, 6, 0, 0),
            Padding = new Thickness(8),
            Background = new SolidColorBrush(Color.FromArgb(0xE0, 0x08, 0x0A, 0x0D)),
            Child = new ScrollViewer { MaxHeight = 240, Content = logText },
        };
        logPanel.Bind(IsVisibleProperty, new Binding(nameof(HudViewModel.LogOpen)));

        Child = new StackPanel
        {
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 16,
                    Children = { gold, zone, hero, autoAdvance, pause, speeds, bank, save, sound, log, menu },
                },
                logPanel,
            },
        };
    }
}
