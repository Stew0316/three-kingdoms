using Godot;
using System.Collections.Generic;

/// <summary>16 关节拆件骨架。骨链驱动部件变换，平级绘制层控制遮挡；参考姿态独立求解武器和握点，动作时钟只读。</summary>
public partial class CharacterRig : Node2D
{
    [Export] public float ArtScale { get; set; } = .038f; // 世界显示尺度；由表现配置覆盖，不影响碰撞体。
    [Export] public float MotionStrength { get; set; } = 1;
    public bool ShowBones { get; set; }
    public Skeleton2D Skeleton { get; private set; }
    public IReadOnlyList<Sprite2D> Parts => _parts;
    public IReadOnlyList<Bone2D> Joints => _bones; // 业务关节顺序；Skeleton2D.GetBone 的树序索引会随父子结构重排。
    public IReadOnlyList<Sprite2D> DrawOrderedParts => _drawOrderedParts; // 角色内部从后往前的绘制顺序，残影使用同一顺序。
    public Sprite2D WeaponPart => _partByBone[14]; // 主武器图层，强化效果只对它施加白色辉光。
    public Combatant Actor { get; private set; }
    private readonly List<Sprite2D> _parts = new();
    private readonly List<Sprite2D> _drawOrderedParts = new();
    private readonly Sprite2D[] _partByBone = new Sprite2D[CharacterRigDefaults.JointCount];
    private readonly Transform2D[] _partOffsets = new Transform2D[CharacterRigDefaults.JointCount];
    private Node2D _drawLayers; // 同一 Z 层的平级绘制节点，避免远肢体落到地图地板下面。
    private readonly Bone2D[] _bones = new Bone2D[CharacterRigDefaults.JointCount];
    private readonly int[] _parents = CharacterRigDefaults.CreateParents();
    private readonly float[] _angles = new float[CharacterRigDefaults.JointCount];
    private Node2D _overlay;
    private float _lastTime;
    private float _walkPhase; // 路程驱动的左右脚循环，不跟随待机时间自行推进。
    private float _lastWalkDistance;
    private float _walkWeight; // 起停过渡，只混合步幅，不延迟输入与碰撞移动。
    private Vector2 _walkDirection = Vector2.Right; // 最近的实际行走方向；停下后保留以收势。
    private float _martialWeight; // 从宽站姿过渡到攻击/受击姿态的权重。
    public float WalkPhase => _walkPhase; // 调试与回归读取，不控制游戏规则。
    public float WalkWeight => _walkWeight;
    private Sprite2D _source;
    private LegacyPortraitRig _legacy; // 生图服务不可用或未配置拆件时保留原有蒙皮动画。
    private ShaderMaterial _cartoonMaterial; // 普通拆件共享的轻量卡通化材质；有采样遮罩的武器使用独立材质。
    private float _pelvisHeight = 27; // 初始腰骨高度，所有状态复用同一比例。
    private float _thighLength = 12; // 固定大腿长度，与蒙皮尺寸在初始化时一致。
    private float _lowerLegLength = 14; // 膝到靴底的固定长度，动作中不改变。
    public float RestPelvisHeight => _pelvisHeight;
    public float ThighLength => _thighLength;
    public float LowerLegLength => _lowerLegLength;

    public override void _Ready()
    {
        Actor = GetParent<Combatant>();
        MotionStrength = Actor.Presentation?.Animation.MotionStrength ?? MotionStrength;
        ArtScale = Actor.Presentation?.Model.CharacterScale ?? ArtScale;
        _source = Actor.GetNode<Sprite2D>(NodeNames.Visual);
        BuildSkeleton();
        _drawLayers = new Node2D { Name="部件绘制层" }; AddChild(_drawLayers);
        if (Actor.Presentation?.Model.PartsAtlas != null) BuildParts(Actor.Presentation.Model.PartsAtlas);
        else
        {
            _legacy=new LegacyPortraitRig { SourceActor=Actor }; AddChild(_legacy); Skeleton=_legacy.Skeleton;
        }
        _source.Visible = false;
        _overlay = new Node2D { ZIndex = 30 }; AddChild(_overlay); _overlay.Draw += DrawBones;
        // 装配完成就落到配置中的站姿，首帧和暂停进入场景时也不能露出直臂绑定姿态。
        if (_legacy == null && Actor.Presentation?.Animation.ReferencePoseSet == true)
        {
            ProcessReferencePose(0);
            SyncParts();
        }
    }

