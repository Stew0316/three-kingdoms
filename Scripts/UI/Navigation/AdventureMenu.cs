using Godot;
using System;
using System.Linq;

/// <summary>城市、洞窟和演武共用的侧栏及分页；唯一负责自己打开的暂停，不覆盖外部暂停。</summary>
public partial class AdventureMenu : CanvasLayer
{
    public Combatant Player { get; set; }
    public Node2D World { get; set; }
    public string Region { get; set; }
    public CityDialogue Dialogue { get; set; }
    public FirstCity City { get; set; }
    public Func<bool> CanOpen { get; set; } = () => true;
    public bool IsOpen => _overlay?.Visible == true;
    public string Page { get; private set; } = "";
    public LocalMinimap Minimap { get; private set; }
    public WorldMapView WorldMap { get; private set; }
    public PlayerStatusBar StatusBar { get; private set; }
    public QuestBookButton QuestBook { get; private set; } // 右侧任务入口，发光状态直接读取冒险进度。
    public CanvasModulate Daylight { get; private set; }
    private Control _overlay, _content;
    private VBoxContainer _nav;
    private Button _firstButton;
    private bool _pausedBefore, _ownsPause;
    private string _waitingAction;
    private Label _bindingMessage;
    private Label _calendar;
    private AdventureSession Session => AdventureSession.Instance;
    private static readonly Color Gold = new("e8d4a2"), Ink = new("152c2a");
    private static readonly string[] Pages = { "地图", "物品栏", "人物", "背包", "坐骑", "设置" };

    public override void _Ready()
    {
        Layer = 15; ProcessMode = ProcessModeEnum.Always;
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiStyles.ChineseTheme(12) };
        AddChild(root); root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Minimap = new LocalMinimap { Player=Player, World=World, Region=Region, Position=new(10,286), Size=new(70,70),Circular=true };
        // 小地图与动作栏共用底部一行，圆形裁切不挡住左侧出生人物。

