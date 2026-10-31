using Godot;
using HeroesOfIdlestan.Core.Presentation.ViewModels;

namespace HeroesOfIdlestan.Core.UI.Godot.Views;

/// <summary>
/// Menu principal. La mise en page vit dans <see cref="ScenePath"/>, éditée dans Godot ; cette classe
/// la relie à <see cref="MenuViewModel"/> (contrat : <see cref="SceneView"/>). Le voile couvre tout
/// l'écran et prend les clics destinés à la caméra tant que le menu est affiché.
/// </summary>
public sealed class MenuView : SceneView
{
    public const string ScenePath = "res://ui/menu.tscn";

    public MenuView(MenuViewModel viewModel) : base(ScenePath, viewModel)
    {
        BindText(Node<Label>("Title"), nameof(MenuViewModel.Title), () => viewModel.Title);

        var resume = Node<Button>("Continue");
        BindButton(resume, nameof(MenuViewModel.ContinueLabel), () => viewModel.ContinueLabel, viewModel.Continue);
        Bind(nameof(MenuViewModel.CanContinue), () => resume.Visible = viewModel.CanContinue);

        BindButton(Node<Button>("NewGame"), nameof(MenuViewModel.NewGameLabel), () => viewModel.NewGameLabel, viewModel.NewGame);
        BindButton(Node<Button>("DebugGame"), nameof(MenuViewModel.DebugGameLabel), () => viewModel.DebugGameLabel, viewModel.DebugGame);

        Bind(nameof(MenuViewModel.Visible), () => Root.Visible = viewModel.Visible);
    }
}
