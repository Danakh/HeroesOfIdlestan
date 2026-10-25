using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using HeroesOfIdlestan.Core.UI.ViewModels;

namespace HeroesOfIdlestan.Core.UI.Views;

/// <summary>Barre du haut, construite en code (pas de XAML) et liée à <see cref="HudViewModel"/>.</summary>
public sealed class HudView : Border
{
    public HudView(HudViewModel viewModel)
    {
        DataContext = viewModel;
        Background = new SolidColorBrush(Color.FromArgb(0xD0, 0x10, 0x14, 0x1A));
        Padding = new Thickness(12, 6);
        VerticalAlignment = VerticalAlignment.Top;

        var gold = new TextBlock { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        gold.Bind(TextBlock.TextProperty, new Binding(nameof(HudViewModel.GoldLabel)));

        var producers = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        producers.Bind(TextBlock.TextProperty, new Binding(nameof(HudViewModel.ProducersLabel)));

        var bank = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray };
        bank.Bind(TextBlock.TextProperty, new Binding(nameof(HudViewModel.BankLabel)));

        var buy = new Button();
        buy.Bind(ContentControl.ContentProperty, new Binding(nameof(HudViewModel.BuyLabel)));
        buy.Bind(IsEnabledProperty, new Binding(nameof(HudViewModel.CanBuy)));
        buy.Click += (_, _) => viewModel.BuyProducer();

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

        Child = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Children = { gold, producers, buy, pause, speeds, bank, save, sound },
        };
    }
}
