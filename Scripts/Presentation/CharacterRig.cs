using Godot;
using System.Collections.Generic;

/// <summary>16 关节拆件骨架。骨链驱动部件变换，平级绘制层控制遮挡；武器关节挂手部，动作时钟只读。</summary>
public partial class CharacterRig : Node2D
{
    [Export] public float ArtScale { get; set; } = .038f; // 世界显示尺度；由表现配置覆盖，不影响碰撞体。
    [Export] public float MotionStrength { get; set; } = 1;
    public bool ShowBones { get; set; }
    public Skeleton2D Skeleton { get; private set; }
    public IReadOnlyList<Sprite2D> Parts => _parts;
    public IReadOnlyList<Bone2D> Joints => _bones; // 业务关节顺序；Skeleton2D.GetBone 的树序索引会随父子结构重排。
    public IReadOnlyList<Sprite2D> DrawOrderedParts => _drawOrderedParts; // 角色内部从后往前的绘制顺序，残影使用同一顺序。
    public Combatant Actor { get; private set; }
    private readonly List<Sprite2D> _parts = new();
    private readonly List<Sprite2D> _drawOrderedParts = new();
    private readonly Sprite2D[] _partByBone = new Sprite2D[16];
    private readonly Transform2D[] _partOffsets = new Transform2D[16];
    private Node2D _drawLayers; // 同一 Z 层的平级绘制节点，避免远肢体落到地图地板下面。
    private readonly Bone2D[] _bones = new Bone2D[16];
    private readonly int[] _parents = { -1,0,1,1,3,4,1,6,7,0,9,0,11,1,8,2 };
    private readonly float[] _angles = new float[16];
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
    private ShaderMaterial _cartoonMaterial; // 所有拆件共享的轻量卡通化材质，参数来自表现配置。
    private float _pelvisHeight = 27; // 初始腰骨高度，所有状态复用同一比例。
    private float _thighLength = 12; // 固定大腿长度，与蒙皮尺寸在初始化时一致。
    private float _lowerLegLength = 14; // 膝到靴底的固定长度，动作中不改变。
    public float RestPelvisHeight => _pelvisHeight;
    public float ThighLength => _thighLength;
    public float LowerLegLength => _lowerLegLength;

    public override void _Ready()
    {
        Actor = GetParent<Combatant>();
        MotionStrength = Actor.Presentation?.MotionStrength ?? MotionStrength;
        ArtScale = Actor.Presentation?.CharacterScale ?? ArtScale;
        _source = Actor.GetNode<Sprite2D>(NodeNames.Visual);
        Skeleton = new Skeleton2D { Name = "Skeleton2D" }; AddChild(Skeleton);
        Vector2[] joints = { new(0,-27), new(0,-15), new(1,-13), new(-9,-7), new(0,14), new(0,14),
            new(9,-6), new(0,12), new(0,12), new(-7,3), new(0,12), new(7,3), new(0,12), new(-5,-10), new(0,1), new(0,-12) };
        if (Actor.Presentation?.CalibratedLegProportions == true)
        {
            var proportions = Actor.Presentation;
            _pelvisHeight = proportions.PelvisHeight;
            _thighLength = proportions.ThighLength;
            _lowerLegLength = proportions.LowerLegLength;
            joints[0] = new Vector2(0, -_pelvisHeight);
            // 髋点位于骨盆内侧，不能跟随脚距一起向外挪；腿从胯下连接。
            joints[9] = new Vector2(-proportions.HipHalfWidth, 2);
            joints[11] = new Vector2(proportions.HipHalfWidth, 2);
            joints[10] = joints[12] = new Vector2(0, _thighLength);
        }
        if (Actor.Presentation?.ReferencePoseSet == true)
        {
            // 依据三姿态样板重新绑定上半身；手脚目标共用脚底原点，不从旧持械角度继承。
            joints[1] = new Vector2(0, -11);
            joints[2] = new Vector2(3, -12);
            joints[3] = new Vector2(-7, -6);
            joints[6] = new Vector2(8, -5);
            joints[4] = joints[7] = new Vector2(0, 12);
            joints[5] = joints[8] = new Vector2(0, 12);
            joints[13] = new Vector2(-5, -9);
            joints[15] = new Vector2(-1, -10);
            // 长戟独立于手臂骨链：先确定武器与握点，再由双臂追踪，允许单手释放。
            _parents[14] = -1;
            joints[14] = Vector2.Zero;
        }
        string[] names = { "腰", "胸", "头", "远上臂", "远前臂", "远手", "近上臂", "近前臂", "近手", "远大腿", "远小腿", "近大腿", "近小腿", "披风", "武器", "冠翎" };
        for (int i=0;i<16;i++)
        {
            var bone = new Bone2D { Name = names[i], Position = joints[i] };
            bone.SetAutocalculateLengthAndAngle(false);
            bone.SetLength(i is 9 or 11 ? _thighLength : i is 10 or 12 ? _lowerLegLength : 11);
            bone.Rest = bone.Transform;
            (_parents[i] < 0 ? (Node)Skeleton : _bones[_parents[i]]).AddChild(bone); _bones[i] = bone;
        }
        _drawLayers = new Node2D { Name="部件绘制层" }; AddChild(_drawLayers);
        if (Actor.Presentation?.PartsAtlas != null) BuildParts(Actor.Presentation.PartsAtlas);
        else
        {
            _legacy=new LegacyPortraitRig { SourceActor=Actor }; AddChild(_legacy); Skeleton=_legacy.Skeleton;
        }
        _source.Visible = false;
        _overlay = new Node2D { ZIndex = 30 }; AddChild(_overlay); _overlay.Draw += DrawBones;
    }

