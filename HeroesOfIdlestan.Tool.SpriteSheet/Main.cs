using Godot;

namespace HeroesOfIdlestan.Tool.SpriteSheet;

/// <summary>
/// Seul script Godot de l'outil (scène principale <c>Main.tscn</c>) : lit les arguments, lance la
/// cuisson en montrant la vue filmée, puis quitte avec un code de sortie (0 si tout s'est bien passé).
/// </summary>
public partial class Main : Node
{
    private Label _status = null!;

    public override async void _Ready()
    {
        _status = new Label { Position = new Vector2(8, 4) };
        int code;
        try
        {
            code = await Run();
        }
        catch (Exception e)
        {
            GD.PrintErr(e.Message);
            code = 1;
        }
        GetTree().Quit(code);
    }

    private async Task<int> Run()
    {
        // Le projet est dans <dépôt>/HeroesOfIdlestan.Tool.SpriteSheet/.
        string repository = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), ".."));
        BakeOptions options;
        try
        {
            options = BakeOptions.Parse(OS.GetCmdlineUserArgs(), repository, System.Environment.CurrentDirectory);
        }
        catch (ArgumentException e)
        {
            GD.PrintErr(e.Message);
            GD.Print(BakeOptions.Usage);
            return 2;
        }
        if (options.Help)
        {
            GD.Print(BakeOptions.Usage);
            return 0;
        }

        GD.Print($"Modèle : {options.ModelPath}");
        var baker = new SpriteBaker(this, options, text => _status.Text = text);
        baker.Setup();

        AddChild(new TextureRect
        {
            Texture = baker.Preview,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            AnchorRight = 1,
            AnchorBottom = 1,
        });
        AddChild(_status);

        return await baker.RunAsync();
    }
}
