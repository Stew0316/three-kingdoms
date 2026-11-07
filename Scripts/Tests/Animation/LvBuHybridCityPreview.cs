using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>把混元模型导出的二维帧图放入真实槐安地图；独立预览不参与任务、伤害或存档。</summary>
public partial class LvBuHybridCityPreview : Node2D
{
    private const string Output = "res://Build/LvBuHybridCityV1";
    private const int Cell = 192;
    private const float ArtScale = .50f, WalkSpeed = 16f;
    // 与图集导出时的正交相机、模型脚点一致，转向时始终围绕二维脚点绘制。
    private const float OrthoSize = 2.55f, Elevation = 22f;
    private static readonly Vector3 ModelFoot = new(-.11f, -.998f, .04f);
    private static readonly Vector2 Start = new(405, 282);
    private readonly Dictionary<string, Texture2D> _atlases = new();
    private readonly Dictionary<string, int> _counts = new() { ["Idle"] = 24, ["Walk"] = 24, ["Attack"] = 28 };
    private readonly Dictionary<string, double> _lengths = new() { ["Idle"] = 2.4, ["Walk"] = 1.2, ["Attack"] = 1.4 };
    private CityMap _map;
    private CharacterBody2D _actor;
    private Sprite2D _sprite;
    private Camera2D _camera;
    private Label _status;
    private Vector2 _facing = Vector2.Right;
    private string _clip = "Idle";
    private double _clock, _demoClock;
    private bool _demo, _capture;