    /// <summary>读取每格 alpha 包围盒建立 AtlasTexture，原图保持不变。</summary>
    private void BuildParts(Texture2D atlas)
    {
        using var image = atlas.GetImage();
        if (image.IsCompressed()) image.Decompress();
        if(Actor.Presentation.CartoonFilter) _cartoonMaterial=BuildCartoonMaterial(Actor.Presentation);
        int cellW = image.GetWidth()/4, cellH = image.GetHeight()/4;
        int[] tiles = { 2,1,0,4,5,12,6,7,13,8,9,10,11,3,14,15 };
        Rect2[] rectangles = { new(-11,-3,22,17),new(-11,-13,22,23),new(-7,-16,14,19),
            new(-5,-3,10,16),new(-4,-3,8,16),new(-4,-3,8,8),new(-6,-3,12,16),new(-4,-3,8,16),new(-4,-3,8,8),
            new(-5,-3,10,17),new(-5,-2,11,16),new(-5,-3,10,17),new(-5,-2,11,16),new(-16,-2,26,42),new(-6,-54,12,87),new(-7,-27,19,30) };
        if (Actor.Presentation.CalibratedLegProportions)
        {
            // 只在绑定时校准体型；裙甲止于大腿中段，让膝关节和大腿连接可读。
            rectangles[0] = new Rect2(-10, -3, 20, 15);
            rectangles[1] = new Rect2(-11, -13, 22, 28); // 胸甲下缘覆盖腰带，转腰时不能露出背景裂缝。
            rectangles[2] = new Rect2(-6, -14, 12, 17);
            rectangles[9] = rectangles[11] = new Rect2(-4.5f, -2, 9, _thighLength + 3);
            rectangles[10] = rectangles[12] = new Rect2(-4.5f, -2, 10, _lowerLegLength + 2);
        }
        if (Actor.Presentation.ReferencePoseSet)
        {
            rectangles[0] = new Rect2(-10, -3, 20, 17);
            rectangles[1] = new Rect2(-10, -12, 20, 25);
            rectangles[2] = new Rect2(-5.5f, -10, 11, 14);
            rectangles[3] = rectangles[6] = new Rect2(-5, -3, 10, 16);
            rectangles[4] = rectangles[7] = new Rect2(-3.5f, -2, 7, 15);
            rectangles[5] = rectangles[8] = new Rect2(-2.5f, -2, 5, 6);
            rectangles[13] = new Rect2(-19, -2, 28, 49);
            rectangles[14] = new Rect2(-5, -42, 10, 86);
        }
        // 骨骼只负责变换；部件以兄弟节点顺序完成角色内部遮挡，全部使用 Z=0 参与整个人物的 YSort。
        // 持长兵器时远前臂也跨在胸前，不能与披风一起藏在躯干后面。
        int[] drawOrder = { 13,9,10,11,12,0,1,3,4,2,15,6,7,14,5,8 };
        for(int b=0;b<16;b++)
        {
            var crop=Actor.Presentation.AtlasRegions.Count==16 ? Actor.Presentation.AtlasRegions[tiles[b]]
                : new Rect2(tiles[b]%4*cellW,tiles[b]/4*cellH,cellW,cellH);
            int x0=Mathf.Clamp((int)crop.Position.X,0,image.GetWidth()-1), y0=Mathf.Clamp((int)crop.Position.Y,0,image.GetHeight()-1);
            int width=Mathf.Min((int)crop.Size.X,image.GetWidth()-x0),height=Mathf.Min((int)crop.Size.Y,image.GetHeight()-y0);
            int left=width,top=height,right=0,bottom=0;
            for(int y=2;y<height-2;y+=2) for(int x=2;x<width-2;x+=2)
                if(image.GetPixel(x0+x,y0+y).A>.08f) { left=Mathf.Min(left,x); top=Mathf.Min(top,y); right=Mathf.Max(right,x); bottom=Mathf.Max(bottom,y); }
            if(right<=left || bottom<=top) continue;
            var region=new Rect2(x0+left-1,y0+top-1,right-left+3,bottom-top+3); var rect=rectangles[b];
            if(b==15) rect=Actor.Presentation.CrestRect;
            var sprite=new Sprite2D { Name="拆件", Texture=new AtlasTexture { Atlas=atlas,Region=region },
                Centered=false, Position=rect.Position, Scale=rect.Size/region.Size, TextureFilter=TextureFilterEnum.Linear,
                Material=_cartoonMaterial };
            // 旧 v2 图集双靴相向时只翻近侧；原生同向的 v3 图集关闭该配置。
            sprite.FlipH = b == 12 && Actor.Presentation.AlignBootsForward;
            sprite.FlipV = b == 15 && Actor.Presentation.FlipCrestVertical;
            if (Actor.Presentation.ReferencePoseSet && Actor.Presentation.ReferenceAccessories != null)
                BindReferenceAccessory(b, sprite);
            sprite.Name=_bones[b].Name+"图层";
            _partOffsets[b]=sprite.Transform;
            _partByBone[b]=sprite;
            _parts.Add(sprite);
        }
        foreach(int boneIndex in drawOrder)
            if(_partByBone[boneIndex] is Sprite2D part)
            {
                _drawLayers.AddChild(part); _drawOrderedParts.Add(part);
                if (Actor.Presentation.ReferencePoseSet && boneIndex is 10 or 12)
                    BuildReferenceFoot(boneIndex, part);
            }
        SyncParts();
    }

