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
    private Sprite2D _source;
    private LegacyPortraitRig _legacy; // 生图服务不可用或未配置拆件时保留原有蒙皮动画。
    private ShaderMaterial _cartoonMaterial; // 所有拆件共享的轻量卡通化材质，参数来自表现配置。

    public override void _Ready()
    {
        Actor = GetParent<Combatant>();
        MotionStrength = Actor.Presentation?.MotionStrength ?? MotionStrength;
        ArtScale = Actor.Presentation?.CharacterScale ?? ArtScale;
        _source = Actor.GetNode<Sprite2D>(NodeNames.Visual);
        Skeleton = new Skeleton2D { Name = "Skeleton2D" }; AddChild(Skeleton);
        Vector2[] joints = { new(0,-27), new(0,-15), new(1,-13), new(-9,-7), new(0,14), new(0,14),
            new(9,-6), new(0,12), new(0,12), new(-7,3), new(0,12), new(7,3), new(0,12), new(-5,-10), new(0,1), new(0,-12) };
        string[] names = { "腰", "胸", "头", "远上臂", "远前臂", "远手", "近上臂", "近前臂", "近手", "远大腿", "远小腿", "近大腿", "近小腿", "披风", "武器", "冠翎" };
        for (int i=0;i<16;i++)
        {
            var bone = new Bone2D { Name = names[i], Position = joints[i] };
            bone.SetAutocalculateLengthAndAngle(false); bone.SetLength(11); bone.Rest = bone.Transform;
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
            sprite.Name=_bones[b].Name+"图层";
            _partOffsets[b]=sprite.Transform;
            _partByBone[b]=sprite;
            _parts.Add(sprite);
        }
        foreach(int boneIndex in drawOrder)
            if(_partByBone[boneIndex] is Sprite2D part) { _drawLayers.AddChild(part); _drawOrderedParts.Add(part); }
        SyncParts();
    }

    /// <summary>将骨链的最终变换同步到平级绘制层，骨骼父子关系不再决定部件遮挡顺序。</summary>
    private void SyncParts()
    {
        Transform2D inverse=_drawLayers.GlobalTransform.AffineInverse();
        for(int i=0;i<_partByBone.Length;i++)
            if(_partByBone[i]!=null) _partByBone[i].Transform=inverse*_bones[i].GlobalTransform*_partOffsets[i];
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
        float t=Actor.VisualTime, walk=Actor.CurrentState==Combatant.State.Move ? 1 : 0;
        float stride=Mathf.Sin(t*12)*walk;
        System.Array.Clear(_angles);
        _angles[1]=Mathf.Sin(t*2.8f)*.025f + walk*.06f; _angles[2]=-_angles[1]*.5f;
        // 武器顶端指向前上方，避免待机时用柄端迎敌或刀刃横穿面部。
        const float weaponRest = 1.2f;
        _angles[6]=.45f; _angles[7]=-1.45f; _angles[14]=weaponRest;
        _angles[9]=stride*.52f; _angles[11]=-stride*.52f;
        _angles[10]=Mathf.Max(0,-stride)*.65f; _angles[12]=Mathf.Max(0,stride)*.65f;
        _angles[13]=.12f+Mathf.Sin(t*4)*.09f+walk*.25f; _angles[15]=Mathf.Sin(t*5)*.12f;
        if(Actor.CurrentState is Combatant.State.Attack or Combatant.State.Dodge)
        {
            var s=Actor.Spec; float at=Actor.ActionTime;
            float wind=Smooth(at/Mathf.Max(s.Prepare,.001f));
            float hit=Smooth((at-s.Prepare)/Mathf.Max(s.Active,.001f));
            float recovery=Smooth((at-s.Prepare-s.Active)/Mathf.Max(s.Recover,.001f));
            float pose=(-wind+hit*2.1f)*(1-recovery);
            _angles[0]=pose*.18f; _angles[1]=pose*.38f; _angles[2]=-pose*.2f;
            _angles[6]=.45f+pose*1.15f; _angles[7]=-1.45f+pose*.65f; _angles[14]=weaponRest+pose*.8f;
            _angles[9]=-.35f*wind*(1-recovery); _angles[11]=.3f*wind*(1-recovery);
            _angles[12]=.22f*wind*(1-recovery); _angles[13]=.2f-pose*.45f;
            if(Actor.CurrentAction is CombatAction.Dash or CombatAction.Dodge) { _angles[1]=.35f; _angles[9]=-.7f; _angles[11]=.7f; }
        }
        if(Actor.CurrentState==Combatant.State.Hurt)
        {
            // 受击只做肩胸收缩和抬臂防御，不旋转或翻转整张人物纸片。
            _angles[0]=Actor.LastDamage.KnockbackDirection.X*.08f;
            _angles[1]=-.19f; _angles[2]=.11f; _angles[6]=.2f;
        }
        if(Actor.CounterRemaining>0)
        {
            // 反击螺旋通过躯干扭转、披风摆动和武器绕手旋转表达，不再把 Sprite2D 横向压缩到负值。
            float p=1-Actor.CounterRemaining/Actor.CounterDuration;
            float wave=Mathf.Sin(p*Mathf.Tau);
            _angles[0]=wave*.20f; _angles[1]=wave*.24f;
            _angles[6]=.3f+wave*.28f; _angles[7]=-1.2f-wave*.22f;
            _angles[14]=weaponRest+p*Mathf.Tau*1.15f;
            _angles[13]=-.45f-wave*.28f;
        }
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
        SyncParts();
        _overlay.QueueRedraw();
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