    public override void _Ready()
    {
        GetWindow().Title = "吕布 · 槐安城二维场景";
        GetWindow().ContentScaleSize = new(960, 600);
        GetWindow().Size = new(1280, 800);
        RenderingServer.SetDefaultClearColor(new Color("282d27"));
        _capture = OS.GetCmdlineUserArgs().Contains("--capture-lvbu-city");
        _map = new CityMap { Name = "Map" }; AddChild(_map);
        foreach (var (pos, robe) in new[] { (new Vector2(169,234), "597b85"), (new Vector2(467,266), "915e49"),
            (new Vector2(748,242), "8b7354"), (new Vector2(233,526), "748056"), (new Vector2(666,566), "737f8b") })
        {
            _map.Actors.AddChild(new CityInteractable { Position = pos, IsNpc = true, Robe = new Color(robe), ShowLabel = false });
            _map.Wall(new Rect2(pos - new Vector2(7,5), new Vector2(14,10)));
        }
        foreach (string name in _counts.Keys)
        {
            var texture = GD.Load<Texture2D>($"res://Source/People/LvBu/HunyuanV1/LvBu{name}FourDirections.png");
            if (texture == null || texture.GetWidth() != Cell * _counts[name] || texture.GetHeight() != Cell * 4)
                throw new InvalidOperationException("二维动画图集缺失或尺寸错误：" + name);
            _atlases[name] = texture;
        }
        _actor = new CharacterBody2D { Name = "LvBu", Position = Start, CollisionLayer = 0,
            CollisionMask = PhysicsLayers.WorldBody, MotionMode = CharacterBody2D.MotionModeEnum.Floating };
        _actor.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 8 } });
        var shadow = new Polygon2D { Color = new Color(0,0,0,.19f) };
        shadow.Polygon = Enumerable.Range(0,32).Select(i => new Vector2(Mathf.Cos(i*Mathf.Tau/32)*11, Mathf.Sin(i*Mathf.Tau/32)*3.6f+1)).ToArray();
        _actor.AddChild(shadow);
        _sprite = new Sprite2D { Centered = false, RegionEnabled = true, Scale = Vector2.One*ArtScale,
            TextureFilter = TextureFilterEnum.Linear, RegionFilterClipEnabled = true };
        _actor.AddChild(_sprite); _map.Actors.AddChild(_actor);
        _camera = new Camera2D { Zoom = Vector2.One*1.35f, LimitLeft = 0, LimitTop = 0,
            LimitRight = 960, LimitBottom = 640, Position = Start + new Vector2(0,-45) };
        AddChild(_camera); _camera.MakeCurrent();
        BuildHud(); RefreshFrame();
        if (_capture) CallDeferred(MethodName.Capture);
    }

    private void BuildHud()
    {
        var layer = new CanvasLayer(); AddChild(layer);
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiStyles.ChineseTheme(14) };
        layer.AddChild(root); root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(new ColorRect { Size = new(960,72), Color = new Color("202923ef"), MouseFilter = Control.MouseFilterEnum.Ignore });
        var title = new Label { Text = "吕布 · 槐安城", Position = new(20,10) };
        title.AddThemeFontSizeOverride("font_size",23); title.AddThemeColorOverride("font_color",new Color("e4cfaa")); root.AddChild(title);
        root.AddChild(new Label { Text = "WASD / 方向键移动   ·   J 挥戟   ·   Shift 快走   ·   滚轮缩放", Position = new(20,44) });
        var row = new HBoxContainer { Position = new(620,19) }; root.AddChild(row);
        AddButton(row,"演示动作",()=> { ResetActor(); _demo=true; });
        AddButton(row,"恢复位置",ResetActor);
        AddButton(row,"远近视角",()=> _camera.Zoom=Vector2.One*(_camera.Zoom.X<1.7f ? 2f : 1.35f));
        root.AddChild(new ColorRect { Position = new(0,570), Size = new(960,30), Color = new Color("202923e8"), MouseFilter = Control.MouseFilterEnum.Ignore });
        _status = new Label { Position = new(20,575) }; root.AddChild(_status);
        root.AddChild(new Label { Position = new(570,575), Text = "沿街走动，查看人物与屋舍、树木的比例和遮挡" });
    }

    private static void AddButton(HBoxContainer row, string text, Action action)
    {
        var button = new Button { Text=text, CustomMinimumSize=new(98,34), FocusMode=Control.FocusModeEnum.None };
        button.Pressed+=action; row.AddChild(button);
    }

    private void ResetActor()
    {
        _actor.Position=Start; _facing=Vector2.Right; _demo=false; _demoClock=0; _clip="Idle"; _clock=0;
        RefreshFrame();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (_capture) return;
        if (input is InputEventMouseButton { Pressed:true } mouse && mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            _camera.Zoom=Vector2.One*Mathf.Clamp(_camera.Zoom.X+(mouse.ButtonIndex==MouseButton.WheelUp ? .15f : -.15f),1f,2.4f);
            GetViewport().SetInputAsHandled();
        }
        if (input is InputEventKey { Pressed:true,Echo:false } key && (key.PhysicalKeycode==Key.J || key.Keycode==Key.J))
        { _demo=false; Attack(); GetViewport().SetInputAsHandled(); }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_capture) return;
        Vector2 input=Input.GetVector("move_left","move_right","move_up","move_down");
        if (input.LengthSquared()>.001f) _demo=false;
        if (_demo)
        {
            double old=_demoClock; _demoClock+=delta;
            input=DemoDirection(_demoClock);
            if(old<6 && _demoClock>=6) Attack();
            if(_demoClock>=8) _demo=false;
        }
        Step(input,delta,Input.IsPhysicalKeyPressed(Key.Shift) ? 2 : 1);
    }

    private void Attack()
    {
        if (_clip=="Attack") return;
        _clip="Attack"; _clock=0;
    }

    private void Step(Vector2 input, double delta, float speed=1)
    {
        if (_clip=="Attack")
        {
            _clock+=delta;
            if (_clock>=_lengths["Attack"]) { _clip="Idle"; _clock=0; }
            RefreshFrame(); return;
        }
        Vector2 before=_actor.Position;
        // 行走图集保留22°俯视投影；纵向位移相应压缩，避免背向时脚步在地上滑行。
        float depth=Mathf.Sin(Mathf.DegToRad(Elevation));
        Vector2 motion=new Vector2(input.X,input.Y*depth)*(WalkSpeed*speed*(float)delta);
        if (motion.LengthSquared()>.000001f)
        {
            var collision=_actor.MoveAndCollide(motion);
            if (collision!=null) _actor.MoveAndCollide(collision.GetRemainder().Slide(collision.GetNormal()));
        }
        Vector2 moved=_actor.Position-before;
        if (moved.LengthSquared()>.000001f)
        {
            _facing=Mathf.Abs(input.X)>Mathf.Abs(input.Y) ? new(Mathf.Sign(input.X),0) : new(0,Mathf.Sign(input.Y));
            if (_clip!="Walk") { _clip="Walk"; _clock=0; }
            // 原步态每周期沿地面前进0.3模型单位；只累计碰撞后的真实路程。
            float distance=new Vector2(moved.X,moved.Y/depth).Length();
            _clock=(_clock+distance/(.3f*Cell/OrthoSize*ArtScale)*_lengths["Walk"])%_lengths["Walk"];
        }
        else
        {
            if (_clip!="Idle") { _clip="Idle"; _clock=0; }
            _clock=(_clock+delta)%_lengths["Idle"];
        }
        RefreshFrame();
    }

    private void RefreshFrame()
    {
        int row=_facing.Y>.5f ? 0 : _facing.X<-.5f ? 1 : _facing.Y<-.5f ? 2 : 3;
        int frame=Math.Clamp((int)(_clock/_lengths[_clip]*_counts[_clip]),0,_counts[_clip]-1);
        _sprite.Texture=_atlases[_clip]; _sprite.RegionRect=new Rect2(frame*Cell,row*Cell,Cell,Cell);
        float yaw=Mathf.Atan2(_facing.X,_facing.Y)-Mathf.Atan2(.85f,.527f);
        Vector3 foot=ModelFoot.Rotated(Vector3.Up,yaw);
        float angle=Mathf.Atan2(5*Mathf.Sin(Mathf.DegToRad(Elevation))+.03f,5*Mathf.Cos(Mathf.DegToRad(Elevation)));
        Vector2 anchor=new(Cell*.5f+foot.X*Cell/OrthoSize,
            Cell*.5f-((foot.Y+.03f)*Mathf.Cos(angle)-foot.Z*Mathf.Sin(angle))*Cell/OrthoSize);
        _sprite.Offset=-anchor;
        _camera.Position=_actor.Position+new Vector2(0,-45);
        _status.Text=(_demo ? "动作演示 · " : "")+(_clip=="Attack" ? "挥戟" : _clip=="Walk" ? "行走" : "待机");
    }

    private static Vector2 DemoDirection(double time) => time<1 ? Vector2.Zero : time<3 ? Vector2.Right
        : time<4 ? Vector2.Down : time<5 ? Vector2.Left : time<6 ? Vector2.Up : Vector2.Zero;

    private async Task RenderFrame()
    {
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
    }

    private async void Capture()
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Output+"/Frames"));
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            // 实际撞向军营墙基，确认停墙后停止行走采样；此检查复用正常游玩的移动入口。
            _actor.Position=new(465,230);
            for(int i=0;i<120;i++) Step(Vector2.Up,1/30.0,4);
            bool blocked=_actor.Position.Y>=209.8f && _actor.Position.Y<=210.2f && _clip=="Idle";
            if(!blocked) throw new InvalidOperationException("建筑阻挡或停步检查失败："+_actor.Position+" "+_clip);
            ResetActor();
            for(int i=0;i<240;i++)
            {
                if(i==180) Attack();
                Step(DemoDirection(i/30.0),1/30.0);
                await RenderFrame(); await RenderFrame();
                using var image=GetViewport().GetTexture().GetImage();
                image.SavePng($"{Output}/Frames/Frame{i:D3}.png");
                if(i==15) image.SavePng(Output+"/CityIdle.png");
                if(i==165) image.SavePng(Output+"/CityBack.png");
                if(i==200) image.SavePng(Output+"/CityAttack.png");
            }
            var report=new { rendered=DisplayServer.GetName()!="headless", map="CityMap", frames=240, seconds=8,
                atlasDirections=4, atlasOnly=true, mapYSort=_map.Actors.YSortEnabled, wallCollision=blocked,
                stoppedAtWall=true, cell=Cell, scale=ArtScale, source="LvBuHunyuanRig", issues=Array.Empty<string>() };
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(Output+"/CityAudit.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("LVBU_CITY_CAPTURE_COMPLETE "+JsonSerializer.Serialize(report)); GetTree().Quit();
        }
        catch(Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
