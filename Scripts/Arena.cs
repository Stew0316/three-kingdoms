using Godot;
using System;

public partial class Arena : Node2D
{
    public Combatant Player { get; private set; }
    public Combatant Enemy { get; private set; }
    public bool Finished { get; private set; }
    public string Result { get; private set; } = "";
    public double BattleSeconds { get; private set; }
    private static bool _testsStarted;

    public override void _Ready()
    {
        Player = GetNode<Combatant>("Fighters/LvBu");
        Enemy = GetNode<Combatant>("Fighters/WeiYan");
        Enemy.GetNode<WeiYanController>("Controller").Target = Player;
        Player.Health.Died += () => EndBattle(false);
        Enemy.Health.Died += () => EndBattle(true);
        if (!_testsStarted && Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--combat-test"))
        {
            _testsStarted = true;
            CallDeferred(MethodName.StartTests);
        }
        if (Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--capture-arena"))
            CallDeferred(MethodName.CapturePreview);
    }

    public override void _Process(double delta)
    {
        if (!Finished) BattleSeconds += delta;
    }

    private void EndBattle(bool won)
    {
        if (Finished) return;
        Finished = true;
        Result = won ? "挑战成功" : "胜败乃兵家常事";
        Player.FinishBattle();
        Enemy.FinishBattle();
    }

    private void StartTests() => GetTree().Root.AddChild(new CombatSmokeTests());

    private async void CapturePreview()
    {
        Player.GetNode("Controller").ProcessMode = ProcessModeEnum.Disabled;
        Enemy.GetNode("Controller").ProcessMode = ProcessModeEnum.Disabled;
        bool attackPreview = Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--capture-attack");
        if (attackPreview)
        {
            Player.Position = new Vector2(279, 252);
            Enemy.Position = new Vector2(354, 249);
            Enemy.TryAction(CombatAction.Sweep);
            for (int i = 0; i < 18; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string path = attackPreview ? "res://Build/S1-attack-preview.png" : "res://Build/S1-arena-preview.png";
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"Arena preview saved: {path}");
        GetTree().Quit();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, 640, 360), new Color("172023"));
        DrawRect(new Rect2(16, 83, 608, 241), new Color("343a35"));
        DrawRect(new Rect2(24, 101, 592, 211), new Color("786d53"));
        // 石板低对比处理，交战范围保持干净。装饰与物理边界分别管理。
        var random = new RandomNumberGenerator { Seed = 20260928 };
        for (int row = 0; row < 8; row++)
        for (int col = 0; col < 16; col++)
        {
            float x = 25 + col * 40 - (row % 2) * 20;
            float y = 102 + row * 27;
            if (x < 25 || x + 38 > 615 || y + 25 > 311) continue;
            float value = random.RandfRange(-.025f, .025f);
            DrawRect(new Rect2(x, y, 38, 25), new Color(.49f + value, .45f + value, .35f + value));
        }
        DrawArc(new Vector2(320, 214), 78, 0, Mathf.Tau, 96, new Color(.78f, .70f, .48f, .23f), 1, true);
        DrawArc(new Vector2(320, 214), 83, 0, Mathf.Tau, 96, new Color(.78f, .70f, .48f, .18f), 1, true);
        DrawLine(new Vector2(310, 214), new Vector2(330, 214), new Color(.8f, .72f, .5f, .25f));
        DrawLine(new Vector2(320, 204), new Vector2(320, 224), new Color(.8f, .72f, .5f, .25f));
        DrawRect(new Rect2(16, 83, 608, 15), new Color("464c46"));
        DrawLine(new Vector2(16, 83), new Vector2(624, 83), new Color("9a8c65"), 2);
        DrawRect(new Rect2(16, 98, 8, 226), new Color("4b5149"));
        DrawRect(new Rect2(616, 98, 8, 226), new Color("4b5149"));
        DrawRect(new Rect2(16, 312, 608, 12), new Color("464c46"));
        foreach (int x in new[] { 36, 604 })
        {
            DrawLine(new Vector2(x, 48), new Vector2(x, 95), new Color("aa9666"), 2);
            DrawColoredPolygon(new[] { new Vector2(x + 2, 48), new Vector2(x + 20, 48),
                new Vector2(x + 20, 72), new Vector2(x + 11, 67), new Vector2(x + 2, 72) }, new Color("713f35"));
        }
    }
}
