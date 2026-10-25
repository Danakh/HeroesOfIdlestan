using System.ComponentModel;
using Godot;
using HeroesOfIdlestan.Core.UI.Godot.ViewModels;

namespace HeroesOfIdlestan.Core.UI.Godot.Views;

/// <summary>
/// Barre du haut, construite en code et liée à <see cref="HudViewModel"/> : Godot n'a pas de binding,
/// chaque propriété notifiée recopie sa valeur dans son contrôle.
/// </summary>
public sealed class HudView
{
    private readonly HudViewModel _viewModel;
    private readonly Dictionary<string, Action> _bindings = [];

    public HudView(HudViewModel viewModel)
    {
        _viewModel = viewModel;

        Root = new PanelContainer { Name = "Hud" };
        Root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        Root.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0x101418D0),
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 6, ContentMarginBottom = 6,
        });

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        Root.AddChild(row);

        row.AddChild(BoundLabel(nameof(HudViewModel.GoldLabel), () => viewModel.GoldLabel));
        row.AddChild(BoundLabel(nameof(HudViewModel.ZoneLabel), () => viewModel.ZoneLabel));
        row.AddChild(BoundLabel(nameof(HudViewModel.HeroLabel), () => viewModel.HeroLabel));
        row.AddChild(BoundButton(nameof(HudViewModel.AutoAdvanceLabel), () => viewModel.AutoAdvanceLabel, viewModel.ToggleAutoAdvance));
        row.AddChild(BoundButton(nameof(HudViewModel.PauseLabel), () => viewModel.PauseLabel, viewModel.TogglePause));

        var speeds = new HBoxContainer();
        speeds.AddThemeConstantOverride("separation", 2);
        foreach (var speed in new[] { 1, 3, 10 })
        {
            var b = new Button { Text = $"x{speed}" };
            b.Pressed += () => viewModel.SetSpeed(speed);
            speeds.AddChild(b);
        }
        row.AddChild(speeds);

        var bank = BoundLabel(nameof(HudViewModel.BankLabel), () => viewModel.BankLabel);
        bank.Modulate = Colors.Gray;
        row.AddChild(bank);
        row.AddChild(BoundButton(nameof(HudViewModel.SaveLabel), () => viewModel.SaveLabel, viewModel.Save));

        var sound = BoundButton(nameof(HudViewModel.SoundLabel), () => viewModel.SoundLabel, viewModel.ToggleSound);
        Bind(nameof(HudViewModel.SoundAvailable), () => sound.Visible = viewModel.SoundAvailable);
        row.AddChild(sound);

        viewModel.PropertyChanged += OnPropertyChanged;
    }

    public PanelContainer Root { get; }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != null && _bindings.TryGetValue(e.PropertyName, out var apply)) apply();
    }

    private void Bind(string property, Action apply)
    {
        _bindings[property] = apply;
        apply();
    }

    private Label BoundLabel(string property, Func<string> text)
    {
        var label = new Label { VerticalAlignment = VerticalAlignment.Center };
        Bind(property, () => label.Text = text());
        return label;
    }

    private Button BoundButton(string property, Func<string> text, Action pressed)
    {
        var button = new Button();
        button.Pressed += pressed;
        Bind(property, () => button.Text = text());
        return button;
    }

    public void Detach() => _viewModel.PropertyChanged -= OnPropertyChanged;
}
