using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>吕布背面动作制作台：展示正式CharacterRig的实际绘制层，交互与录制共用真实行动入口。</summary>
public partial class LvBuBackPreview : Node2D
{
    private const string Output = "res://Build/LvBuBackV1";
    private Combatant _actor;
    private CharacterRig Rig => _actor.Rig;
    private Node2D _large, _actual, _overlay;
    private Label _state;
    private readonly List<(Node2D Source, Node2D Large, Node2D Actual)> _layers = new();
    private bool _bones, _walking, _paused;
    private float _largestGripError, _supportGripError, _anchorError, _minElbow = 180, _maxElbow;
    private float _maxSlip, _maxGroundError;
    private readonly Vector2[] _previousContact = new Vector2[2];
    private readonly bool[] _previousSupport = new bool[2];
    private int _contactSamples, _attackSamples, _samples;
    private string _worstGrip = "";
    private string _worstSecondary = "", _tightestElbow = "", _straightestElbow = "";
    private bool Rendered => DisplayServer.GetName() != "headless";

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        ProcessPriority = 100; // 在角色与骨架更新后同步预览，避免录到上一帧的关节。
        GetWindow().ContentScaleSize = new Vector2I(1100, 760);
        GetWindow().Size = new Vector2I(1100, 760);
        RenderingServer.SetDefaultClearColor(new Color("202527"));
        _actor = GD.Load<PackedScene>("res://Components/Characters/LvBuBackV1.tscn").Instantiate<Combatant>();
        _actor.ProcessMode = ProcessModeEnum.Pausable;
        _actor.GetNode(NodeNames.Controller).ProcessMode = ProcessModeEnum.Disabled;
        _actor.Visible = false;
        AddChild(_actor);
        _actor.Face(Vector2.Up);
        _actor.GetNode<GroundEffects>(NodeNames.GroundEffects).Hide();
        _large = new Node2D { Position = new Vector2(460, 630), Scale = new Vector2(3.8f, 3.648f) };
        _actual = new Node2D { Position = new Vector2(900, 590), Scale = new Vector2(1.6f, 1.536f) };
        AddChild(_large); AddChild(_actual);
        foreach (Node2D source in Rig.ArtworkRoot.GetChildren())
        {
            var large = RigArtworkCapture.CopyLayer(source);
            var actual = RigArtworkCapture.CopyLayer(source);
            _large.AddChild(large); _actual.AddChild(actual);
            _layers.Add((source, large, actual));
        }
        _overlay = new Node2D(); _large.AddChild(_overlay); _overlay.Draw += DrawBones;
        AddLabel("吕布 · 背面动作", new Vector2(30, 20), 30);
        AddLabel("像素彩绘 / 黑金甲 / 双冠翎", new Vector2(31, 61), 16);
        AddLabel("放大检查", new Vector2(410, 663), 17);
        AddLabel("游戏缩放参考", new Vector2(840, 620), 17);
        _state = AddLabel("待机", new Vector2(31, 113), 21);
        var row = new HBoxContainer { Position = new Vector2(30, 707) };
        AddChild(row);
        AddButton(row, "待机", () => { _walking=false; _actor.SetMoveInput(Vector2.Zero); });
        AddButton(row, "行走", () => { _walking=true; _actor.SetMoveInput(Vector2.Up); });
        AddButton(row, "挥戟", () => Act(CombatAction.Basic));
        AddButton(row, "横扫", () => Act(CombatAction.Sweep));
        AddButton(row, "突刺", () => Act(CombatAction.Dash));
        AddButton(row, "闪避", () => Act(CombatAction.Dodge));
        AddButton(row, "骨骼", () => _bones=!_bones);
        AddButton(row, "慢放", () => Engine.TimeScale=Engine.TimeScale < .9 ? 1 : .35);
        AddButton(row, "暂停", () => { _paused=!_paused; GetTree().Paused=_paused; });
        AddButton(row, "换底色", () => { _light=!_light; RenderingServer.SetDefaultClearColor(new Color(_light ? "d4d1c9" : "202527")); });
        if (OS.GetCmdlineUserArgs().Contains("--capture-lvbu-back") || OS.GetCmdlineUserArgs().Contains("--test-lvbu-back"))
            CallDeferred(MethodName.CaptureAndCheck);
    }

    private bool _light;
    private Label AddLabel(string text, Vector2 position, int size)
    {
        var label = new Label { Text=text, Position=position, Theme=UiStyles.ChineseTheme(size), MouseFilter=Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size); AddChild(label); return label;
    }
    private static void AddButton(HBoxContainer row, string text, Action action)
    {
        var button = new Button { Text=text, Theme=UiStyles.ChineseTheme(17), CustomMinimumSize=new Vector2(88, 36) };
        button.Pressed += action; row.AddChild(button);
    }
    private void Act(CombatAction action)
    {
        _walking=false; _actor.SetMoveInput(Vector2.Zero); _actor.TryAction(action);
    }

    public override void _Process(double delta)
    {
        if (_actor == null) return;
        SyncPreview();
        _state.Text = _actor.CurrentState == Combatant.State.Hurt ? "受击 · 缓冲"
            : _actor.CounterRemaining > 0 ? "反击 · 回架"
            : _actor.CurrentState == Combatant.State.Attack ? _actor.GetSkill(_actor.CurrentAction).DisplayName + " · " + _actor.Phase
            : _actor.CurrentState == Combatant.State.Dodge ? "闪避 · 收势" : _walking ? "背面行走" : "待机 · 呼吸";
        _overlay.QueueRedraw(); QueueRedraw();
    }

    private void SyncPreview()
    {
        foreach (var (source, large, actual) in _layers)
        {
            var transform = Rig.Skeleton.GlobalTransform.AffineInverse() * source.GlobalTransform;
            RigArtworkCapture.SyncLayer(source, large, transform);
            RigArtworkCapture.SyncLayer(source, actual, transform);
        }
        // 收戟到头后时按运行时的实际遮挡顺序复制，不能用制作台初始顺序盖住脸。
        foreach (Node2D source in Rig.ArtworkRoot.GetChildren())
            foreach (var pair in _layers)
                if (pair.Source == source)
                {
                    _large.MoveChild(pair.Large, _large.GetChildCount()-1);
                    _actual.MoveChild(pair.Actual, _actual.GetChildCount()-1);
                }
        _large.MoveChild(_overlay, _large.GetChildCount()-1);
    }

    public override void _Draw()
    {
        // 横线按真实纵向位移滚动，用于观察背面承重脚是否锁在地面上。
        float offset = Mathf.PosMod(_actor == null ? 0 : -_actor.Position.Y / Mathf.Max(.001f, Rig.Scale.Y) * 3.648f, 48);
        for(float y=530+offset; y<660; y+=48)
            DrawLine(new Vector2(300,y),new Vector2(650,y),new Color("343d40"),1);
    }

    private void DrawBones()
    {
        if (!_bones) return;
        int[] parents = CharacterRigDefaults.CreateParents(); parents[14]=-1;
        for (int bone=0; bone<Rig.Joints.Count; bone++)
        {
            Vector2 point=Rig.Skeleton.ToLocal(Rig.Joints[bone].GlobalPosition);
            if(parents[bone]>=0) _overlay.DrawLine(point,Rig.Skeleton.ToLocal(Rig.Joints[parents[bone]].GlobalPosition),new Color("69dcc2"),.45f);
            _overlay.DrawCircle(point,1,new Color("f6f1d8"));
        }
    }

    private async Task Tick()
    {
        await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        if(Rendered) await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        else { Rig._Process(0); SyncPreview(); }
    }

    /// <summary>连续记录实际待机、移动、三种攻击与闪避，并量测源图关节和世界承重脚。</summary>
    private async void CaptureAndCheck()
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Output+"/Frames"));
            bool capture = Rendered && OS.GetCmdlineUserArgs().Contains("--capture-lvbu-back");
            int limit = OS.GetCmdlineUserArgs().Contains("--idle-only") ? 40 : 800;
            for(int frame=0;frame<limit;frame++)
            {
                if(frame==60) { _walking=true; _actor.SetMoveInput(Vector2.Up); }
                if(frame==210) { _walking=false; _actor.SetMoveInput(Vector2.Zero); }
                if(frame==260) Act(CombatAction.Basic);
                if(frame==330) Act(CombatAction.Sweep);
                if(frame==420) Act(CombatAction.Dash);
                if(frame==510) Act(CombatAction.Dodge);
                if(frame==570) _actor.PlayCounterSpin(68,.48f);
                if(frame==660) _actor.ReceiveDamage(new DamageInfo(_actor,1,Vector2.Left));
                if(frame==700) { _walking=true; _actor.SetMoveInput(Vector2.Up); }
                if(frame==740) Act(CombatAction.Basic);
                await Tick();
                InspectFrame(frame);
                if(capture && frame%2==0)
                {
                    // 截图等待不推进动作时钟，保持每两物理帧一张、30fps连续预览。
                    GetTree().Paused=true;
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    using var image=GetViewport().GetTexture().GetImage();
                    image.SavePng($"{Output}/Frames/Frame{frame/2:D3}.png");
                    if(frame==30) image.SavePng(Output+"/Idle.png");
                    if(frame==122) image.SavePng(Output+"/Walk.png");
                    if(frame==272) image.SavePng(Output+"/Attack.png");
                    if(frame==350) image.SavePng(Output+"/Sweep.png");
                    GetTree().Paused=false;
                }
            }
            float phase=Rig.WalkPhase, clock=_actor.VisualTime;
            float clothClock=Rig.ClothTime;
            Transform2D pausedWeapon=Rig.WeaponPart.GlobalTransform;
            GetTree().Paused=true;
            await Tick(); await Tick();
            if(Rig.WalkPhase!=phase || _actor.VisualTime!=clock || Rig.ClothTime!=clothClock
                || !pausedWeapon.IsEqualApprox(Rig.WeaponPart.GlobalTransform)) throw new InvalidOperationException("暂停后动作仍在推进");
            GetTree().Paused=false;
            if(capture)
            {
                RenderingServer.SetDefaultClearColor(new Color("d4d1c9"));
                GetTree().Paused=true;
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using(var light=GetViewport().GetTexture().GetImage()) light.SavePng(Output+"/IdleLight.png");
                GetTree().Paused=false;
                RenderingServer.SetDefaultClearColor(new Color("202527"));
            }
            bool wallPassed=limit<=40 || await CheckWall();
            if(Rendered && limit>40) await CheckIdleWind(capture);
            var result = new { Samples=_samples, PrimaryGripError=_largestGripError, SecondaryGripError=_supportGripError,
                ArtworkAnchorError=_anchorError, MinElbowDegrees=_minElbow, MaxElbowDegrees=_maxElbow,
                MaxSupportSlipPerFrame=_maxSlip, MaxGroundError=_maxGroundError, ContactSamples=_contactSamples,
                AttackSamples=_attackSamples, WorstGrip=_worstGrip, WorstSecondary=_worstSecondary,
                TightestElbow=_tightestElbow, StraightestElbow=_straightestElbow, PausePassed=true, WallPassed=wallPassed, Rendered };
            string json=JsonSerializer.Serialize(result,new JsonSerializerOptions { WriteIndented=true });
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(Output+"/MotionAudit.json"),json);
            GD.Print(json);
            if(_largestGripError>.1f || _supportGripError>.1f || _anchorError>.05f)
                throw new InvalidOperationException("握点或原图关节误差超限，详见MotionAudit.json");
            if(_minElbow<18 || _maxElbow>148)
                throw new InvalidOperationException("手肘进入近乎锁直或过度折叠的范围");
            if(limit>40 && (_maxSlip>.12f || _maxGroundError>.1f || _contactSamples<20 || _attackSamples<10))
                throw new InvalidOperationException("接地、承重脚滑动或动作覆盖不满足检查");
            GD.Print("吕布背面动作验证通过。"); GetTree().Quit();
        }
        catch(Exception error) { GD.PushError(error.ToString()); GetTree().Paused=false; GetTree().Quit(1); }
    }

    private async Task<bool> CheckWall()
    {
        var wall=new StaticBody2D { Position=_actor.Position+new Vector2(0,-50), CollisionLayer=1 };
        wall.AddChild(new CollisionShape2D { Shape=new RectangleShape2D { Size=new Vector2(1000,20) } });
        AddChild(wall); _actor.SetMoveInput(Vector2.Up);
        for(int i=0;i<75;i++) await Tick();
        float distance=_actor.WalkDistance, phase=Rig.WalkPhase;
        for(int i=0;i<15;i++) await Tick();
        bool passed=_actor.WalkVelocity.Length()<1 && Mathf.Abs(distance-_actor.WalkDistance)<.001f
            && Mathf.Abs(phase-Rig.WalkPhase)<.0001f && Rig.ClothWeight<.01f;
        wall.QueueFree(); _actor.SetMoveInput(Vector2.Zero);
        if(!passed) throw new InvalidOperationException("顶墙后仍推进步态或移动引起的布料摆动");
        return true;
    }

    private void InspectFrame(int frame)
    {
        _samples++;
        float primary=Rig.Skeleton.ToLocal(Rig.Joints[5].GlobalPosition).DistanceTo(Rig.ReferencePrimaryGrip);
        if(primary>_largestGripError) { _largestGripError=primary; _worstGrip=$"帧{frame} {_actor.CurrentState}/{_actor.CurrentMotion} t={_actor.ActionTime:F3}"; }
        if(Rig.ReferenceGripWeight>.9999f)
        {
            float secondary=Rig.Skeleton.ToLocal(Rig.Joints[8].GlobalPosition).DistanceTo(Rig.ReferenceSecondaryGrip);
            if(secondary>_supportGripError) { _supportGripError=secondary; _worstSecondary=$"帧{frame} {_actor.CurrentState}/{_actor.CurrentMotion} t={_actor.ActionTime:F3}"; }
        }
        if(_actor.IsAttackActive) _attackSamples++;
        foreach(int upper in new[] {3,6})
        {
            float bend=Rig.DepthElbowDegrees(upper);
            if(bend < 18 || bend > 148)
                GD.Print($"肘诊断 帧{frame} {upper} 角{bend:F1} 姿态{_actor.CurrentMotion}/{_actor.ActionTime:F3} 肩{Rig.Skeleton.ToLocal(Rig.Joints[upper].GlobalPosition)} 手{Rig.Skeleton.ToLocal(Rig.Joints[upper+2].GlobalPosition)} 主握{Rig.ReferencePrimaryGrip} 副握{Rig.ReferenceSecondaryGrip} 权重{Rig.ReferenceGripWeight:F3}");
            string context=$"帧{frame} {_actor.CurrentState}/{_actor.CurrentMotion} t={_actor.ActionTime:F3} 臂{upper}";
            if(bend<_minElbow) { _minElbow=bend; _straightestElbow=context; }
            if(bend>_maxElbow) { _maxElbow=bend; _tightestElbow=context; }
            Vector2 Pin(int bone,int end)
            {
                var part=Rig.Parts[bone]; var atlas=(AtlasTexture)part.Texture;
                return Rig.Skeleton.ToLocal(part.ToGlobal(_actor.Presentation.Model.ArmAtlasAnchors[(bone-3)*2+end]-atlas.Region.Position));
            }
            _anchorError=Mathf.Max(_anchorError,Pin(upper,1).DistanceTo(Pin(upper+1,0)));
            _anchorError=Mathf.Max(_anchorError,Pin(upper+1,1).DistanceTo(Pin(upper+2,0)));
            if(!Rig.Joints[upper+1].Scale.IsEqualApprox(Vector2.One)) throw new InvalidOperationException("手臂骨长被缩放");
        }
        for(int leg=0;leg<2;leg++)
        {
            var foot=Rig.DrawOrderedParts.First(p=>p.Name==(leg==0 ? "远靴掌图层" : "近靴掌图层"));
            float phase=Mathf.PosMod(Rig.WalkPhase+leg*.5f,1);
            bool support=frame>85 && _actor.CurrentState==Combatant.State.Move && Rig.WalkWeight>.999f && phase is >.12f and <.38f;
            Vector2 point=foot.ToGlobal(foot.Texture.GetSize()*new Vector2(.5f,1));
            if(support)
            {
                float ground=Rig.DepthFootLift(leg);
                _maxGroundError=Mathf.Max(_maxGroundError,Mathf.Abs(ground));
                if(_previousSupport[leg]) { _maxSlip=Mathf.Max(_maxSlip,point.DistanceTo(_previousContact[leg])); _contactSamples++; }
            }
            _previousSupport[leg]=support; _previousContact[leg]=point;
            int thigh=leg==0 ? 9 : 11;
            if(Rig.DepthLegLengthError > .01f || Rig.DepthFootReachError > .05f || Rig.DepthArmLengthError > .01f)
                throw new InvalidOperationException("纵深骨链长度或落脚误差超限");
        }
    }
}
