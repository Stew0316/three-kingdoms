using Godot;
using System;

/// <summary>演武场流程入口：绑定对手、累计对战时间、统一结算，并提供测试/截图入口。</summary>
public partial class Arena : Node2D
{
    // 本场玩家角色，在场景 Ready 完成后可用。
    public Combatant Player { get; private set; }
    // 本场敌方角色，作为玩家对手并由 AI 驱动。
    public Combatant Enemy { get; private set; }
    // 是否已经结算，用于防止死亡信号导致重复结算。
    public bool Finished { get; private set; }
    // 显示在结算面板的中文结果，未结束时为空。
    public string Result { get; private set; } = "";
    // 本场对战累计秒数；暂停及结算后停止增长。
    public double BattleSeconds { get; private set; }
    // 进程级测试启动守卫，防止测试中的反复重开递归创建新测试器。
    private static bool _testsStarted;
    public CombatResolver Resolver { get; private set; }
    [Export] public bool Hd2D { get; set; } // 独立三维演武场启用，旧二维场景保留对照。

    /// <summary>绑定角色与死亡信号；仅在显式命令行参数存在时启动测试或截图。</summary>
    public override void _Ready()
    {
        Player = GetNode<Combatant>(SceneNodePaths.Player);
        Enemy = GetNode<Combatant>(SceneNodePaths.Enemy);
        Enemy.GetNode<WeiYanController>(NodeNames.Controller).Target = Player;
        Resolver = new CombatResolver();
        foreach (var unit in new[] { Player, Enemy }) { Resolver.Register(unit); unit.Resolver = Resolver; }
        Resolver.Settled += () => { if (Player.IsDead || Enemy.IsDead) EndBattle(!Player.IsDead); };
        if (Hd2D) AddChild(new Hd2DStage { Name = "HD2DStage" });
        if (!_testsStarted && Array.Exists(OS.GetCmdlineUserArgs(), a => a == DevelopmentArguments.CombatTest))
        {
            _testsStarted = true;
            CallDeferred(MethodName.StartTests);
        }
        if (Array.Exists(OS.GetCmdlineUserArgs(), a => a == DevelopmentArguments.CaptureArena))
            CallDeferred(MethodName.CapturePreview);
    }

    /// <summary>按 delta 秒累计对战时间，场景暂停时由引擎停止调用。</summary>
    public override void _Process(double delta)
    {
        if (!Finished) BattleSeconds += delta;
    }

    /// <summary>结算整场；won 为 true 表示玩家胜利，双方立即停止行动。</summary>
    private void EndBattle(bool won)
    {
        if (Finished) return;
        Finished = true;
        Result = won ? BattleTexts.Victory : BattleTexts.Defeat;
        Player.FinishBattle();
        Enemy.FinishBattle();
    }

    /// <summary>把测试器挂到场景树根部，使它能跨越 Arena 的重载持续执行。</summary>
    private void StartTests() => GetTree().Root.AddChild(new CombatSmokeTests());

