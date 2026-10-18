using Godot;
using System.Collections.Generic;

/// <summary>整身、四条整腿与尾部的连续纹理蒙皮。32个控制节点保留关节与挂点，不按骨数拆碎皮肤。</summary>
public partial class HorsePaintedRig : Node2D
{
    public HorseAppearance Appearance { get; set; }
    public Skeleton2D Skeleton { get; private set; }
    public IReadOnlyList<Bone2D> Bones => _bones;
    public IReadOnlyList<HorseLimbSkin> LimbSkins => _limbSkins;
    public Polygon2D BodySkin { get; private set; }
    public Polygon2D TailSkin { get; private set; }
    public Vector2 SaddleWorld => ToGlobal(_saddle);
    public Vector2 BridleWorld => ToGlobal(_bridle);
    public Vector2 HoofWorld(bool front,bool near) => _limbSkins[(front?2:0)+(near?1:0)].ToGlobal(_limbSkins[(front?2:0)+(near?1:0)].SoleCenter);
    private readonly Bone2D[] _bones=new Bone2D[32];
    private readonly Transform2D[] _poses=new Transform2D[32];
    private readonly HorseLimbSkin[] _limbSkins=new HorseLimbSkin[4];
    private readonly int[] _parents={-1,0,0,0,0,4,5,6,6,1,9,10,5,4,0,1,
        1,16,17,18,1,20,21,22,2,24,25,26,3,28,29,30};
    private Vector2[] _bodyPixels,_tailPixels,_bodyVertices,_tailVertices;
    private Vector2 _saddle,_bridle;
    private Node2D _layers;