    /// <summary>将骨链的最终变换同步到平级绘制层，骨骼父子关系不再决定部件遮挡顺序。</summary>
    private void SyncParts()
    {
        Transform2D inverse=_drawLayers.GlobalTransform.AffineInverse();
        for(int i=0;i<_partByBone.Length;i++)
            if(_partByBone[i]!=null)
            {
                Transform2D offset = _partOffsets[i];
                _partByBone[i].Transform=inverse*_bones[i].GlobalTransform*offset;
            }
        if (Actor.Presentation.ReferencePoseSet) SyncReferenceFeet(inverse);
    }

    /// <summary>压缩写实贴图色阶并在透明边缘内侧加深轮廓，降低卡牌立绘感。</summary>
    private static ShaderMaterial BuildCartoonMaterial(CharacterPresentationConfig config)
    {
        var shader=new Shader { Code="""
            shader_type canvas_item;
            render_mode unshaded;
            uniform float color_steps = 7.0;
            uniform float saturation = 1.15;
            uniform vec4 outline_color : source_color = vec4(0.10,0.08,0.07,0.92);
            varying vec4 vertex_tint;
            void vertex() { vertex_tint = COLOR; }
            void fragment() {
                vec4 tex = texture(TEXTURE, UV);
                float edge_alpha = min(min(texture(TEXTURE,UV+vec2(TEXTURE_PIXEL_SIZE.x,0.0)).a,
                    texture(TEXTURE,UV-vec2(TEXTURE_PIXEL_SIZE.x,0.0)).a),
                    min(texture(TEXTURE,UV+vec2(0.0,TEXTURE_PIXEL_SIZE.y)).a,
                    texture(TEXTURE,UV-vec2(0.0,TEXTURE_PIXEL_SIZE.y)).a));
                float luminance = dot(tex.rgb, vec3(0.299,0.587,0.114));
                vec3 saturated = mix(vec3(luminance),tex.rgb,saturation);
                vec3 posterized = floor(clamp(saturated,0.0,1.0)*color_steps+0.5)/color_steps;
                float inner_edge = tex.a>0.08 ? 1.0-smoothstep(0.05,0.42,edge_alpha) : 0.0;
                vec3 color = mix(posterized,outline_color.rgb,inner_edge*outline_color.a);
                // fragment 的输入 COLOR 已乘过纹理；使用顶点颜色避免贴图二次相乘而发黑。
                COLOR = vec4(color,tex.a)*vertex_tint;
            }
            """ };
        var material=new ShaderMaterial { Shader=shader };
        material.SetShaderParameter("color_steps",config.CartoonColorSteps);
        material.SetShaderParameter("saturation",config.CartoonSaturation);
        material.SetShaderParameter("outline_color",config.CartoonOutlineColor);
        return material;
    }

