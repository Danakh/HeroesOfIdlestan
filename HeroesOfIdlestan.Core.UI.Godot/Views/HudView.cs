using Godot;
using HeroesOfIdlestan.Core.Presentation.ViewModels;

namespace HeroesOfIdlestan.Core.UI.Godot.Views;

/// <summary>
/// Barre du haut. La mise en page vit dans une scène éditée dans Godot (<see cref="ScenePath"/>) ;
/// cette classe l'instancie et la relie à <see cref="HudViewModel"/> (contrat : <see cref="SceneView"/>).
/// Le bouton « Log » clignote tant qu'un message du journal n'a pas été vu, et déplie le journal
/// (<c>%LogPanel</c>) sous la barre.
/// </summary>
public sealed class HudView : SceneView
{
    private static readonly Color LogHighlight = new(1f, 0.6f, 0.15f);

    /// <summary>La scène du HUD, dans le projet Godot : seul un chemin <c>res://</c> passe par l'import.</summary>
    public const string ScenePath = "res://ui/hud.tscn";

    public HudView(HudViewModel viewModel) : base(ScenePath, viewModel)
    {
        BindText(Node<Label>("Gold"), nameof(HudViewModel.GoldLabel), () => viewModel.GoldLabel);
        BindText(Node<Label>("Zone"), nameof(HudViewModel.ZoneLabel), () => viewModel.ZoneLabel);
        BindText(Node<Label>("Hero"), nameof(HudViewModel.HeroLabel), () => viewModel.HeroLabel);
        BindText(Node<Label>("Bank"), nameof(HudViewModel.BankLabel), () => viewModel.BankLabel);

        BindButton(Node<Button>("AutoAdvance"), nameof(HudViewModel.AutoAdvanceLabel), () => viewModel.AutoAdvanceLabel, viewModel.ToggleAutoAdvance);
        BindButton(Node<Button>("Pause"), nameof(HudViewModel.PauseLabel), () => viewModel.PauseLabel, viewModel.TogglePause);
        BindButton(Node<Button>("Save"), nameof(HudViewModel.SaveLabel), () => viewModel.SaveLabel, viewModel.Save);
        BindButton(Node<Button>("Menu"), nameof(HudViewModel.MenuLabel), () => viewModel.MenuLabel, viewModel.ReturnToMenu);

        var sound = Node<Button>("Sound");
        BindButton(sound, nameof(HudViewModel.SoundLabel), () => viewModel.SoundLabel, viewModel.ToggleSound);
        Bind(nameof(HudViewModel.SoundAvailable), () => sound.Visible = viewModel.SoundAvailable);

        var log = Node<Button>("Log");
        BindButton(log, nameof(HudViewModel.LogLabel), () => viewModel.LogLabel, viewModel.ToggleLog);
        Bind(nameof(HudViewModel.LogHighlighted), () => log.SelfModulate = viewModel.LogHighlighted ? LogHighlight : Colors.White);
        var logPanel = Node<PanelContainer>("LogPanel");
        Bind(nameof(HudViewModel.LogOpen), () => logPanel.Visible = viewModel.LogOpen);
        BindText(Node<Label>("LogText"), nameof(HudViewModel.LogText), () => viewModel.LogText);

        foreach (var speed in new[] { 1, 3, 10 })
            Node<Button>($"Speed{speed}").Pressed += () => viewModel.SetSpeed(speed);
    }
}