    public override void _Ready()
    {
        Skeleton=new Skeleton2D {Name="Skeleton2D"}; AddChild(Skeleton);
        string[] names={"胸廓","骨盆","远肩胛","近肩胛","下颈","上颈","头","远耳","近耳","尾根","尾中","尾梢","上鬃","下鬃","鞍座","行囊",
            "远后大腿","远后胫骨","远后管骨","远后蹄","近后大腿","近后胫骨","近后管骨","近后蹄",
            "远前上臂","远前前臂","远前管骨","远前蹄","近前上臂","近前前臂","近前管骨","近前蹄"};
        for(int i=0;i<_bones.Length;i++)
        {
            _bones[i]=new Bone2D {Name=names[i]};
            _bones[i].SetAutocalculateLengthAndAngle(false); _bones[i].SetLength(8);
            (_parents[i]<0?(Node)Skeleton:_bones[_parents[i]]).AddChild(_bones[i]);
        }
        _layers=new Node2D {Name="彩绘层"}; AddChild(_layers);
        using var source=Appearance.PartsAtlas.GetImage();
        if(source.IsCompressed()) source.Decompress();
        // 兄弟顺序决定遮挡，全部Z=0以保留城市YSort：远肢→尾→整身→近肢。
        AddLeg(0,source); AddLeg(2,source);
        TailSkin=BuildMesh("连续尾毛",HorseArtAtlas.Tail,10,24,out _tailPixels);
        BodySkin=BuildMesh("连续躯干颈头",HorseArtAtlas.Body,56,28,out _bodyPixels);
        _bodyVertices=new Vector2[_bodyPixels.Length]; _tailVertices=new Vector2[_tailPixels.Length];
        AddLeg(1,source); AddLeg(3,source);
        ApplyPose(0,0);
        foreach(var bone in _bones) bone.Rest=bone.Transform;
    }
    private void AddLeg(int leg,Image source)
    {
        _limbSkins[leg]=new(Appearance.PartsAtlas,source,leg>=2,leg%2==1);
        _layers.AddChild(_limbSkins[leg]);
    }
    private Polygon2D BuildMesh(string name,Rect2 region,int columns,int rows,out Vector2[] pixels)
    {
        pixels=new Vector2[(columns+1)*(rows+1)];
        var vertices=new Vector2[pixels.Length]; var triangles=new Godot.Collections.Array();
        for(int y=0;y<=rows;y++) for(int x=0;x<=columns;x++)
        {
            int i=y*(columns+1)+x;
            pixels[i]=region.Position+new Vector2(region.Size.X*x/columns,region.Size.Y*y/rows);
            vertices[i]=new(x,y);
            if(x<columns&&y<rows){triangles.Add(new[]{i,i+1,i+columns+2});triangles.Add(new[]{i,i+columns+2,i+columns+1});}
        }
        var mesh=new Polygon2D {Name=name,Texture=Appearance.PartsAtlas,UV=pixels,Polygon=vertices,Polygons=triangles,TextureFilter=TextureFilterEnum.Linear};
        // 生成PNG在透明背景有低Alpha残留。只剔除几乎不可见的背景，保留毛边；不改写原图。
        mesh.Material=new ShaderMaterial {Shader=new Shader {Code="shader_type canvas_item; void fragment(){ if(COLOR.a < 0.035) discard; }"}};
        _layers.AddChild(mesh); return mesh;
    }
    private void SetBone(int id,Transform2D transform)
    {
        _poses[id]=transform;
        _bones[id].Transform=_parents[id]<0?transform:_poses[_parents[id]].AffineInverse()*transform;
    }
    private void SetSegment(int id,Vector2 start,Vector2 end)
    {
        SetBone(id,new Transform2D((end-start).Angle(),start)); _bones[id].SetLength(start.DistanceTo(end));
    }
    private Vector2 BodyPoint(Vector2 pixel,HorseBodyMotion.Pose pose)
    {
        Vector2 rest=HorseArtAtlas.BodyPoint(pixel,Appearance.BodyWidth);
        Vector2 trunk=pose.SkinPoint(rest);
        // 胸颈、上下颈和枕部权重连续；头内保持同一刚性变换，不把脸当橡皮拉伸。
        float neck=Mathf.SmoothStep(795,1090,pixel.X);
        float upper=1-Mathf.SmoothStep(120,310,pixel.Y);
        Vector2 neckPoint=(pose.LowerNeck*rest).Lerp(pose.UpperNeck*rest,upper);
        Vector2 point=trunk.Lerp(neckPoint,neck);
        return point.Lerp(pose.Head*rest,Mathf.SmoothStep(1110,1190,pixel.X));
    }
    public void ApplyPose(float phase,float weight)
    {
        if(Skeleton==null) return;
        var p=HorseBodyMotion.Sample(phase,weight,Appearance.BodyWidth);
        _saddle=p.Saddle; _bridle=BodyPoint(HorseArtAtlas.BridlePixel,p);
        SetBone(0,p.Chest*new Transform2D(0,new(8,-35)));
        SetBone(1,p.Pelvis*new Transform2D(0,new(-20*Appearance.BodyWidth,-35)));
        SetBone(2,p.FarShoulder*new Transform2D(0,new(18*Appearance.BodyWidth,-35)));
        SetBone(3,p.NearShoulder*new Transform2D(0,new(18*Appearance.BodyWidth,-35)));
        SetSegment(4,p.NeckRoot,p.NeckMiddle); SetSegment(5,p.NeckMiddle,p.Poll);
        SetBone(6,new Transform2D(p.Head.Rotation,p.Poll));
        SetSegment(7,p.Poll,p.Poll+Vector2.Up*5); SetSegment(8,p.Poll+Vector2.Right*2,p.Poll+new Vector2(2,-5));
        float trail=weight*(.18f+.055f*Mathf.Sin((phase-.12f)*Mathf.Tau));
        Vector2 tailMid=p.TailRoot+new Vector2(-5,15).Rotated(trail+p.Pelvis.Rotation);
        Vector2 tailEnd=tailMid+new Vector2(-5,15).Rotated(trail+.07f*weight*Mathf.Sin((phase-.24f)*Mathf.Tau));
        SetSegment(9,p.TailRoot,tailMid); SetSegment(10,tailMid,tailEnd); SetSegment(11,tailEnd,tailEnd+new Vector2(-1,3));
        SetSegment(12,p.Poll,p.NeckMiddle); SetSegment(13,p.NeckMiddle,p.NeckRoot);
        SetBone(14,new Transform2D(p.ChestAngle,p.Saddle));
        SetBone(15,p.Pelvis*new Transform2D(0,new(-12*Appearance.BodyWidth,-37)));
        for(int leg=0;leg<4;leg++)
        {
            var q=HorseLocomotion.Sample(leg>=2,leg%2==1,phase,weight,Appearance.BodyWidth); int bone=16+leg*4;
            SetSegment(bone,q.Root,q.Proximal); SetSegment(bone+1,q.Proximal,q.Joint);
            SetSegment(bone+2,q.Joint,q.Fetlock); SetSegment(bone+3,q.Fetlock,q.Hoof);
            _limbSkins[leg].Apply(q,p);
        }
        for(int i=0;i<_bodyVertices.Length;i++) _bodyVertices[i]=BodyPoint(_bodyPixels[i],p);
        BodySkin.Polygon=_bodyVertices;
        for(int i=0;i<_tailVertices.Length;i++)
        {
            Vector2 pixel=_tailPixels[i]; float t=(pixel.Y-510)/480;
            Vector2 center=t<.5f?p.TailRoot.Lerp(tailMid,t*2):tailMid.Lerp(tailEnd,(t-.5f)*2);
            // 纹理原尾向右下，绑定为向后下垂；横向偏移从中心轴算，避免重复施加尾长。
            float axisX=Mathf.Lerp(1075,1280,t);
            _tailVertices[i]=center+new Vector2(-(pixel.X-axisX)*.045f,0);
        }
        TailSkin.Polygon=_tailVertices;
    }
}
