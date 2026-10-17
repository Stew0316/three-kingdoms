using Godot;
using System.Linq;

/// <summary>默认进入首城；原有专项参数和 --arena 仍可直接进入演武场。</summary>
public partial class GameEntry : Node
{
    public override void _Ready() => CallDeferred(MethodName.Start);
    private void Start()
    {
        if(OS.GetCmdlineUserArgs().Any(a=>a is "--cave" or "--cave-test"))
        { GetTree().ChangeSceneToFile(WorldScenePaths.OldRoadCave); return; }
        bool arena = OS.GetCmdlineUserArgs().Any(a => a == "--arena" || ((a.Contains("test") || a.StartsWith("--capture")) && a != "--city-test" && a != "--horse-test"));
        GetTree().ChangeSceneToFile(arena ? WorldScenePaths.Arena : WorldScenePaths.FirstCity);
    }
}

