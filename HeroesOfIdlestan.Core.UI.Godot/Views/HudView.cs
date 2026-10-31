using System.ComponentModel;
using Godot;
using HeroesOfIdlestan.Core.Presentation.ViewModels;

namespace HeroesOfIdlestan.Core.UI.Godot.Views;

/// <summary>
/// Barre du haut. La mise en page vit dans une scène éditée dans Godot (<see cref="ScenePath"/>,
/// dans le projet App.Godot) ; cette classe l'instancie et la relie à <see cref="HudViewModel"/>.
/// Godot n'a pas de binding : chaque propriété notifiée recopie sa valeur dans son contrôle.
///
/// <para><b>Contrat avec la scène</b> : les nœuds liés portent un nom unique (<c>%Gold</c>…, case
/// « Access as Unique Name » de l'éditeur) ; leur place dans l'arbre est libre. Un nœud manquant ou
/// du mauvais type lève à la construction, au démarrage du jeu, plutôt que de laisser un HUD muet.
/// La scène ne porte aucun script : un script Godot doit vivre dans l'assembly du head.</para>
/// </summary>
public sealed class HudView
{
    /// <summary>La scène du HUD, dans le projet Godot : seul un chemin <c>res://</c> passe par l'import.</summary>
    public const string ScenePath = "res://ui/hud.tscn";

    private readonly HudViewModel _viewModel;
    private readonly Dictionary<string, Action> _bindings = [];

    public HudView(HudViewModel viewModel)
    {
        _viewModel = viewModel;

        var scene = GD.Load<PackedScene>(ScenePath)
            ?? throw new InvalidOperationException($"Scène du HUD introuvable : {ScenePath}");
        Root = scene.Instantiate<Control>();

        BindText(Node<Label>("Gold"), nameof(HudViewModel.GoldLabel), () => viewModel.GoldLabel);
        BindText(Node<Label>("Zone"), nameof(HudViewModel.ZoneLabel), () => viewModel.ZoneLabel);
        BindText(Node<Label>("Hero"), nameof(HudViewModel.HeroLabel), () => viewModel.HeroLabel);
        BindText(Node<Label>("Bank"), nameof(HudViewModel.BankLabel), () => viewModel.BankLabel);

        BindButton(Node<Button>("AutoAdvance"), nameof(HudViewModel.AutoAdvanceLabel), () => viewModel.AutoAdvanceLabel, viewModel.ToggleAutoAdvance);
        BindButton(Node<Button>("Pause"), nameof(HudViewModel.PauseLabel), () => viewModel.PauseLabel, viewModel.TogglePause);
        BindButton(Node<Button>("Save"), nameof(HudViewModel.SaveLabel), () => viewModel.SaveLabel, viewModel.Save);

        var sound = Node<Button>("Sound");
        BindButton(sound, nameof(HudViewModel.SoundLabel), () => viewModel.SoundLabel, viewModel.ToggleSound);
        Bind(nameof(HudViewModel.SoundAvailable), () => sound.Visible = viewModel.SoundAvailable);

        foreach (var speed in new[] { 1, 3, 10 })
            Node<Button>($"Speed{speed}").Pressed += () => viewModel.SetSpeed(speed);

        viewModel.PropertyChanged += OnPropertyChanged;
    }

    public Control Root { get; }

    private T Node<T>(string uniqueName) where T : Node =>
        Root.GetNodeOrNull<T>($"%{uniqueName}")
        ?? throw new InvalidOperationException(
            $"{ScenePath} : nœud %{uniqueName} ({typeof(T).Name}) absent, ou sans « Access as Unique Name ».");

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != null && _bindings.TryGetValue(e.PropertyName, out var apply)) apply();
    }

    private void Bind(string property, Action apply)
    {
        _bindings[property] = apply;
        apply();
    }

    private void BindText(Label label, string property, Func<string> text) =>
        Bind(property, () => label.Text = text());

    private void BindButton(Button button, string property, Func<string> text, Action pressed)
    {
        button.Pressed += pressed;
        Bind(property, () => button.Text = text());
    }

    public void Detach() => _viewModel.PropertyChanged -= OnPropertyChanged;
}