    /// <summary>关闭控制器并摆放固定画面，等待渲染完成后保存截图并退出测试进程。</summary>
    private async void CapturePreview()
    {
        Player.GetNode(NodeNames.Controller).ProcessMode = ProcessModeEnum.Disabled;
        Enemy.GetNode(NodeNames.Controller).ProcessMode = ProcessModeEnum.Disabled;
        // 三个标记分别选择前摇预警、生效刀光、骨骼叠加图；默认只截待机画面。
        bool attackPreview = Array.Exists(OS.GetCmdlineUserArgs(), a => a == DevelopmentArguments.CaptureAttack);
        bool slashPreview = Array.Exists(OS.GetCmdlineUserArgs(), a => a == DevelopmentArguments.CaptureSlash);
        bool bonePreview = Array.Exists(OS.GetCmdlineUserArgs(), a => a == DevelopmentArguments.CaptureBones);
        bool motionPreview = Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--capture-motion");
        bool counterPreview = Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--capture-counter");
        if (bonePreview)
        {
            Player.Rig.ShowBones = true;
            Enemy.Rig.ShowBones = true;
        }
        if (attackPreview || slashPreview)
        {
            Player.Position = new Vector2(279, 252);
            Enemy.Position = new Vector2(354, 249);
            Enemy.TryAction(CombatAction.Sweep);
            for (int i = 0; i < (slashPreview ? 44 : 18); i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        if(motionPreview)
        {
            Player.SetMoveInput(Vector2.Right);
            for(int i=0;i<24;i++) await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
        }
        if(counterPreview)
        {
            Player.Position=new(300,252); Enemy.Position=new(350,252);
            Resolver.RollOverride=()=>0;
            Enemy.ReceiveDamage(new DamageInfo(Player,1,Vector2.Right,DamageKind.Attack));
            for(int i=0;i<9;i++) await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
        }
        for(int i=0;i<3;i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string path = attackPreview ? PreviewPaths.Attack : PreviewPaths.Arena;
        if (slashPreview) path = PreviewPaths.Slash;
        if (bonePreview) path = PreviewPaths.Bones;
        if(Hd2D) path="res://Build/HD2D-"+(motionPreview ? "motion" : counterPreview ? "counter" : slashPreview ? "slash" : bonePreview ? "bones" : "arena")+"-preview.png";
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"场景预览已保存：{path}");
        GetTree().Quit();
    }

    /// <summary>绘制静态场地装饰；这里的线条和石板不充当物理碰撞体。</summary>
    public override void _Draw()
    {
        if (Hd2D) return;
        DrawRect(new Rect2(0, 0, 640, 360), new Color(ArenaVisualConstants.Background));
        DrawRect(new Rect2(16, 83, 608, 241), new Color(ArenaVisualConstants.OuterFloor));
        DrawRect(new Rect2(24, 101, 592, 211), new Color(ArenaVisualConstants.InnerFloor));
        // 石板低对比处理，交战范围保持干净。装饰与物理边界分别管理。
        var random = new RandomNumberGenerator { Seed = ArenaVisualConstants.StonePatternSeed };
        for (int row = 0; row < 8; row++)
        for (int col = 0; col < 16; col++)
        {
            float x = 25 + col * 40 - (row % 2) * 20;
            float y = 102 + row * 27;
            if (x < 25 || x + 38 > 615 || y + 25 > 311) continue;
            float value = random.RandfRange(-.025f, .025f);
            DrawRect(new Rect2(x, y, 38, 25), new Color(.49f + value, .45f + value, .35f + value));
        }
        DrawArc(new Vector2(320, 214), 78, 0, Mathf.Tau, 96, ArenaVisualConstants.RingPrimary, 1, true);
        DrawArc(new Vector2(320, 214), 83, 0, Mathf.Tau, 96, ArenaVisualConstants.RingSecondary, 1, true);
        DrawLine(new Vector2(310, 214), new Vector2(330, 214), ArenaVisualConstants.RingCross);
        DrawLine(new Vector2(320, 204), new Vector2(320, 224), ArenaVisualConstants.RingCross);
        DrawRect(new Rect2(16, 83, 608, 15), new Color(ArenaVisualConstants.Border));
        DrawLine(new Vector2(16, 83), new Vector2(624, 83), new Color(ArenaVisualConstants.BorderHighlight), 2);
        DrawRect(new Rect2(16, 98, 8, 226), new Color(ArenaVisualConstants.SideWall));
        DrawRect(new Rect2(616, 98, 8, 226), new Color(ArenaVisualConstants.SideWall));
        DrawRect(new Rect2(16, 312, 608, 12), new Color(ArenaVisualConstants.Border));
        foreach (int x in new[] { 36, 604 })
        {
            DrawLine(new Vector2(x, 48), new Vector2(x, 95), new Color(ArenaVisualConstants.BannerPole), 2);
            DrawColoredPolygon(new[] { new Vector2(x + 2, 48), new Vector2(x + 20, 48),
                new Vector2(x + 20, 72), new Vector2(x + 11, 67), new Vector2(x + 2, 72) }, new Color(ArenaVisualConstants.Banner));
        }
    }
}