        root.AddChild(Minimap);
        StatusBar=new PlayerStatusBar { Player=Player,Position=new(0,298),Size=new(640,62),
            CanUse=()=>!IsOpen && !GetTree().Paused && Dialogue?.IsOpen!=true && CanOpen() };
        root.AddChild(StatusBar);
        root.AddChild(new DayNightDial { Name="DayNightDial",Position=new(284,1),Size=new(120,46),Scale=Vector2.One*.6f });
        _calendar=Label(root,Session.Clock.CalendarText,new(260,30,120,16),10);
        _calendar.Name="Calendar"; _calendar.HorizontalAlignment=HorizontalAlignment.Center;
        QuestBook=new QuestBookButton { Name="QuestBook",Position=new(594,156),Size=new(38,48) };
        QuestBook.Pressed+=()=>Open("任务");
        root.AddChild(QuestBook);
        // 同一世界画布只允许一个环境乘色；暗窟已经拥有自己的 CanvasModulate。
        if(Region!="暗窟") { Daylight=new CanvasModulate { Color=Session.Clock.Ambient }; GetParent().AddChild(Daylight); }
        _overlay = new Control { Visible=false }; root.AddChild(_overlay);
        _overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var shade = new ColorRect { Color=new Color("081513a0") }; _overlay.AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Panel(_overlay,new(482,12,146,316));
        Label(_overlay,"行 旅",new(498,25,114,26),22);
        Label(_overlay,Region+"  /  暂歇",new(498,54,114,18),10);
        _nav = new VBoxContainer { Position=new(494,77),Size=new(122,190) };
        _nav.AddThemeConstantOverride("separation",3); _overlay.AddChild(_nav);
        foreach (string page in Pages)
        {
            var button = new Button { Text=page,ToggleMode=true,CustomMinimumSize=new(122,27),Alignment=HorizontalAlignment.Left };
            StyleButton(button); _nav.AddChild(button); button.Pressed += () => ShowPage(page);
            _firstButton ??= button;
        }
        Button(_overlay,"返回游戏  [Esc]",new(494,264,122,25),Close);
        Button(_overlay,"退出游戏",new(494,294,122,25),()=>ShowPage("退出确认"));
        _content = new Control { Position=new(10,12),Size=new(462,316) }; _overlay.AddChild(_content);
        var footer=new ColorRect { Position=new(0,330),Size=new(640,30),Color=Ink };
        _overlay.AddChild(footer);
        Label(_overlay,"↑↓ 选择   Enter 确认   Esc 返回   M 天下图",new(190,335,434,17),10);
    }
    public void Open(string page = "")
    {
        if (!CanOpen() || Dialogue?.IsOpen == true) return;
        if (!IsOpen)
        {
            _pausedBefore=GetTree().Paused; _ownsPause=true; GetTree().Paused=true;
            Player.StopMoving(); Player.SuppressActionsUntilReleased=true; _overlay.Show();
        }
        ShowPage(page);
        _firstButton.GrabFocus();
    }
    public void Close()
    {
        if (!IsOpen) return;
        _overlay.Hide(); Page=""; _waitingAction=null;
        if (_ownsPause) { GetTree().Paused=_pausedBefore; _ownsPause=false; }
        Player.StopMoving();
        Player.SuppressActionsUntilReleased=true;
    }
    public override void _ExitTree()
    {
        if (_ownsPause && IsInsideTree()) GetTree().Paused=_pausedBefore;
    }
    /// <summary>使用输入阶段先处理菜单快捷键，避免 Esc 与旧战斗暂停重复执行。</summary>
    public override void _Input(InputEvent input)
    {
        if (input is not InputEventKey { Pressed:true,Echo:false } key) return;
        Key code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
        if(_waitingAction!=null)
        {
            if(code==Key.Escape) { _waitingAction=null; _bindingMessage.Text="已取消改键。"; }
            else if(key.CtrlPressed || key.AltPressed || key.MetaPressed || key.ShiftPressed) _bindingMessage.Text="请使用单个按键，不使用组合键。";
            else
            {
                bool changed=Session.Bindings.TrySet(_waitingAction,code,out string message);
                if(changed) { _waitingAction=null; ShowPage("设置"); }
                _bindingMessage.Text=message;
            }
            GetViewport().SetInputAsHandled(); return;
        }
        if (Dialogue?.IsOpen == true) return;
        if(!IsOpen && !GetTree().Paused && CanOpen())
        {
            for(int i=0;i<4;i++) if(input.IsActionPressed(InputActions.ItemSlots[i]))
            { StatusBar.UseItem(i); GetViewport().SetInputAsHandled(); return; }
            // 探索角色没有战斗控制器，仍可使用已装备技能；训练、骑乘和对话由场景约束。
            if(Region!="演武场" && !Player.IsMounted)
                for(int i=0;i<4;i++) if(input.IsActionPressed(InputActions.SkillSlots[i]))
                { Player.TryAction(InputActions.SkillActions[i]); GetViewport().SetInputAsHandled(); return; }
        }
        if (code is Key.Escape or Key.M or Key.I)
        {
            if (!IsOpen && !CanOpen()) return;
            if (code == Key.Escape)
            {
                if (!IsOpen) Open();
                else if (Page != "") ShowPage("");
                else Close();
            }
            else if (code == Key.M) { if (IsOpen && Page=="地图") Close(); else Open("地图"); }
            else { if (IsOpen && Page=="背包") Close(); else Open("背包"); }
            GetViewport().SetInputAsHandled();
        }
    }
    public void ShowPage(string page)
    {
        _waitingAction=null;
        Page=page;
        foreach(var button in _nav.GetChildren().OfType<Button>())
            button.SetPressedNoSignal(button.Text==page || page=="本地导览" && button.Text=="地图");
        foreach (var child in _content.GetChildren()) { _content.RemoveChild(child); child.QueueFree(); }
        _content.Visible=page!="";
        if (page=="") { _firstButton.GrabFocus(); return; }
        Panel(_content,new(0,0,462,316));
        Label(_content,page=="地图" ? "天 下 舆 图" : page,new(16,10,360,26),20);
        if (page=="地图") BuildMap();
        else if (page=="人物") BuildCharacter();
        else if (page=="坐骑") BuildMounts();
        else if(page is "物品栏" or "背包") BuildInventory(page=="物品栏");
        else if(page=="设置") BuildSettings();
        else if(page=="退出确认") BuildQuit();
        else if(page=="任务") BuildQuests();
    }
    public override void _Process(double delta)
    {
        if(Daylight!=null) Daylight.Color=Session.Clock.Ambient;
        _calendar.Text=Session.Clock.CalendarText;
        QuestBook.HasQuest=Session.Progress.Trial!=AdventureProgress.QuestStage.Rewarded;
        QuestBook.Visible=!IsOpen && Dialogue?.IsOpen!=true;
        QuestBook.Disabled=!CanOpen();
    }
    private void BuildQuests()
    {
        var progress=Session.Progress;
        string state=progress.Trial switch
        {
            AdventureProgress.QuestStage.Available=>"可接取",
            AdventureProgress.QuestStage.Accepted=>"进行中",
            AdventureProgress.QuestStage.Completed=>"待交付",
            _=>"已完成"
        };
        Label(_content,"演武试炼  ·  "+state,new(20,52,422,28),18);
        string objective=progress.Trial switch
        {
            AdventureProgress.QuestStage.Available=>"前往槐安城军营，与教头韩正交谈，接下演武委托。",
            AdventureProgress.QuestStage.Accepted=>"从槐安城东门进入演武场，击败魏延。完成后返回军营向韩正复命。",
            AdventureProgress.QuestStage.Completed=>"魏延已被击败。返回槐安城军营，与韩正交谈并领取报酬。",
            _=>"演武委托已完成，当前没有待办任务。可以继续探索槐安城、锻炼或前往旧驿道。"
        };
        var detail=Label(_content,objective,new(20,101,420,93),14);
        detail.Name="QuestObjective"; detail.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        Label(_content,$"报酬：铜钱 {Session.Config.QuestCoins} · 《行军要诀》\n兵书研习后永久增加 {Session.Config.BookPower:P0} 伤害",new(20,208,420,50),12);
        Button(_content,"查看天下图",new(20,277,150,27),()=>ShowPage("地图"));
        Button(_content,"返回游戏",new(292,277,150,27),Close);
    }
    private void BuildSettings()
    {
        Label(_content,"选择按键后按新键 · Esc 取消 · 重复按键会提示冲突",new(16,41,430,19),10);
        for(int i=0;i<KeyBindings.Entries.Length;i++)
        {
            var entry=KeyBindings.Entries[i]; int col=i/8,row=i%8;
            Label(_content,entry.Title,new(18+col*219,67+row*25,107,23),11);
            Button(_content,Session.Bindings.Label(entry.Action),new(127+col*219,67+row*25,84,23),()=>
            { _waitingAction=entry.Action; _bindingMessage.Text="请为“"+entry.Title+"”按下新键（Esc 取消）。"; });
        }
        _bindingMessage=Label(_content,"鼠标左键普攻；Esc / M / I / X 保留。",new(18,270,430,18),10);
        Button(_content,"恢复默认按键",new(18,291,146,23),()=>
        { var error=Session.Bindings.Reset(); ShowPage("设置"); _bindingMessage.Text=error==Error.Ok ? "已恢复并保存默认按键。" : "已恢复默认，但保存失败。"; });
    }
    private void BuildQuit()
    {
        Label(_content,"确认退出游戏？",new(35,89,390,32),22);
        var message=Label(_content,"退出前将保存当前冒险进度。",new(35,134,390,44),12);
        Button(_content,"取消",new(35,208,163,34),()=>ShowPage(""));
        Button(_content,"确认退出",new(224,208,190,34),()=>
        {
            Session.Save();
            if(Session.SaveWarning.Length>0) { message.Text=Session.SaveWarning+"\n未退出，请重试。"; return; }
            GetTree().Quit();
        });
    }
    private void BuildMap()
    {
        WorldMap = new WorldMapView { Atlas=Session.Atlas,Position=new(10,42),Size=new(300,256) };
        _content.AddChild(WorldMap);
        var pick = new OptionButton { Position=new(320,44),Size=new(130,28) };
        foreach (var city in Session.Atlas.Cities) pick.AddItem(city.Name);
        _content.AddChild(pick);
        var info = Label(_content,"",new(320,81,130,130),11);
        info.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        WorldMap.Selected += city =>
        {
            pick.Select(Array.IndexOf(Session.Atlas.Cities,city));
            string neighbors=string.Join("、",Session.Atlas.Connections(city.Id).Select(r => Session.Atlas.Find(r.From==city.Id ? r.To : r.From).Name));
            info.Text=$"{city.Name} · 待建设\n\n相邻城池\n{neighbors}\n\n金色陆路 / 青色水路";
        };
        pick.ItemSelected += index => WorldMap.SelectCity(Session.Atlas.Cities[(int)index].Id,true);
        WorldMap.SelectCity("luoyang");
        Button(_content,"−",new(320,218,37,26),()=>WorldMap.ChangeZoom(1/1.3f));
        Button(_content,"＋",new(365,218,37,26),()=>WorldMap.ChangeZoom(1.3f));
        Button(_content,"全图",new(409,218,41,26),()=>WorldMap.ResetView());
        Button(_content,"本地导览",new(320,254,130,26),ShowLocalOverview);
        Label(_content,"滚轮缩放 · 拖动地图 · 点选城池",new(14,298,300,16),9);
    }
    private void ShowLocalOverview()
    {
        ShowPage("本地导览");
        var overview = new LocalMinimap { Player=Player,World=World,Region=Region,Overview=true,Position=new(14,44),Size=new(434,230) };
        _content.AddChild(overview);
        Label(_content,"槐安所在天下位置待剧本确定",new(18,282,280,20),11);
        Button(_content,"天下图",new(350,280,96,25),()=>ShowPage("地图"));
    }
    private void BuildCharacter()
    {
        Label(_content,Player.Ui.DisplayName,new(18,49,220,26),22);
        Label(_content,$"生命    {Player.Health.CurrentHealth} / {Player.Health.MaxHealth}\n伤害倍率    ×{Player.ProgressionDamageMultiplier:0.00}\n基础徒步速度    {Player.Config.MoveSpeed:0}",new(18,88,420,70),13);
        var speed = Label(_content,"",new(18,174,420,23),13);
        void Refresh() => speed.Text=$"移动倍率 ×{Player.MoveRate:0.00}     当前目标速度 {Player.TargetMoveSpeed:0}";
        Refresh();
        foreach (var choice in new[] {(.5f,"慢行"),(1f,"正常"),(1.5f,"快行"),(2f,"疾行")}.Select((p,i)=>(p,i)))
        {
            float rate=choice.p.Item1;
            Button(_content,$"{choice.p.Item2} ×{rate:0.0}",new(18+choice.i*108,209,101,30),()=>
            {
                Session.SetMoveRate(Player,rate); Refresh();
                if(Session.SaveWarning.Length>0) speed.Text=Session.SaveWarning;
            });
        }
        Label(_content,"起步渐快，松键收势；骑乘也使用此倍率。\n进入战斗后，技能位移仍按各自招式执行。",new(18,258,420,41),11);
    }
    private void BuildInventory(bool quick)
    {
        var progress=Session.Progress;
        Label(_content,quick ? "随手取用的行旅道具" : $"全部持有物   ·   铜钱 {progress.Coins}",new(18,48,422,24),13);
        if(progress.HasTorch)
        {
            Label(_content,"火把  ×1\n"+(Session.TorchInUse ? "正在使用 · 可点燃壁烛" : "尚未使用 · 照亮身边的暗处"),new(18,89,420,47),13);
            Button(_content,Session.TorchInUse ? "收起火把" : "使用火把",new(18,150,180,31),()=>
            {
                if(Session.TorchInUse) Session.PutAwayTorch(); else Session.UseTorch();
                ShowPage(quick ? "物品栏" : "背包");
            });
        }
        else Label(_content,"尚无可用道具。\n可在驿站或暗窟入口领取火把。",new(18,90,420,55),13);
        if(!quick)
            Label(_content,progress.HasBook ? "《行军要诀》 ×1  ·  已研习\n永久增加 10% 伤害" : "兵书：尚未获得\n完成军营演武委托后领取。",new(18,201,420,55),13);
        Label(_content,quick ? "火把可随时使用或收起，不消耗数量。" : "任务所得与行旅道具均汇入此处。",new(18,282,420,18),10);
    }
    private void BuildMounts()
    {
        Label(_content,"槐安马厩 · 可借用坐骑",new(18,49,420,23),13);
        for(int i=1;i<=2;i++)
        {
            int horse=i; float speed=i==1 ? Session.Config.BrownHorseSpeed : Session.Config.WhiteHorseSpeed;
            Label(_content,$"{(i==1 ? "枣红马" : "青骢马")}   速度 {speed:0}   ×{Player.MoveRate:0.0}",new(18,90+(i-1)*69,420,24),14);
            var button=Button(_content,City?.MountedHorse==horse ? "下马" : "骑乘",new(18,118+(i-1)*69,170,28),()=>
            {
                if(City.MountedHorse==horse) City.Dismount(); else City.Mount(horse);
                ShowPage("坐骑");
            });
            button.Disabled=City==null;
        }
        Label(_content,City==null ? "返回槐安城后可骑乘；洞窟和演武场需徒步。" : "X 可随时下马；进入洞窟或演武场会自动下马。",new(18,265,423,40),11);
    }
    private static void Panel(Control parent,Rect2 rect)
    {
        var panel=new Panel { Position=rect.Position,Size=rect.Size,MouseFilter=Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel",UiStyles.Panel(Ink,new Color("ac9768"),1,4)); parent.AddChild(panel);
    }
    private static Label Label(Control parent,string text,Rect2 rect,int size)
    {
        var label=new Label { Text=text,Position=rect.Position,Size=rect.Size,MouseFilter=Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size",size); label.AddThemeColorOverride("font_color",Gold); parent.AddChild(label); return label;
    }
    private static void StyleButton(Button button)
    {
        // 显式控制所有状态的内边距，防止系统主题的最小高度挤出固定布局。
        button.AddThemeFontSizeOverride("font_size",11);
        button.AddThemeStyleboxOverride("normal",UiStyles.Panel(new Color("24403a"),new Color("5d7060"),1,4,4,2));
        button.AddThemeStyleboxOverride("hover",UiStyles.Panel(new Color("3c5747"),Gold,1,4,4,2));
        button.AddThemeStyleboxOverride("pressed",UiStyles.Panel(new Color("435d4b"),Gold,1,4,4,2));
        button.AddThemeStyleboxOverride("disabled",UiStyles.Panel(new Color("24322d"),new Color("5d7060"),1,4,4,2));
        button.AddThemeStyleboxOverride("focus",UiStyles.Panel(new Color("00000000"),Gold,2,4,0,0));
        button.AddThemeColorOverride("font_color",Gold);
    }
    private static Button Button(Control parent,string text,Rect2 rect,Action action)
    {
        var button=new Button { Text=text,Position=rect.Position,Size=rect.Size };
        StyleButton(button); button.Pressed+=action; parent.AddChild(button);
        // 入树继承主题之后重新设定尺寸，避免初始系统主题最小高度残留。
        button.Size=rect.Size;
        return button;
    }
}