    public override void _Process(double delta)
    {
        if (Skeleton == null) return;
        if (_legacy != null) { _legacy.ShowBones=ShowBones; return; }
        if (_partByBone[14] != null) _partByBone[14].Visible = !Actor.IsMounted;
        if (_offhandWeapon != null) _offhandWeapon.Visible = !Actor.IsMounted;
        if (Actor.IsMounted) { ProcessRidingPose(); return; }
        float elapsed = Mathf.Max(0, Actor.VisualTime - _lastTime); _lastTime = Actor.VisualTime;
        bool martial = Actor.Presentation?.Animation.MartialWalk == true;
        float travelled = Mathf.Max(0, Actor.WalkDistance - _lastWalkDistance);
        _lastWalkDistance = Actor.WalkDistance;
        if (martial && elapsed > 0)
        {
            _walkPhase = Mathf.PosMod(_walkPhase + travelled / Mathf.Max(1, Actor.Presentation.Animation.WalkCycleDistance), 1);
            bool walking = Actor.CurrentState == Combatant.State.Move && Actor.WalkVelocity.LengthSquared() > 1;
            _walkWeight = Mathf.MoveToward(_walkWeight, walking ? 1 : 0, elapsed * 12);
            if (walking) _walkDirection = Actor.WalkVelocity.Normalized();
            float stance = Actor.CurrentState is Combatant.State.Idle or Combatant.State.Move ? 1 : 0;
            _martialWeight = Mathf.MoveToward(_martialWeight, stance, elapsed * 14);
        }
        if (Actor.Presentation?.Animation.ReferencePoseSet == true)
        {
            ProcessReferencePose(elapsed);
            SyncParts();
            _overlay.QueueRedraw();
            return;
        }
        float t=Actor.VisualTime, walk=Actor.CurrentState==Combatant.State.Move ? 1 : 0;
        float stride=Mathf.Sin(t*12)*walk;
        SamplePoseTargets(martial, t, stride);
        float blend=1-Mathf.Exp(-24*elapsed);
        for(int i=0;i<CharacterRigDefaults.JointCount;i++) _bones[i].Rotation=Mathf.LerpAngle(_bones[i].Rotation,_angles[i]*MotionStrength,blend);
        SolveSupportArm();
        float facing=Actor.FacingDirection.X<-.15f ? -1 : Actor.FacingDirection.X>.15f ? 1 : Mathf.Sign(Scale.X);
        float baseScale=ScreenLockedScale();
        float depthScale=Actor.Presentation?.Model.DepthScale ?? .92f;
        Vector2 poseScale=new(1,depthScale);
        if(Actor.CurrentState==Combatant.State.Hurt) poseScale=new(1.08f,depthScale*.86f);
        if(Actor.IsDead) poseScale=new(1.12f,depthScale*.52f);
        Scale=new Vector2(facing*baseScale*poseScale.X,baseScale*poseScale.Y);
        Modulate=_source.Modulate;
        Rotation=Actor.CurrentState==Combatant.State.Hurt ? Actor.LastDamage.KnockbackDirection.X*.045f : 0;
        float verticalLean=Actor.CurrentState==Combatant.State.Move ? Actor.FacingDirection.Y*1.1f : 0;
        Position=new Vector2(Actor.CurrentState==Combatant.State.Hurt ? Actor.LastDamage.KnockbackDirection.X*1.8f : 0,
            Actor.IsDead ? 2 : -Mathf.Abs(stride)*1.25f+verticalLean);
        if (martial)
        {
            // 站立与移动各有重心，短过渡连接；出招/受击时退出行走修正，交还动作姿态。
            float settle = Mathf.Lerp(Actor.Presentation.Animation.StandCrouch, Actor.Presentation.Animation.WalkCrouch, _walkWeight)
                + .18f * _walkWeight * Mathf.Cos(_walkPhase * Mathf.Tau * 2);
            _bones[0].Position = new Vector2(0, -_pelvisHeight + settle * _martialWeight);
            Position = Position.Lerp(Vector2.Zero, _martialWeight);
            ApplyMartialLeg(9, 10, -1, _walkPhase, facing);
            ApplyMartialLeg(11, 12, 1, Mathf.PosMod(_walkPhase + .5f, 1), facing);
        }
        SyncParts();
        _overlay.QueueRedraw();
    }

    /// <summary>将人物锁定在约 200px 屏幕高度。相机 Zoom、窗口拉伸都会改变 Canvas 变换，故反向补偿；碰撞与战斗坐标保持世界尺度。</summary>
    private float ScreenLockedScale()
    {
        // CanvasTransform 含相机，StretchTransform 含窗口/高 DPI 拉伸；两者都要抵消才是物理屏幕像素恒定。
        float canvasScale = GetViewport().GetCanvasTransform().X.Length() * GetViewport().GetStretchTransform().X.Length();
        if (canvasScale <= .001f) return ArtScale / .05f;
        // 参考姿态的头顶到武器/脚点约为 100 个骨架单位，预留少量飘动特效空间。
        return Actor.Presentation?.Model.TargetScreenPixels / (100f * canvasScale) ?? ArtScale / .05f;
    }

    private void DrawBones()
    {
        if(!ShowBones) return;
        for(int i=0;i<CharacterRigDefaults.JointCount;i++)
        {
            var p=ToLocal(_bones[i].GlobalPosition);
            if(_parents[i]>=0) _overlay.DrawLine(ToLocal(_bones[_parents[i]].GlobalPosition),p,new Color("66f5db"),.7f,true);
            _overlay.DrawCircle(p,1.1f,Colors.White);
        }
    }
}