    public override void _Process(double delta)
    {
        if (Skeleton == null) return;
        if (_legacy != null) { _legacy.ShowBones=ShowBones; return; }
        float elapsed = Mathf.Max(0, Actor.VisualTime - _lastTime); _lastTime = Actor.VisualTime;
        bool martial = Actor.Presentation?.MartialWalk == true;
        float travelled = Mathf.Max(0, Actor.WalkDistance - _lastWalkDistance);
        _lastWalkDistance = Actor.WalkDistance;
        if (martial && elapsed > 0)
        {
            _walkPhase = Mathf.PosMod(_walkPhase + travelled / Mathf.Max(1, Actor.Presentation.WalkCycleDistance), 1);
            bool walking = Actor.CurrentState == Combatant.State.Move && Actor.WalkVelocity.LengthSquared() > 1;
            _walkWeight = Mathf.MoveToward(_walkWeight, walking ? 1 : 0, elapsed * 12);
            if (walking) _walkDirection = Actor.WalkVelocity.Normalized();
            float stance = Actor.CurrentState is Combatant.State.Idle or Combatant.State.Move ? 1 : 0;
            _martialWeight = Mathf.MoveToward(_martialWeight, stance, elapsed * 14);
        }
        if (Actor.Presentation?.ReferencePoseSet == true)
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
        for(int i=0;i<16;i++) _bones[i].Rotation=Mathf.LerpAngle(_bones[i].Rotation,_angles[i]*MotionStrength,blend);
        SolveSupportArm();
        float facing=Actor.FacingDirection.X<-.15f ? -1 : Actor.FacingDirection.X>.15f ? 1 : Mathf.Sign(Scale.X);
        float baseScale=ArtScale/.05f;
        float depthScale=Actor.Presentation?.DepthScale ?? .92f;
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
            float settle = Mathf.Lerp(Actor.Presentation.StandCrouch, Actor.Presentation.WalkCrouch, _walkWeight)
                + .18f * _walkWeight * Mathf.Cos(_walkPhase * Mathf.Tau * 2);
            _bones[0].Position = new Vector2(0, -_pelvisHeight + settle * _martialWeight);
            Position = Position.Lerp(Vector2.Zero, _martialWeight);
            ApplyMartialLeg(9, 10, -1, _walkPhase, facing);
            ApplyMartialLeg(11, 12, 1, Mathf.PosMod(_walkPhase + .5f, 1), facing);
        }
        SyncParts();
        _overlay.QueueRedraw();
    }

    private const float WeaponRestAngle = 1.2f; // 基础持械角：刃端指向前上方。

    /// <summary>唯一姿态选择入口：同一骨架复用关节，各状态只生成目标值，不各自争抢骨骼写入。</summary>
    private void SamplePoseTargets(bool martial, float time, float stride)
    {
        System.Array.Clear(_angles);
        SampleStandPose(time, martial);
        switch (Actor.CurrentState)
        {
            case Combatant.State.Move: SampleMovePose(stride, martial); break;
            case Combatant.State.Attack:
            case Combatant.State.Dodge: SampleActionPose(); break;
            case Combatant.State.Hurt: SampleHurtPose(); break;
        }
        // 反击是显式的上身覆盖层，只覆盖列出的关节，不混入行走腿部循环。
        if(Actor.CounterRemaining>0) SampleCounterPose();
    }

    /// <summary>站立警戒：持械稳定、轻微呼吸，腿部不播放移动循环。</summary>
    private void SampleStandPose(float time, bool martial)
    {
        _angles[1] = martial ? .035f + Mathf.Sin(time * 2.8f) * .008f : Mathf.Sin(time * 2.8f) * .025f;
        _angles[2] = -_angles[1] * .5f;
        _angles[6]=.45f; _angles[7]=-1.45f; _angles[14]=WeaponRestAngle;
        _angles[13]=.12f+Mathf.Sin(time*4)*.09f;
        _angles[15]=Mathf.Sin(time*5)*.12f;
    }

    /// <summary>移动姿态只负责行走目标；吕布采用沉胯换步，未启用的角色保留原步态。</summary>
    private void SampleMovePose(float stride, bool martial)
    {
        if (martial)
        {
            float transfer = Mathf.Sin(_walkPhase * Mathf.Tau) * _walkWeight;
            _angles[0] = transfer * .018f;
            _angles[1] = .035f + _walkWeight * .04f - transfer * .025f;
            _angles[2] = -_angles[1] * .6f;
            _angles[13] = .12f + _walkWeight * .10f + transfer * .045f;
        }
        else
        {
            _angles[1] += .06f; _angles[2] = -_angles[1] * .5f;
            _angles[9]=stride*.52f; _angles[11]=-stride*.52f;
            _angles[10]=Mathf.Max(0,-stride)*.65f; _angles[12]=Mathf.Max(0,stride)*.65f;
            _angles[13] += .25f;
        }
    }

    /// <summary>出招与闪避独立读取动作时钟，保留原前摇、生效与后摇，行走循环不驱动攻击。</summary>
    private void SampleActionPose()
    {
        var s=Actor.Spec; float at=Actor.ActionTime;
        float wind=Smooth(at/Mathf.Max(s.Prepare,.001f));
        float hit=Smooth((at-s.Prepare)/Mathf.Max(s.Active,.001f));
        float recovery=Smooth((at-s.Prepare-s.Active)/Mathf.Max(s.Recover,.001f));
        float pose=(-wind+hit*2.1f)*(1-recovery);
        _angles[0]=pose*.18f; _angles[1]=pose*.38f; _angles[2]=-pose*.2f;
        _angles[6]=.45f+pose*1.15f; _angles[7]=-1.45f+pose*.65f; _angles[14]=WeaponRestAngle+pose*.8f;
        _angles[9]=-.35f*wind*(1-recovery); _angles[11]=.3f*wind*(1-recovery);
        _angles[12]=.22f*wind*(1-recovery); _angles[13]=.2f-pose*.45f;
        if(Actor.CurrentAction is CombatAction.Dash or CombatAction.Dodge) { _angles[1]=.35f; _angles[9]=-.7f; _angles[11]=.7f; }
    }

    /// <summary>受击目标与行走目标分离；肩胸收缩、抬臂防御，不翻转整个人物。</summary>
    private void SampleHurtPose()
    {
        _angles[0]=Actor.LastDamage.KnockbackDirection.X*.08f;
        _angles[1]=-.19f; _angles[2]=.11f; _angles[6]=.2f;
    }

    /// <summary>反击的局部上身覆盖层：躯干扭转、披风随动，兵器绕手旋转。</summary>
    private void SampleCounterPose()
    {
        float p=1-Actor.CounterRemaining/Actor.CounterDuration;
        float wave=Mathf.Sin(p*Mathf.Tau);
        _angles[0]=wave*.20f; _angles[1]=wave*.24f;
        _angles[6]=.3f+wave*.28f; _angles[7]=-1.2f-wave*.22f;
        _angles[14]=WeaponRestAngle+p*Mathf.Tau*1.15f;
        _angles[13]=-.45f-wave*.28f;
    }

    /// <summary>固定骨长的两段腿求解：以落脚点弯曲髋膝，超出可达范围时约束落点，不拉伸腿段。</summary>
    private void ApplyMartialLeg(int thigh, int shin, float side, float phase, float facing)
    {
        var config = Actor.Presentation;
        // 62% 周期承重，38% 周期低抬脚前摆；两脚错开半周期，保留双脚接地的时间。
        const float support = .62f;
        float swing = Mathf.Clamp((phase - support) / (1 - support), 0, 1);
        float stride = phase < support ? Mathf.Lerp(1, -1, phase / support) : Mathf.Lerp(-1, 1, Smooth(swing));
        float lift = Mathf.Pow(Mathf.Sin(swing * Mathf.Pi), 2) * config.WalkFootLift * _walkWeight;
        Vector2 direction = new(_walkDirection.X * facing, _walkDirection.Y * .45f);
        float width = Mathf.Lerp(config.StandStanceWidth, config.WalkStanceWidth, _walkWeight);
        Vector2 sole = new Vector2(side * width, side * .5f)
            + direction * (stride * config.WalkStride * _walkWeight) - new Vector2(0, lift);
        // 在骨盆局部坐标求解，左右镜像不改变膝盖朝向；两腿均向角色前方屈膝。
        Vector2 target = _bones[thigh].GetParent<Node2D>().ToLocal(Skeleton.ToGlobal(sole));
        Vector2 offset = target - _bones[thigh].Position;
        float distance = Mathf.Clamp(offset.Length(), Mathf.Abs(_thighLength - _lowerLegLength) + .01f,
            _thighLength + _lowerLegLength - .01f);
        float kneeAngle = Mathf.Acos(Mathf.Clamp((distance * distance - _thighLength * _thighLength
            - _lowerLegLength * _lowerLegLength) / (2 * _thighLength * _lowerLegLength), -1, 1));
        float thighAngle = offset.Angle() - Mathf.Pi / 2
            - Mathf.Atan2(_lowerLegLength * Mathf.Sin(kneeAngle), _thighLength + _lowerLegLength * Mathf.Cos(kneeAngle));
        _bones[thigh].Rotation = Mathf.LerpAngle(_bones[thigh].Rotation, thighAngle, _martialWeight);
        _bones[shin].Rotation = Mathf.LerpAngle(_bones[shin].Rotation, kneeAngle, _martialWeight);
    }

    /// <summary>远手对齐兵器上的独立握点，在胸部局部坐标内求解两节 IK；横向镜像不改变求解结果。</summary>
    private void SolveSupportArm()
    {
        float gripDistance=Actor.Presentation?.SupportGripDistance ?? 10;
        Vector2 target=_bones[1].ToLocal(_bones[14].ToGlobal(new Vector2(0,-gripDistance)));
        Vector2 diff=target-_bones[3].Position;
        float upperLength=_bones[4].Position.Length(),lowerLength=_bones[5].Position.Length();
        float distance=Mathf.Clamp(diff.Length(),.1f,upperLength+lowerLength-.01f);
        float elbow=-Mathf.Acos(Mathf.Clamp((distance*distance-upperLength*upperLength-lowerLength*lowerLength)/(2*upperLength*lowerLength),-1,1));
        _bones[3].Rotation=diff.Angle()-Mathf.Pi/2-Mathf.Atan2(lowerLength*Mathf.Sin(elbow),upperLength+lowerLength*Mathf.Cos(elbow));
        _bones[4].Rotation=elbow;
    }
    private static float Smooth(float value) { float v=Mathf.Clamp(value,0,1); return v*v*(3-2*v); }
    private void DrawBones()
    {
        if(!ShowBones) return;
        for(int i=0;i<16;i++)
        {
            var p=ToLocal(_bones[i].GlobalPosition);
            if(_parents[i]>=0) _overlay.DrawLine(ToLocal(_bones[_parents[i]].GlobalPosition),p,new Color("66f5db"),.7f,true);
            _overlay.DrawCircle(p,1.1f,Colors.White);
        }
    }
}
