using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>旧驿道洞窟：输入、近距交互和场景装配；物品与点火进度通过会话提交。</summary>
public partial class OldRoadCave : Node2D
{
    public Combatant Player { get; private set; }
    public CaveMap Map { get; private set; }
    public CityDialogue Dialogue { get; private set; }
    public Camera2D Camera { get; private set; }
    public AdventureMenu Navigation { get; private set; }
    public CanvasModulate Darkness { get; private set; }
    public List<CityInteractable> Interactions { get; } = new();
    public Dictionary<string,CaveFlame> Candles { get; } = new();
    public CaveFlame Torch { get; private set; }
    private AdventureSession Session => AdventureSession.Instance;
    private Label _status, _hint, _notice;
    private float _noticeTime;
    private bool _switching;
    private static bool _testsStarted;
    public override void _Ready()
    {
        Map=new CaveMap(); AddChild(Map);
        Darkness=new CanvasModulate { Color=new Color(.105f,.12f,.125f) }; AddChild(Darkness);
        foreach(var c in CaveLayout.Candles)
        {
            var candle=new CaveFlame { Position=c.Position,Burning=Session.Progress.LitCaveCandles.Contains(c.Id) };
            Map.Actors.AddChild(candle); Candles.Add(c.Id,candle);
            AddInteraction(c.Id,c.Title+" · 壁上蜡烛",c.Position+new Vector2(0,7));
        }
        AddInteraction("exit","返回槐安 · 旧驿道",CaveLayout.Entrance+new Vector2(0,31));
        AddInteraction("supplies","行旅物资 · 领取火把",CaveLayout.Supplies);
        var entranceLight=CaveFlame.CreateLight(95); entranceLight.Position=CaveLayout.Entrance+new Vector2(0,48);
        entranceLight.Color=new Color("bdc7ac"); entranceLight.Energy=.85f; AddChild(entranceLight);
        Player=GD.Load<PackedScene>(WorldScenePaths.Combatant).Instantiate<Combatant>();
        HeroCatalog.Configure(Player,0); Session.ConfigureActor(Player,true); Player.Position=CaveLayout.Entrance;
        Map.Actors.AddChild(Player); Session.RestoreHealth(Player);
        Player.GetNode<Node2D>(NodeNames.GroundEffects).Hide(); Player.GetNode(NodeNames.GroundEffects).ProcessMode=ProcessModeEnum.Disabled;
        Torch=new CaveFlame { IsTorch=true,Position=new(-17,-17),Burning=Session.TorchInUse }; Player.AddChild(Torch);
        Camera=new Camera2D { Position=Player.Position,Zoom=Vector2.One*Session.Settings.CaveZoom,PositionSmoothingEnabled=true,PositionSmoothingSpeed=9,
            LimitLeft=0,LimitRight=1152,LimitTop=0,LimitBottom=704 }; AddChild(Camera);
        BuildHud(); Dialogue=new CityDialogue(); AddChild(Dialogue);
        Navigation=new AdventureMenu { Player=Player,World=Map,Region="暗窟",Dialogue=Dialogue,CanOpen=()=>!_switching };
        AddChild(Navigation);
        Toast(Session.Progress.LitCaveCandles.Count>0 ? "重返旧道 · 已点燃的壁烛仍在照亮石室" : Session.TorchInUse ? $"举火探路 · 靠近墙上蜡烛按 {Session.Bindings.Label(InputActions.Interact)} 点火" : "洞里一片昏暗 · 领取入口火把，按 I 打开行囊并使用",6);
        if(!_testsStarted && OS.GetCmdlineUserArgs().Contains("--cave-test"))
        { _testsStarted=true; GetTree().Root.CallDeferred(Node.MethodName.AddChild,new CaveTests()); }
    }
    private void AddInteraction(string id,string title,Vector2 position)
    {
        var target=new CityInteractable { ContentId=id,Title=title,Position=position,ShowLabel=false };
        Map.Actors.AddChild(target); Interactions.Add(target);
    }
    public override void _PhysicsProcess(double delta)
    {
        bool blocked=Dialogue.IsOpen || _switching || Navigation.IsOpen;
        if(blocked) Player.StopMoving();
        Player.SetMoveInput(blocked ? Vector2.Zero : Input.GetVector(InputActions.MoveLeft, InputActions.MoveRight, InputActions.MoveUp, InputActions.MoveDown));
        Camera.Position=Player.Position+new Vector2(0,-35);
        Torch.Burning=Session.TorchInUse && Session.Progress.HasTorch;
        var nearest=FindNearest();
        foreach(var item in Interactions) { item.Highlighted=item==nearest && !Dialogue.IsOpen; item.QueueRedraw(); }
        _hint.Text=nearest==null ? $"I 行囊 / 使用火把 · {Session.Bindings.Label(InputActions.Interact)} 点火或调查" : Session.Bindings.Label(InputActions.Interact)+"  "+nearest.Title+"    ·    I 行囊";
        _status.Text=$"旧驿道 · 暗窟     壁烛 {Session.Progress.LitCaveCandles.Count} / {Candles.Count}     火把：{(Torch.Burning ? "使用中" : Session.Progress.HasTorch ? "未使用 · 按 I" : "未领取")}";
        _noticeTime-=(float)delta; _notice.Visible=_noticeTime>0;
    }
    public CityInteractable FindNearest() => InteractionQuery.FindNearest(Player, Interactions);
    public override void _UnhandledInput(InputEvent input)
    {
        if(input is not InputEventKey { Pressed:true,Echo:false } key || Dialogue.IsOpen || _switching) return;
        if(key.PhysicalKeycode==Key.I || key.Keycode==Key.I) { AdventureBackpack.Open(Dialogue,t=>Toast(t)); GetViewport().SetInputAsHandled(); }
        if(input.IsActionPressed(InputActions.Interact)) { Interact(FindNearest()); GetViewport().SetInputAsHandled(); }
    }
    public void Interact(CityInteractable target)
    {
        if(target==null || Dialogue.IsOpen || Navigation.IsOpen || _switching || target!=FindNearest()) return;
        if(target.ContentId=="exit")
        {
            Dialogue.Open("旧驿道出口","沿石阶返回槐安城。已经点燃的蜡烛会继续照亮各自区域。",("返回槐安",()=>
            {
                _switching=true;
                if(Session.LeaveCave(GetTree(),Player)!=Error.Ok) { _switching=false; Toast("出口加载失败，请重试"); }
            })); return;
        }
        if(target.ContentId=="supplies")
        {
            if(Session.Progress.ReceiveTorch()) { Session.Save(); Dialogue.Open("行旅物资","获得火把 ×1，已放入行囊。按 I 打开行囊，选择“使用火把”后才能点燃壁烛。"); }
            else Toast("你已经有一支火把 · 按 I 使用或收起");
            ShowSaveWarning(); return;
        }
        if(!Session.Progress.HasTorch) { Toast("需要火把 · 入口行旅物资中可以领取"); return; }
        if(!Session.TorchInUse) { Toast("先按 I 打开行囊，选择“使用火把”"); return; }
        if(Session.Progress.LightCaveCandle(target.ContentId,Session.TorchInUse))
        {
            Candles[target.ContentId].Burning=true; Session.Save(); Toast("壁烛已点燃 · 这片区域亮起来了"); ShowSaveWarning();
        }
        else Toast("这盏蜡烛已经点燃");
    }
    private void ShowSaveWarning() { if(Session.SaveWarning.Length>0) Toast(Session.SaveWarning,6); }
    public void Toast(string text,float seconds=3) { _notice.Text=text; _noticeTime=seconds; _notice.Show(); }
    private void BuildHud()
    {
        var layer=new CanvasLayer { Layer=2 }; AddChild(layer);
        var root=new Control { MouseFilter=Control.MouseFilterEnum.Ignore }; root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); layer.AddChild(root);
        root.Theme=UiStyles.ChineseTheme(12);
        var top=new Panel { Position=new(10,48),Size=new(620,37),MouseFilter=Control.MouseFilterEnum.Ignore };
        top.AddThemeStyleboxOverride("panel",CityDialogue.Box(new Color("162d30e8"),new Color("8c865d"))); root.AddChild(top);
        _status=new Label { Position=new(22,56),Size=new(598,24),Modulate=new Color("ead5a0") }; root.AddChild(_status);
        var bottom=new ColorRect { Position=new(0,285),Size=new(640,17),Color=new Color("132c2ded"),MouseFilter=Control.MouseFilterEnum.Ignore }; root.AddChild(bottom);
        _hint=new Label { Position=new(12,285),Size=new(616,18),HorizontalAlignment=HorizontalAlignment.Center }; root.AddChild(_hint);
        _notice=new Label { Position=new(168,260),Size=new(460,25),HorizontalAlignment=HorizontalAlignment.Center,Modulate=new Color("fff1c0") }; root.AddChild(_notice);
        _notice.AddThemeColorOverride("font_shadow_color",new Color("162d30")); _notice.AddThemeConstantOverride("shadow_offset_y",1);
    }
}
