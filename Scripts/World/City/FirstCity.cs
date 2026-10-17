using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>首城场景装配与交互入口。进度规则属于 AdventureProgress，对话只提供选择。</summary>
public partial class FirstCity : Node2D
{
    public Combatant Player { get; private set; }
    public CityMap Map { get; private set; }
    public CityDialogue Dialogue { get; private set; }
    public List<CityInteractable> Interactions { get; } = new();
    public int MountedHorse { get; private set; } // 0徒步、1枣红、2青骢；切入演武场自动下马。
    public float TrainingRemaining { get; private set; }
    public CityInteractable Nearest { get; private set; }
    public Camera2D Camera { get; private set; }
    private AdventureSession Session => AdventureSession.Instance;
    private AdventureProgress Progress => Session.Progress;
    private FirstCityConfig Config => Session.Config;
    private Label _quest, _stats, _hint, _notice;
    private CityHorse _ridingHorse;
    private readonly HorseAppearance[] _horseAppearances = {
        GD.Load<HorseAppearance>("res://Resources/Config/World/horse-chestnut.tres"),
        GD.Load<HorseAppearance>("res://Resources/Config/World/horse-dapple.tres") };
    private readonly CityHorse[] _stableHorses = new CityHorse[2];
    private float _smashRemaining, _toastRemaining, _trainingBeat;
    private bool _training, _switching;
    private CityOverview _overview;
    private static bool _testsStarted;
    // 北侧屋舍是主要交互目标，给屋顶留出HUD下方的完整展示空间。
    private static readonly Vector2 CityCameraLookAhead=new(0,-95);
    public override void _Ready()
    {
        Map = new CityMap { Name="Map", Broken=Progress.BoulderBroken,CacheOpen=Progress.CacheClaimed }; AddChild(Map);
        AddInteraction("captain","教头 · 韩正",new(467,266),true,new("915e49"));
        AddInteraction("inn","驿丞 · 阿宁",new(169,234),true,new("597b85"));
        AddInteraction("smith","铁匠 · 石叔",new(748,242),true,new("8b7354"));
        AddInteraction("resident","街坊 · 槐婆",new(233,526),true,new("748056"));
        AddInteraction("groom","马夫 · 小满",new(666,566),true,new("737f8b"));
        AddInteraction("gate","东门 · 前往演武场",new(903,294));
        AddInteraction("boulder","封路巨石",new(864,347));
        AddInteraction("cache","旧驿道的木匣",new(864,564));
        AddInteraction("cave","旧驿道 · 暗窟入口",new(864,475));
        for(int i=0;i<2;i++)
        {
            Vector2 position=new(671+i*77,528);
            // 展示马放在敞栏内，头部低于棚檐；骑乘仍使用玩家身上的完整坐骑表现。
            _stableHorses[i]=new CityHorse { Position=position,Scale=new(.8f,.8f),Appearance=_horseAppearances[i] }; Map.Actors.AddChild(_stableHorses[i]);
            AddInteraction(i==0 ? "horse-brown" : "horse-white",i==0 ? "枣红马" : "青骢马",position+new Vector2(0,8));
        }
        Player=GD.Load<PackedScene>(WorldScenePaths.Combatant).Instantiate<Combatant>();
        Player.Name="CityPlayer"; HeroCatalog.Configure(Player,0); Session.ConfigureActor(Player,true);
        Player.Position=Session.CityEntrance=="gate" ? new Vector2(857,294) : new Vector2(287,283);
        if(Session.CityEntrance=="cave") Player.Position=new(864,512);
        Map.Actors.AddChild(Player); Session.RestoreHealth(Player);
        Player.GetNode(NodeNames.GroundEffects).ProcessMode=ProcessModeEnum.Disabled;
        Player.GetNode<Node2D>(NodeNames.GroundEffects).Hide();
        _ridingHorse=new CityHorse { Name="RidingHorse",Rider=Player,Appearance=_horseAppearances[0],Visible=false }; Player.AddChild(_ridingHorse); Player.MoveChild(_ridingHorse,0);
        Camera=new Camera2D { Position=Player.Position+CityCameraLookAhead,Zoom=new(.85f,.85f), LimitLeft=0,LimitTop=-80,LimitRight=960,LimitBottom=640,PositionSmoothingEnabled=true,PositionSmoothingSpeed=9 };
        AddChild(Camera);
        BuildHud();
        Dialogue=new CityDialogue(); AddChild(Dialogue);
        RefreshHud();
        Toast(Session.SaveWarning.Length>0 ? Session.SaveWarning : "初到槐安 · 先与军营教头交谈。按 M 查看全城导览。",6);
        if(!_testsStarted && OS.GetCmdlineUserArgs().Contains("--horse-test")) { _testsStarted=true; GetTree().Root.CallDeferred(Node.MethodName.AddChild,new HorseVisualTests()); }
        if(!_testsStarted && OS.GetCmdlineUserArgs().Contains("--city-test")) { _testsStarted=true; GetTree().Root.CallDeferred(Node.MethodName.AddChild,new FirstCityTests()); }
    }
    private void AddInteraction(string id,string title,Vector2 position,bool npc=false,Color robe=default)
    {
        var item=new CityInteractable { Name=id,ContentId=id,Title=title,Position=position,IsNpc=npc,Robe=robe };
        Map.Actors.AddChild(item); Interactions.Add(item);
        if(npc) Map.Wall(new Rect2(position-new Vector2(7,5),new Vector2(14,10)));
    }
    public override void _PhysicsProcess(double delta)
    {
        float dt=(float)delta;
        bool blocked=Dialogue.IsOpen || _training || _smashRemaining>0 || _switching || _overview.Visible;
        Player.SetMoveInput(blocked ? Vector2.Zero : Input.GetVector(InputActions.MoveLeft, InputActions.MoveRight, InputActions.MoveUp, InputActions.MoveDown));
        Camera.Position=Player.Position+CityCameraLookAhead;
        if(_training)
        {
            TrainingRemaining=Mathf.Max(0,TrainingRemaining-dt); _trainingBeat-=dt;
            if(_trainingBeat<=0) { Player.TryAction(CombatAction.Basic); _trainingBeat=1; }
            if(TrainingRemaining<=0)
            {
                _training=false;
                if(Progress.Train(Config))
                {
                    int index=Progress.TrainingCount-1;
                    Player.Config.MaxHealth=GD.Load<CombatantConfig>("res://Resources/Config/Characters/lubu/lubu.tres").MaxHealth+Progress.HealthBonus(Config);
                    Player.Health.Configure(Player.Config.MaxHealth,false);
                    Player.ProgressionDamageMultiplier=Progress.PowerMultiplier(Config);
                    Commit(); Toast($"锻炼完成：生命上限 +{Config.TrainingHealth[index]}，伤害 +{Config.TrainingPower[index]:P0}",5);
                }
            }
        }
        if(_smashRemaining>0)
        {
            _smashRemaining-=dt;
            if(_smashRemaining<=0 && Player.Position.DistanceTo(new(864,347))<62 && Progress.Smash())
            { Map.RemoveBoulder(); Commit(); Toast("崩击！巨石碎裂，旧驿道已开启。",5); }
        }
        _toastRemaining-=dt; _notice.Visible=_toastRemaining>0;
        Nearest=FindNearest();
        foreach(var item in Interactions)
        {
            item.Highlighted=item==Nearest && !blocked;
            item.QuestMarker=item.ContentId=="captain" && Progress.Trial is AdventureProgress.QuestStage.Available or AdventureProgress.QuestStage.Completed;
            item.QueueRedraw();
        }
        _hint.Text=_training ? $"军营锻炼中 · {TrainingRemaining:0.0} 秒" : _smashRemaining>0 ? "蓄力 · 崩击" : Nearest!=null && !blocked ? "E  "+Nearest.Title : "WASD 移动   ·   E 交互   ·   I 行囊   ·   M 导览   ·   X 下马";
        RefreshHud();
    }
    /// <summary>统一最近目标查询；射线阻止隔着建筑和封路巨石与后方物品交互。</summary>
    public CityInteractable FindNearest() => InteractionQuery.FindNearest(Player, Interactions,
        item => !(item.ContentId == "boulder" && Progress.BoulderBroken || item.ContentId == "cache" && Progress.CacheClaimed),
        item => item.IsNpc ? 10 : 12);
    public override void _UnhandledInput(InputEvent input)
    {
        if(input is not InputEventKey { Pressed:true,Echo:false } key || Dialogue.IsOpen || _switching) return;
        if(key.PhysicalKeycode==Key.M || key.Keycode==Key.M) { _overview.Visible=!_overview.Visible; GetViewport().SetInputAsHandled(); return; }
        if(_overview.Visible) { if(key.Keycode==Key.Escape) _overview.Hide(); return; }
        if(_training || _smashRemaining>0) return;
        if(key.PhysicalKeycode==Key.I || key.Keycode==Key.I) { AdventureBackpack.Open(Dialogue,t=>Toast(t)); GetViewport().SetInputAsHandled(); return; }
        if(key.PhysicalKeycode==Key.X || key.Keycode==Key.X) { Dismount(); GetViewport().SetInputAsHandled(); }
        if(key.PhysicalKeycode==Key.E || key.Keycode==Key.E)
        {
            GetViewport().SetInputAsHandled();
            var target=FindNearest(); if(target!=null && Player.CanAct) Interact(target);
        }
    }
    public void BeginTraining()
    {
        if(_training || Progress.TrainingCount>=Config.TrainingHealth.Length) return;
        Dismount(); Player.Face(Vector2.Up); _training=true; TrainingRemaining=Config.TrainingSeconds; _trainingBeat=0;
        Toast("站桩、运气、挥击 · 训练收益逐次递减",Config.TrainingSeconds);
    }
    private void HorseDialogue(int horse)
    {
        float speed=horse==1 ? Config.BrownHorseSpeed : Config.WhiteHorseSpeed;
        Dialogue.Open(horse==1 ? "枣红马" : "青骢马",$"移动速度 {speed:0} 像素/秒，徒步为 {Config.WalkSpeed:0}。上马后人物屈膝执缰，跟随马匹步态起伏。",
            (MountedHorse==horse ? "下马" : "上马",()=> { if(MountedHorse==horse) Dismount(); else Mount(horse); }));
    }
    public void Mount(int horse)
    {
        if(horse is <1 or >2) return;
        Dismount(); MountedHorse=horse; Player.IsMounted=true;
        Player.ExplorationSpeedOverride=horse==1 ? Config.BrownHorseSpeed : Config.WhiteHorseSpeed;
        _ridingHorse.Appearance=_horseAppearances[horse-1]; _ridingHorse.ResetGait(); _ridingHorse.Show(); _stableHorses[horse-1].Hide();
        Toast($"已骑上{(horse==1 ? "枣红马" : "青骢马")} · X 下马");
    }
    public void Dismount()
    {
        MountedHorse=0; Player.IsMounted=false; Player.ExplorationSpeedOverride=0; _ridingHorse.Hide();
        foreach(var horse in _stableHorses) horse.Show();
    }
    public void EnterArena()
    {
        if(_switching) return;
        Dismount(); Commit(); _switching=true;
        if(Session.EnterTrial(GetTree())!=Error.Ok) { _switching=false; Toast("演武场加载失败，请重试"); }
    }
    private void Commit()
    {
        Progress.HealthRatio=(float)Player.Health.CurrentHealth/Player.Health.MaxHealth; Session.Save(); RefreshHud();
        if(Session.SaveWarning.Length>0) Toast(Session.SaveWarning,6);
    }
    public void Toast(string text,float seconds=3) { _notice.Text=text; _toastRemaining=seconds; _notice.Show(); }
}
