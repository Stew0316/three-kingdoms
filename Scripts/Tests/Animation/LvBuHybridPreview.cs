using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>混元三维骨骼的独立二维试验台；二维脚点与输入独立于三维渲染，不改变正式战斗。</summary>
public partial class LvBuHybridPreview : Node2D
{
    private const string Asset = "res://Source/People/LvBu/HunyuanV1/LvBuHunyuanRig.glb";
    private const string Output = "res://Build/LvBuHybridV1";
    private SubViewport _view;
    private Node3D _turn;
    private Camera3D _camera;
    private AnimationPlayer _player;
    private Skeleton3D _skeleton;
    private Sprite2D _sprite;
    private TextureRect _large;
    private Label _status;
    private Node2D _boneOverlay;
    private Vector2 _foot = new(993, 476);
    private Vector2 _direction = new Vector2(1, .62f).Normalized();
    private bool _walk, _pause, _bones, _capture, _attack;
    private float _speed = 1, _angle = 22;
    private readonly Dictionary<string, string> _clips = new();
    private readonly Dictionary<string, Texture2D> _atlases = new();
    private readonly Dictionary<string, int> _frameCounts = new() { ["Idle"] = 24, ["Walk"] = 24, ["Attack"] = 28 };
    private bool _baked;
    private readonly Vector3 _rootPoint = new(-.11f, -.998f, .04f);
    private readonly Vector2 _largePosition = new(12, 108);
    private const float SpriteScale = .30f;

