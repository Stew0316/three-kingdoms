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
        Player=GD.Load<PackedScene>("res://Components/Characters/Combatant.tscn").Instantiate<Combatant>();
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
        Player.SetMoveInput(blocked ? Vector2.Zero : Input.GetVector("move_left","move_right","move_up","move_down"));
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
                    Player.Config.MaxHealth=GD.Load<CombatantConfig>("res://Resources/Config/Combat/lubu.tres").MaxHealth+Progress.HealthBonus(Config);
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
    public CityInteractable FindNearest()
    {
        foreach(var item in Interactions.OrderBy(i=>i.Position.DistanceSquaredTo(Player.Position)))
        {
            if(item.ContentId=="boulder" && Progress.BoulderBroken || item.ContentId=="cache" && Progress.CacheClaimed) continue;
            if(item.Position.DistanceSquaredTo(Player.Position)>48*48) continue;
            var ray=PhysicsRayQueryParameters2D.Create(Player.GlobalPosition,item.GlobalPosition,1);
            var hit=GetWorld2D().DirectSpaceState.IntersectRay(ray);
            // NPC脚边的身体碰撞、巨石本身可以作为射线终点，其他前置障碍则阻断。
            if(hit.Count>0 && ((Vector2)hit["position"]).DistanceTo(item.GlobalPosition)>(item.IsNpc?10:12)) continue;
            return item;
        }
        return null;
    }
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
    public void Interact(CityInteractable target)
    {
        if(Dialogue.IsOpen || _training || _smashRemaining>0 || _switching || target!=FindNearest()) return;
        Player.SetMoveInput(Vector2.Zero);
        switch(target.ContentId)
        {
            case "captain": CaptainDialogue(); break;
            case "inn": Dialogue.Open("阿宁 · 槐安驿站","远行先歇脚。驿站可免费恢复全部生命。现在只开通槐安这一处驿站，待游历更多城池再通驿路。",
                ("歇息 · 恢复全部生命",()=> { Player.Health.ResetHealth(); Commit(); Toast("驿站休息完毕 · 生命已恢复"); }),
                ("领取行旅火把",()=> { bool received=Progress.ReceiveTorch(); Commit(); Toast(received ? "火把已收入行囊 · 按 I 使用，可点亮旧驿道暗窟的壁烛" : "行囊中已有火把 · 按 I 使用"); })); break;
            case "smith": Dialogue.Open("石叔 · 铁匠铺",Progress.WeaponForged ? "兵刃已开锋。这份整备会随你带入演武场。" : $"替兵刃开锋需要 {Config.ForgeCost} 铜钱，永久增加 {Config.ForgePower:P0} 伤害。本次只可开锋一次。",
                (Progress.WeaponForged ? "查看已整备的兵刃" : $"开锋 · {Config.ForgeCost} 铜钱",()=>
                {
                    if(Progress.Forge(Config)) { Player.ProgressionDamageMultiplier=Progress.PowerMultiplier(Config); Commit(); Toast("开锋完成 · 伤害 +10%"); }
                    else Toast(Progress.WeaponForged ? "兵刃已经开锋" : "铜钱不足，先去完成演武任务或寻找旧驿道木匣");
                })); break;
            case "resident": Dialogue.Open("槐婆 · 槐下民居","城门旁那块巨石，寻常刀剑打不动。韩教头会教你崩击。石后是废弃的旧驿道，据说还留着一只木匣。\n东边出城是演武场；南边马厩的两匹马脚程可不一样。"); break;
            case "groom": Dialogue.Open("小满 · 马厩","枣红马性子稳，青骢马脚程快。走进马厩，靠近想骑的马按 E 上马；按 X 下马，马匹会送回原栏。\n骑马可以在城里赶路，进演武场时先下马。",
                ($"枣红马 {Config.BrownHorseSpeed:0} / 青骢马 {Config.WhiteHorseSpeed:0} 像素每秒",()=>Toast("进马厩靠近马匹，按 E 选择坐骑"))); break;
            case "horse-brown": HorseDialogue(1); break;
            case "horse-white": HorseDialogue(2); break;
            case "gate": Dialogue.Open("东门 · 演武场",Progress.Trial==AdventureProgress.QuestStage.Available ? "城外演武场默认魏延、关羽、刘备三将同场。建议先去军营接任务，击败魏延后即可回城交付。" : "进入四人演武场，与魏延、关羽、刘备切磋。击败魏延即可按“返回槐安”回城交任务，也可继续挑战其余两将；失败可回驿站恢复。",
                ("出城 · 挑战魏延",EnterArena)); break;
            case "boulder":
                Dialogue.Open("城门边的巨石",Progress.CanSmash ? "裂隙清晰可见，教头传授的崩击正好能破开这块巨石。" : "巨石纹丝不动。你还没有“崩击”能力，去军营找韩教头接下演武任务。",
                    (Progress.CanSmash ? "施展崩击 · 破开巨石" : "试着击打",()=>
                    {
                        Dismount(); Player.Face(Vector2.Down); Player.TryAction(CombatAction.Basic);
                        if(Progress.CanSmash) _smashRemaining=.5f; else Toast("寻常攻击无法破坏巨石 · 需要崩击");
                    })); break;
            case "cache": if(Progress.ClaimCache()) { Map.CacheOpen=true; Map.QueueRedraw(); Commit(); Dialogue.Open("旧驿道木匣","你沿着巨石后的小路找到遗失的行囊。获得 80 铜钱，可带回铁匠铺整备兵刃。"); } break;
            case "cave":
                if(!Progress.BoulderBroken) { Toast("先用崩击打开旧驿道"); break; }
                Dialogue.Open("旧驿道 · 暗窟","洞内没有点燃的蜡烛。需要在行囊中使用火把，再靠近壁烛逐盏点燃；每盏照亮一片区域。洞口备有行旅火把。",
                    ("进入洞窟",()=> { Dismount(); _switching=true; if(Session.EnterCave(GetTree(),Player)!=Error.Ok) { _switching=false; Toast("洞窟加载失败，请重试"); } })); break;
        }
    }
    private void CaptainDialogue()
    {
        string task=Progress.Trial switch
        {
            AdventureProgress.QuestStage.Available=>"去东门外的演武场击败魏延，再回来见我。我先传你“崩击”，可破开东门边的巨石。凯旋后赠你《行军要诀》，增加伤害。",
            AdventureProgress.QuestStage.Accepted=>"任务：前往东门演武场击败魏延。崩击已经传你，城门巨石也可以先去试试。",
            AdventureProgress.QuestStage.Completed=>"打得好！你已击败魏延。收下这卷《行军要诀》和100铜钱，再准备下一段旅程。",
            _=>"《行军要诀》已收入行囊并研习，伤害永久 +10%。路还长，别忘了整备与锻炼。"
        };
        int n=Progress.TrainingCount;
        var choices=new List<(string,Action)>();
        if(Progress.Trial==AdventureProgress.QuestStage.Available) choices.Add(("接下演武任务 · 学会崩击",()=> { Progress.AcceptTrial(); Commit(); Toast("已接任务：击败魏延 · 已学会地形能力：崩击",5); }));
        if(Progress.Trial==AdventureProgress.QuestStage.Completed) choices.Add(("交付任务 · 领取兵书与100铜钱",()=> { if(Progress.ClaimTrial(Config)) { Player.ProgressionDamageMultiplier=Progress.PowerMultiplier(Config); Commit(); Toast("获得《行军要诀》 · 伤害 +10% · 铜钱 +100",6); } }));
        if(n<Config.TrainingHealth.Length) choices.Add(($"锻炼第{n+1}次 · 生命 +{Config.TrainingHealth[n]} / 伤害 +{Config.TrainingPower[n]:P0}",BeginTraining));
        else task+="\n本城三次锻炼已经完成，继续游历吧。";
        Dialogue.Open("韩正 · 军营教头",task,choices.ToArray());
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
    private void BuildHud()
    {
        var layer=new CanvasLayer(); AddChild(layer);
        var root=new Control { MouseFilter=Control.MouseFilterEnum.Ignore }; root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); layer.AddChild(root);
        root.Theme=new Theme { DefaultFont=new SystemFont { FontNames=GameFontNames.CreateChineseFallbacks() },DefaultFontSize=11 };
        var top=new Panel { Position=new(10,8),Size=new(620,54),MouseFilter=Control.MouseFilterEnum.Ignore }; top.AddThemeStyleboxOverride("panel",CityDialogue.Box(new Color("162d30e8"),new Color("8c865d"))); root.AddChild(top);
        Label LabelAt(Vector2 pos,Vector2 size,int font=11)
        { var label=new Label { Position=pos,Size=size,MouseFilter=Control.MouseFilterEnum.Ignore }; label.AddThemeFontSizeOverride("font_size",font); root.AddChild(label); return label; }
        var title=LabelAt(new(22,13),new(132,22),18); title.Text=Config.CityName+" · 初行"; title.Modulate=new Color("f0d89f");
        _stats=LabelAt(new(162,16),new(454,20));
        _quest=LabelAt(new(23,39),new(590,18)); _quest.Modulate=new Color("c7d5b7");
        var bottom=new ColorRect { Position=new(0,332),Size=new(640,28),Color=new Color("132c2dec"),MouseFilter=Control.MouseFilterEnum.Ignore }; root.AddChild(bottom);
        _hint=LabelAt(new(14,337),new(612,18)); _hint.HorizontalAlignment=HorizontalAlignment.Center;
        _notice=LabelAt(new(20,304),new(600,24),12); _notice.HorizontalAlignment=HorizontalAlignment.Center; _notice.AddThemeColorOverride("font_color",new Color("fff1c0")); _notice.AddThemeColorOverride("font_shadow_color",new Color("162d30")); _notice.AddThemeConstantOverride("shadow_offset_x",1); _notice.AddThemeConstantOverride("shadow_offset_y",1);
        _overview=new CityOverview { City=this,Visible=false }; root.AddChild(_overview);
    }
    private void RefreshHud()
    {
        _stats.Text=$"生命 {Player.Health.CurrentHealth}/{Player.Health.MaxHealth}   铜钱 {Progress.Coins}   伤害 ×{Progress.PowerMultiplier(Config):0.00}   {(MountedHorse==0 ? "徒步" : MountedHorse==1 ? "枣红马" : "青骢马")}";
        _quest.Text=Progress.Trial switch
        {
            AdventureProgress.QuestStage.Available=>"初来乍到  /  与军营教头交谈，接下第一份委托",
            AdventureProgress.QuestStage.Accepted=>"演武试炼  /  出东门击败魏延  ·  地形能力：崩击已习得",
            AdventureProgress.QuestStage.Completed=>"演武试炼  /  已击败魏延，返回军营领取《行军要诀》",
            _=>"演武试炼已完成  /  《行军要诀》已研习  ·  "+(Progress.BoulderBroken ? "旧驿道已开启" : "可用崩击打开东门巨石")
        };
    }
}

