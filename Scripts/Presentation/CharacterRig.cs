using Godot;
using System.Collections.Generic;

/// <summary>16 关节拆件骨架。独立肢体随 Bone2D 旋转，武器挂手部，动作时钟只读。</summary>
public partial class CharacterRig : Node2D
{
    [Export] public float ArtScale { get; set; } = .05f; // 世界显示尺度。
    [Export] public float MotionStrength { get; set; } = 1;
    public bool ShowBones { get; set; }
    public Skeleton2D Skeleton { get; private set; }
    public IReadOnlyList<Sprite2D> Parts => _parts;
    public Combatant Actor { get; private set; }
    private readonly List<Sprite2D> _parts = new();
    private readonly Bone2D[] _bones = new Bone2D[16];
    private readonly int[] _parents = { -1,0,1,1,3,4,1,6,7,0,9,0,11,1,8,2 };
    private readonly float[] _angles = new float[16];
    private Node2D _overlay;
    private float _lastTime;
    private Sprite2D _source;
    private LegacyPortraitRig _legacy; // 生图服务不可用或未配置拆件时保留原有蒙皮动画。

    public override void _Ready()
    {
        Actor = GetParent<Combatant>();
        MotionStrength = Actor.Presentation?.MotionStrength ?? MotionStrength;
        _source = Actor.GetNode<Sprite2D>(NodeNames.Visual);
        Skeleton = new Skeleton2D { Name = "Skeleton2D" }; AddChild(Skeleton);
        Vector2[] joints = { new(0,-27), new(0,-15), new(1,-13), new(-7,-9), new(0,11), new(0,11),
            new(8,-8), new(0,11), new(0,11), new(-5,3), new(0,12), new(5,3), new(0,12), new(-5,-10), new(0,1), new(0,-12) };
        string[] names = { "腰", "胸", "头", "远上臂", "远前臂", "远手", "近上臂", "近前臂", "近手", "远大腿", "远小腿", "近大腿", "近小腿", "披风", "武器", "冠翎" };
        for (int i=0;i<16;i++)
        {
            var bone = new Bone2D { Name = names[i], Position = joints[i] };
            bone.SetAutocalculateLengthAndAngle(false); bone.SetLength(11); bone.Rest = bone.Transform;
            (_parents[i] < 0 ? (Node)Skeleton : _bones[_parents[i]]).AddChild(bone); _bones[i] = bone;
        }
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
        int cellW = image.GetWidth()/4, cellH = image.GetHeight()/4;
        int[] tiles = { 2,1,0,4,5,12,6,7,13,8,9,10,11,3,14,15 };
        Rect2[] rectangles = { new(-11,-3,22,17),new(-11,-13,22,23),new(-7,-16,14,19),
            new(-5,-3,10,16),new(-4,-3,8,16),new(-4,-3,8,8),new(-6,-3,12,16),new(-4,-3,8,16),new(-4,-3,8,8),
            new(-5,-3,10,17),new(-5,-2,11,16),new(-5,-3,10,17),new(-5,-2,11,16),new(-16,-2,26,42),new(-6,-54,12,87),new(-7,-27,19,30) };
        int[] layers = { 0,2,4,-3,-3,-2,5,6,8,-2,-2,1,1,-5,7,4 };
        for(int b=0;b<16;b++)
        {
            int x0=tiles[b]%4*cellW, y0=tiles[b]/4*cellH;
            int left=cellW,top=cellH,right=0,bottom=0;
            for(int y=3;y<cellH-3;y+=2) for(int x=3;x<cellW-3;x+=2)
                if(image.GetPixel(x0+x,y0+y).A>.08f) { left=Mathf.Min(left,x); top=Mathf.Min(top,y); right=Mathf.Max(right,x); bottom=Mathf.Max(bottom,y); }
            if(right<=left || bottom<=top) continue;
            var region=new Rect2(x0+left-2,y0+top-2,right-left+5,bottom-top+5); var rect=rectangles[b];
            var sprite=new Sprite2D { Name="拆件", Texture=new AtlasTexture { Atlas=atlas,Region=region },
                Centered=false, Position=rect.Position, Scale=rect.Size/region.Size, ZIndex=layers[b], TextureFilter=TextureFilterEnum.Linear };
            _bones[b].AddChild(sprite); _parts.Add(sprite);
        }
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
        _angles[6]=-.7f; _angles[7]=-1f; _angles[14]=.7f;
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
            _angles[6]=-.7f+pose*1.15f; _angles[7]=-1f+pose*.65f; _angles[14]=.7f+pose*.8f;
            _angles[9]=-.35f*wind*(1-recovery); _angles[11]=.3f*wind*(1-recovery);
            _angles[12]=.22f*wind*(1-recovery); _angles[13]=.2f-pose*.45f;
            if(Actor.CurrentAction is CombatAction.Dash or CombatAction.Dodge) { _angles[1]=.35f; _angles[9]=-.7f; _angles[11]=.7f; }
        }
        if(Actor.CurrentState==Combatant.State.Hurt) { _angles[1]=-.24f; _angles[2]=.15f; }
        if(Actor.CounterRemaining>0) { _angles[6]=-1.3f; _angles[7]=-.25f; _angles[14]=1.3f; _angles[13]=-.55f; }
        float blend=1-Mathf.Exp(-24*elapsed);
        for(int i=0;i<16;i++) _bones[i].Rotation=Mathf.LerpAngle(_bones[i].Rotation,_angles[i]*MotionStrength,blend);
        // 远手双节 IK 跟随近手下方握点，双手持柄不分离。
        Vector2 target=Skeleton.ToLocal(_bones[8].GlobalPosition)+new Vector2(-2,4);
        Vector2 shoulder=Skeleton.ToLocal(_bones[3].GlobalPosition);
        Vector2 diff=target-shoulder; float d=Mathf.Clamp(diff.Length(),1,21.8f);
        float elbow=Mathf.Acos(Mathf.Clamp((d*d-242)/242,-1,1));
        _bones[3].Rotation=diff.Angle()-Mathf.Pi/2-Mathf.Atan2(11*Mathf.Sin(elbow),11+11*Mathf.Cos(elbow))-_bones[0].Rotation-_bones[1].Rotation;
        _bones[4].Rotation=elbow;
        float facing=Actor.FacingDirection.X<-.15f ? -1 : Actor.FacingDirection.X>.15f ? 1 : Mathf.Sign(Scale.X);
        float turn=Actor.CounterRemaining>0 ? Mathf.Cos((1-Actor.CounterRemaining/Actor.CounterDuration)*Mathf.Tau) : 1;
        Scale=new Vector2(facing*(Mathf.Abs(turn)<.16f ? .16f : turn),1)*(ArtScale/.05f);
        Modulate=_source.Modulate; Rotation=Actor.IsDead ? .5f*Mathf.Pi*facing : 0;
        Position=new Vector2(0,Actor.IsDead ? -5 : -Mathf.Abs(stride)*1.5f);
        _overlay.QueueRedraw();
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