    public override void _Ready()
    {
        GetWindow().ContentScaleSize = new(1280, 850);
        GetWindow().Size = new(1280, 850);
        GetWindow().Title = "吕布 · 三维骨骼转二维预览";
        RenderingServer.SetDefaultClearColor(new Color("181e22"));
        _view = new SubViewport { Size = new(640, 640), TransparentBg = true, OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always, Msaa3D = Viewport.Msaa.Msaa4X, MeshLodThreshold = 0 };
        AddChild(_view);
        var env = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0, 0, 0, 0), AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("b9c8d2"), AmbientLightEnergy = .7f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled,
            TonemapMode = Godot.Environment.ToneMapper.Filmic };
        _view.AddChild(new WorldEnvironment { Environment = env });
        _view.AddChild(new DirectionalLight3D { RotationDegrees = new(-35, -35, 0), LightEnergy = 1.3f, LightColor = new Color("ffe9ce") });
        _view.AddChild(new DirectionalLight3D { RotationDegrees = new(-20, 145, 0), LightEnergy = .55f, LightColor = new Color("a3bbd4") });
        _turn = new Node3D(); _view.AddChild(_turn);
        var model = GD.Load<PackedScene>(Asset).Instantiate<Node3D>(); _turn.AddChild(model);
        _player = Descendants(model).OfType<AnimationPlayer>().FirstOrDefault();
        _skeleton = Descendants(model).OfType<Skeleton3D>().FirstOrDefault();
        if (_player == null || _skeleton == null) throw new InvalidOperationException("模型缺少骨骼或动画播放器。");
        foreach (string logical in new[] { "Idle", "Walk", "Attack" })
        {
            string found = _player.GetAnimationList().FirstOrDefault(n => n.ToString().EndsWith(logical, StringComparison.OrdinalIgnoreCase));
            if (found == null) throw new InvalidOperationException("缺少动画：" + logical);
            _clips[logical] = found;
            _player.GetAnimation(found).LoopMode = logical == "Attack" ? Animation.LoopModeEnum.None : Animation.LoopModeEnum.Linear;
        }
        _player.AnimationFinished += _ => { _attack = false; Play(_walk ? "Walk" : "Idle"); };
        _camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 2.55f, Current = true };
        _view.AddChild(_camera); UpdateCamera();
        _large = new TextureRect { Position = _largePosition, Size = new(640, 640), Texture = _view.GetTexture(),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(_large);
        _sprite = new Sprite2D { Texture = _view.GetTexture(), Centered = false, Scale = Vector2.One * SpriteScale,
            TextureFilter = TextureFilterEnum.Linear };
        AddChild(_sprite);
        _boneOverlay = new Node2D(); AddChild(_boneOverlay); _boneOverlay.Draw += DrawBones;
        Label("吕布 · 三维骨骼 → 二维画面", new(28, 19), 28);
        Label("黑金甲 · 暗红披风 / 同一套骨骼切换观察方向", new(30, 60), 16, "a5b4bc");
        Label("放大检查", new(35, 108), 18);
        Label("二维游戏尺寸", new(725, 138), 21);
        Label("WASD / 方向键移动，J 挥戟", new(725, 174), 16, "b3beb9");
        Label("同一个模型转向，无需重新生成四张立绘", new(725, 565), 15, "a5b4bc");
        Label("参考图片", new(755, 632), 16, "a5b4bc");
        var reference = new TextureRect { Position = new(752, 650), Size = new(115, 115),
            Texture = GD.Load<Texture2D>("res://Source/People/Portraits/LvBu/LvBuRightFacingV1.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(reference);
        Label("原图投射上色\n侧背补色 · 骨骼动作试验", new(885, 667), 17, "b8b9ab");
        _status = Label("", new(725, 604), 16, "d5bd86");
        var actions = new HBoxContainer { Position = new(28, 750) }; AddChild(actions);
        Button(actions, "待机", () => { _walk = false; _attack = false; Play("Idle"); });
        Button(actions, "行走", () => { _walk = true; _attack = false; Play("Walk"); });
        Button(actions, "挥戟", Attack);
        Button(actions, "骨骼", () => _bones = !_bones);
        Button(actions, "慢放", () => _speed = _speed < .9f ? 1 : .35f);
        Button(actions, "暂停", () => _pause = !_pause);
        Button(actions, "俯视角", () => { _baked = false; _angle = _angle > 30 ? 12 : _angle + 10; UpdateCamera(); });
        Button(actions, "二维帧图", () => {
            if (_atlases.Count != 3) return;
            _baked = !_baked;
            if (_baked) { _angle=22; UpdateCamera(); _direction=Cardinal(_direction); }
        });
        foreach(string clip in _clips.Keys)
        {
            string path=$"res://Source/People/LvBu/HunyuanV1/LvBu{clip}FourDirections.png";
            if (ResourceLoader.Exists(path)) _atlases[clip]=GD.Load<Texture2D>(path);
        }
        var directions = new HBoxContainer { Position = new(28, 800) }; AddChild(directions);
        foreach (var (text, direction) in new[] { ("右", Vector2.Right), ("右下", new Vector2(1,1)), ("下", Vector2.Down),
            ("左下", new Vector2(-1,1)), ("左", Vector2.Left), ("左上", new Vector2(-1,-1)), ("上", Vector2.Up), ("右上", new Vector2(1,-1)) })
        {
            Vector2 selected = direction.Normalized(); Button(directions, text, () => _direction = _baked ? Cardinal(selected) : selected);
        }
        Play("Idle"); SyncProjection();
        if (OS.GetCmdlineUserArgs().Contains("--capture-lvbu-hybrid")) CallDeferred(MethodName.Capture);
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        yield return node;
        foreach (Node child in node.GetChildren()) foreach (Node next in Descendants(child)) yield return next;
    }
    private Label Label(string text, Vector2 pos, int size, string color = "e4e8e5")
    {
        var label = new Label { Text = text, Position = pos, Theme = UiStyles.ChineseTheme(size), MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeColorOverride("font_color", new Color(color)); AddChild(label); return label;
    }
    private static void Button(HBoxContainer row, string text, Action action)
    {
        var button = new Button { Text = text, Theme = UiStyles.ChineseTheme(17), CustomMinimumSize = new(76, 35) };
        button.Pressed += action; row.AddChild(button);
    }
    private void Play(string name) => _player.Play(_clips[name], .15);
    private void Attack() { _attack = true; _walk = false; _player.Stop(); Play("Attack"); }
    private void UpdateCamera()
    {
        float radians = Mathf.DegToRad(_angle);
        _camera.Position = new(0, 5 * Mathf.Sin(radians), 5 * Mathf.Cos(radians));
        _camera.LookAt(new Vector3(0, -.03f, 0), Vector3.Up);
    }
    public override void _Process(double delta)
    {
        if (_capture || _player == null) return;
        _player.Active = !_pause; _player.SpeedScale = _speed;
        if (!_pause)
        {
            Vector2 input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
            if (Input.IsActionJustPressed("attack")) Attack();
            if (input.LengthSquared() > .01f && !_attack)
            {
                _direction = _baked ? Cardinal(input) : input.Normalized();
                if (!_player.CurrentAnimation.ToString().EndsWith("Walk")) Play("Walk");
                MoveFoot(_direction * (float)delta);
            }
            else if (_walk && !_attack)
            {
                MoveFoot(_direction * (float)delta);
                if (_foot.X < 785 || _foot.X > 1195 || _foot.Y < 345 || _foot.Y > 515) _direction = -_direction;
            }
            else if (!_attack && !_player.CurrentAnimation.ToString().EndsWith("Idle")) Play("Idle");
        }
        SyncProjection();
        _status.Text = $"{(_attack ? "挥戟" : _walk || Input.GetVector("move_left","move_right","move_up","move_down").LengthSquared() > .01f ? "行走" : "待机")}  ·  {(_pause ? "暂停" : _speed < .9 ? "慢放" : "正常速度")}  ·  {(_baked ? "二维帧图" : "实时投影")}";
        _boneOverlay.QueueRedraw(); QueueRedraw();
    }
    private void MoveFoot(Vector2 delta)
    {
        // 步态承重段速度 .25 米/秒，与正交视口的像素密度对应。
        float pixelsPerMeter = 640 / _camera.Size * SpriteScale;
        Vector2 projected = new(delta.X, delta.Y * Mathf.Sin(Mathf.DegToRad(_angle)));
        _foot += projected * (.25f * pixelsPerMeter * _speed);
        _foot = _foot.Clamp(new Vector2(775, 335), new Vector2(1205, 525));
    }
    private void SyncProjection()
    {
        float modelHeading = Mathf.Atan2(.85f, .527f);
        _turn.Rotation = new(0, Mathf.Atan2(_direction.X, _direction.Y) - modelHeading, 0);
        // 脚点固定在二维世界；俯视角改变只影响画面投影，不使角色漂移。
        Vector2 anchor = _camera.UnprojectPosition(_turn.ToGlobal(_rootPoint));
        if (_baked)
        {
            string clip = _clips.FirstOrDefault(x => x.Value == _player.CurrentAnimation).Key ?? "Idle";
            int count = _frameCounts[clip]; double length = _player.GetAnimation(_clips[clip]).Length;
            int frame = Math.Clamp((int)(_player.CurrentAnimationPosition / length * count),0,count-1);
            int row = _direction.Y > .5 ? 0 : _direction.X < -.5 ? 1 : _direction.Y < -.5 ? 2 : 3;
            _sprite.Texture = _atlases[clip]; _sprite.RegionEnabled=true; _sprite.RegionRect=new Rect2(frame*192,row*192,192,192);
            _sprite.Scale=Vector2.One; _sprite.Offset=-anchor*SpriteScale;
        }
        else
        {
            _sprite.Texture = _view.GetTexture(); _sprite.RegionEnabled=false;
            _sprite.Scale=Vector2.One*SpriteScale; _sprite.Offset=-anchor;
        }
        _sprite.Position = _foot;
    }
    private static Vector2 Cardinal(Vector2 direction) => Mathf.Abs(direction.X)>Mathf.Abs(direction.Y)
        ? new Vector2(Mathf.Sign(direction.X),0) : new Vector2(0,Mathf.Sign(direction.Y));
    public override void _Draw()
    {
        DrawStyleBox(new StyleBoxFlat { BgColor = new Color("222b30"), BorderColor = new Color("38444a"),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12 }, new Rect2(18, 101, 650, 632));
        DrawRect(new Rect2(710, 205, 540, 345), new Color("44483e"));
        for (int y=225;y<550;y+=32) DrawLine(new(710,y),new(1250,y),new Color("505349"),1);
        for (int x=715;x<1250;x+=48) DrawLine(new(x,205),new(x,550),new Color("505349"),1);
        DrawLine(new(710,549),new(1250,549),new Color("78806b"),2);
        DrawSetTransform(_foot, 0, new Vector2(1,.27f)); DrawCircle(Vector2.Zero, 21, new Color(0,0,0,.25f)); DrawSetTransform(Vector2.Zero);
    }
    private void DrawBones()
    {
        if (!_bones || _skeleton == null) return;
        for (int i=0;i<_skeleton.GetBoneCount();i++)
        {
            Vector3 pos = _skeleton.ToGlobal(_skeleton.GetBoneGlobalPose(i).Origin);
            Vector2 p = _largePosition + _camera.UnprojectPosition(pos);
            int parent = _skeleton.GetBoneParent(i);
            if (parent >= 0)
            {
                Vector3 prev = _skeleton.ToGlobal(_skeleton.GetBoneGlobalPose(parent).Origin);
                _boneOverlay.DrawLine(_largePosition + _camera.UnprojectPosition(prev), p, new Color("73dfc7"), 1.5f);
            }
            _boneOverlay.DrawCircle(p, 2.5f, new Color("fff1cb"));
        }
    }
    private async Task RenderFrame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
    }
    private async void Capture()
    {
        try
        {
            _capture = true; _player.Active = true; _player.SpeedScale = 1;
            _player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Output+"/Frames"));
            var report = new Dictionary<string,object> { ["bones"] = _skeleton.GetBoneCount(), ["animations"] = _clips,
                ["rendered"] = DisplayServer.GetName() != "headless", ["sourceTriangles"] = 1493928, ["textureProjection"] = true };
            int frame = 0;
            foreach (string clip in new[] { "Idle", "Walk", "Attack" })
            {
                _player.Stop(); _player.Play(_clips[clip], 0); _player.Advance(0);
                double length = _player.GetAnimation(_clips[clip]).Length;
                int count = clip == "Idle" ? 24 : (int)Math.Round(length * 30);
                for (int i=0;i<count;i++)
                {
                    _player.Seek(i / 30.0, true); _player.Advance(0); SyncProjection();
                    _status.Text = clip == "Idle" ? "待机 · 呼吸" : clip == "Walk" ? "行走 · 接地骨骼动作" : "挥戟 · 蓄力与收势";
                    _boneOverlay.QueueRedraw(); QueueRedraw(); await RenderFrame(); await RenderFrame();
                    if (clip == "Walk") MoveFoot(_direction/30);
                    using var image = GetViewport().GetTexture().GetImage();
                    image.SavePng($"{Output}/Frames/Frame{frame++:D3}.png");
                    if (i == (clip == "Attack" ? 19 : 8)) image.SavePng($"{Output}/{clip}Godot.png");
                }
            }
            // 四方向行走帧从 Godot 实际三维渲染导出，用于检查真正二维帧图的可读性。
            foreach(string clip in new[]{"Idle","Walk","Attack"})
            {
                int count=_frameCounts[clip]; double length=_player.GetAnimation(_clips[clip]).Length;
                using var atlas = Image.CreateEmpty(192*count,192*4,false,Image.Format.Rgba8); atlas.Fill(Colors.Transparent);
                int row=0;
                foreach (Vector2 direction in new[] { Vector2.Down, Vector2.Left, Vector2.Up, Vector2.Right })
                {
                    _direction=direction; _player.Stop(); _player.Play(_clips[clip],0); _player.Advance(0);
                    for(int i=0;i<count;i++)
                    {
                        _player.Seek(i*length/count,true); _player.Advance(0); SyncProjection(); await RenderFrame(); await RenderFrame();
                        using var image=_view.GetTexture().GetImage(); image.Convert(Image.Format.Rgba8);
                        image.Resize(192,192,Image.Interpolation.Lanczos); atlas.BlitRect(image,new Rect2I(0,0,192,192),new Vector2I(i*192,row*192));
                    }
                    if(clip=="Idle") using(var full=GetViewport().GetTexture().GetImage()) full.SavePng($"{Output}/Direction{row}.png");
                    row++;
                }
                atlas.SavePng($"res://Source/People/LvBu/HunyuanV1/LvBu{clip}FourDirections.png");
                _atlases[clip]=ImageTexture.CreateFromImage(atlas);
            }
            _baked=true; _direction=Vector2.Right; _player.Stop();_player.Play(_clips["Walk"],0);_player.Advance(0);_player.Seek(.35,true);
            SyncProjection();_status.Text="二维帧图 · 四方向行走";await RenderFrame();await RenderFrame();
            using(var proof=GetViewport().GetTexture().GetImage()) proof.SavePng(Output+"/BakedGodot.png");
            report["atlases"] = "待机24帧 / 行走24帧 / 挥戟28帧 × 4方向；每格192×192；顺序下、左、上、右";
            report["bakedPlayback"] = true;
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(Output+"/GodotAudit.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("HYBRID_CAPTURE_COMPLETE "+JsonSerializer.Serialize(report));
            GetTree().Quit();
        }
        catch(Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }
}
