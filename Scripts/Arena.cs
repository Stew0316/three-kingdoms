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
    private static bool _walkTestsStarted; // 步态回归会重开场景，避免再次创建测试器。
    public CombatResolver Resolver { get; private set; }
    [Export] public bool Hd2D { get; set; } // 仅供历史技术样板启用；正式入口使用二维俯视演武场。
    [Export] public bool SoloPractice { get; set; } // 单人步态练习：保留魏延节点，但关闭显示、处理与碰撞，方便恢复对战。
    public bool IsSoloPractice => SoloPractice && !Array.Exists(OS.GetCmdlineUserArgs(),
        a => a == DevelopmentArguments.CombatTest || a == "--rig-visual-test" || a == "--with-opponent");

    /// <summary>绑定角色与死亡信号；仅在显式命令行参数存在时启动测试或截图。</summary>
    public override void _Ready()
    {
        Player = GetNode<Combatant>(SceneNodePaths.Player);
        Enemy = GetNode<Combatant>(SceneNodePaths.Enemy);
        Enemy.GetNode<WeiYanController>(NodeNames.Controller).Target = Player;
        Resolver = new CombatResolver();
        Resolver.Register(Player); Player.Resolver = Resolver;
        if (IsSoloPractice)
        {
            Enemy.Hide();
            Enemy.ProcessMode = ProcessModeEnum.Disabled;
            Enemy.CollisionLayer = 0; Enemy.CollisionMask = 0;
            foreach (var name in new[] { NodeNames.Hurtbox, NodeNames.AttackHitbox })
            {
                var area = Enemy.GetNode<Area2D>(name);
                area.CollisionLayer = 0; area.CollisionMask = 0;
                area.Monitoring = false; area.Monitorable = false;
            }
        }
        else
        {
            Resolver.Register(Enemy); Enemy.Resolver = Resolver;
            Resolver.Settled += () => { if (Player.IsDead || Enemy.IsDead) EndBattle(!Player.IsDead); };
        }
        if (Hd2D) AddChild(new Hd2DStage { Name = "HD2DStage" });
        if (Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--rig-visual-test"))
            CallDeferred(MethodName.StartRigVisualTests);
        if (!_walkTestsStarted && Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--walk-test"))
        {
            _walkTestsStarted = true;
            CallDeferred(MethodName.StartWalkTests);
        }
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

    /// <summary>使用真实渲染帧验收手脚可见性，只有显式开发参数会启动。</summary>
    private void StartRigVisualTests() => GetTree().Root.AddChild(new RigVisualTests());
    private void StartWalkTests() => GetTree().Root.AddChild(new WalkVisualTests());

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

    /// <summary>绘制宝可梦式二维俯视演武场；地图装饰和物理边界仍分别管理。</summary>
    public override void _Draw()
    {
        if (Hd2D) return;
        DrawRect(new Rect2(0,0,640,360),new Color(ArenaVisualConstants.GrassDark));
        // 16 像素网格草地使用固定随机明暗，强调 GBA 时代地图块感但不要求逐格移动。
        var random = new RandomNumberGenerator { Seed = ArenaVisualConstants.StonePatternSeed };
        for(int row=4;row<22;row++) for(int col=0;col<40;col++)
        {
            float value=random.RandfRange(-.025f,.025f);
            DrawRect(new Rect2(col*16,row*16,16,16),new Color(.47f+value,.68f+value,.34f+value));
            if((row*7+col*13)%23==0) DrawLine(new Vector2(col*16+5,row*16+13),new Vector2(col*16+7,row*16+9),new Color(ArenaVisualConstants.GrassBlade),1);
        }

        // 围墙、院门和草木从上往下分层，角色由 YSort 在院内穿行。
        DrawRect(new Rect2(32,84,576,244),new Color(ArenaVisualConstants.WallShadow));
        DrawRect(new Rect2(40,96,560,224),new Color(ArenaVisualConstants.CourtyardEdge));
        DrawRect(new Rect2(56,108,528,196),new Color(ArenaVisualConstants.Courtyard));
        for(int row=0;row<8;row++) for(int col=0;col<22;col++)
        {
            float x=57+col*24-(row%2)*12,y=109+row*24;
            if(x<57 || x+22>583) continue;
            float value=random.RandfRange(-.035f,.035f);
            DrawRect(new Rect2(x,y,22,22),new Color(.76f+value,.69f+value,.48f+value));
            DrawLine(new Vector2(x,y+22),new Vector2(x+22,y+22),new Color(ArenaVisualConstants.TileJoint),1);
            DrawLine(new Vector2(x+22,y),new Vector2(x+22,y+22),new Color(ArenaVisualConstants.TileJoint),1);
        }
        DrawArc(new Vector2(320,208),70,0,Mathf.Tau,64,ArenaVisualConstants.RingPrimary,3,false);
        DrawArc(new Vector2(320,208),76,0,Mathf.Tau,64,ArenaVisualConstants.RingSecondary,2,false);
        DrawLine(new Vector2(300,208),new Vector2(340,208),ArenaVisualConstants.RingCross,2);
        DrawLine(new Vector2(320,188),new Vector2(320,228),ArenaVisualConstants.RingCross,2);

        // 北侧训练馆屋檐和入口用纯色块表达，不引入 3D 摄像机与模型管线。
        DrawRect(new Rect2(176,64,288,28),new Color(ArenaVisualConstants.RoofDark));
        DrawColoredPolygon(new[]{new Vector2(160,82),new Vector2(480,82),new Vector2(456,59),new Vector2(184,59)},new Color(ArenaVisualConstants.Roof));
        DrawLine(new Vector2(160,82),new Vector2(480,82),new Color(ArenaVisualConstants.RoofHighlight),4);
        DrawRect(new Rect2(277,72,86,28),new Color(ArenaVisualConstants.GateDark));
        DrawRect(new Rect2(289,77,62,23),new Color(ArenaVisualConstants.Gate));
        for(int x=52;x<=588;x+=32) DrawBush(new Vector2(x,91),(x/32)%2==0);
        foreach(int x in new[]{48,592})
        {
            DrawLine(new Vector2(x,70),new Vector2(x,120),new Color(ArenaVisualConstants.BannerPole),3);
            DrawColoredPolygon(new[]{new Vector2(x+3,70),new Vector2(x+23,70),new Vector2(x+23,93),new Vector2(x+13,88),new Vector2(x+3,93)},new Color(x<320 ? ArenaVisualConstants.PlayerBanner : ArenaVisualConstants.EnemyBanner));
        }
    }

    /// <summary>以少量色块绘制可重复灌木，保持地图块轮廓清晰。</summary>
    private void DrawBush(Vector2 center,bool light)
    {
        Color dark=new(ArenaVisualConstants.BushDark),mid=new(ArenaVisualConstants.Bush),shine=new(ArenaVisualConstants.BushHighlight);
        DrawCircle(center+new Vector2(1,3),10,dark);
        DrawCircle(center+new Vector2(-5,0),7,mid);
        DrawCircle(center+new Vector2(5,-1),8,mid);
        DrawRect(new Rect2(center.X-8,center.Y+5,16,5),dark);
        if(light) DrawCircle(center+new Vector2(-3,-3),2,shine);
    }
}