/// <summary>首城导览用固定地图标识和实时玩家坐标，不要求玩家记住屏幕外设施。</summary>
public partial class CityOverview : Control
{
    private Font _font;
    public override void _Ready() => _font=new SystemFont { FontNames=GameFontNames.CreateChineseFallbacks() };
    public FirstCity City { get; set; }
    public override void _Process(double delta) { if(Visible) QueueRedraw(); }
    public override void _Draw()
    {
        DrawRect(new Rect2(0,0,640,360),new Color("122b2bf5"));
        var font=_font;
        DrawString(font,new(47,35),"槐安城导览    ·    M / Esc 返回",HorizontalAlignment.Left,-1,18,new Color("edd7a3"));
        Vector2 Map(Vector2 p)=>new Vector2(54,56)+p*.43f;
        DrawRect(new Rect2(Map(new(28,32)),new Vector2(904,576)*.43f),new Color("577052"));
        DrawRect(new Rect2(Map(new(28,240)),new Vector2(904,80)*.43f),new Color("b7a27c"));
        foreach(int x in new[]{288,576}) DrawRect(new Rect2(Map(new(x,80)),new Vector2(64,512)*.43f),new Color("b7a27c"));
        var points=new (Vector2,string)[]{(new(100,155),"驿站"),(new(406,148),"军营"),(new(701,155),"铁匠铺"),(new(100,440),"民居"),(new(650,465),"马厩")};
        foreach(var (p,name) in points) { DrawRect(new Rect2(Map(p-new Vector2(10,30)),new(56,34)),new Color("354e50")); DrawString(font,Map(p),name,HorizontalAlignment.Left,-1,12,new Color("efdcb0")); }
        DrawCircle(Map(new(865,365)),8,City.Map.Broken?new Color("cab889"):new Color("9b9f8c"));
        DrawString(font,new(467,165),"东门 → 演武场",HorizontalAlignment.Left,-1,11,new Color("f2dcaa"));
        DrawString(font,new(467,218),"巨石 / 旧驿道",HorizontalAlignment.Left,-1,11,new Color("f2dcaa"));
        DrawString(font,new(467,239),"旧驿道 → 暗窟",HorizontalAlignment.Left,-1,11,new Color("f2dcaa"));
        DrawCircle(Map(City.Player.Position),5,new Color("ffdb6e"));
        DrawString(font,new(47,345),"● 你的位置     接任务 → 演武 → 回城领兵书     军营锻炼 / 铁匠整备 / 马厩试骑",HorizontalAlignment.Left,-1,11,new Color("d1d9be"));
    }
}


