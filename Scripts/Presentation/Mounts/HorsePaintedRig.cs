using Godot;
using System.Collections.Generic;

/// <summary>整身、四条整腿、尾与鞍具表层的连续纹理蒙皮。32个控制节点保留关节与挂点。</summary>
[GlobalClass]
public partial class HorsePaintedRig : Node2D
{
    [Export] public HorseAppearance Appearance { get; set; }
    public Skeleton2D Skeleton { get; private set; }
    public IReadOnlyList<Bone2D> Bones => _bones;
    public IReadOnlyList<HorseLimbSkin> LimbSkins => _limbSkins;
    public Polygon2D BodySkin { get; private set; }
    public Polygon2D TailSkin { get; private set; }
    public Polygon2D TackSkin { get; private set; }
    public Vector2 SaddleWorld => ToGlobal(_saddle);
    public Vector2 BridleWorld => ToGlobal(_bridle);
    public Vector2 HoofWorld(bool front,bool near) => _limbSkins[(front?2:0)+(near?1:0)].ToGlobal(_limbSkins[(front?2:0)+(near?1:0)].SoleCenter);
    private readonly Bone2D[] _bones=new Bone2D[HorseSkeletonDefinition.BoneCount];
    private readonly Transform2D[] _poses=new Transform2D[HorseSkeletonDefinition.BoneCount];
    private readonly HorseLimbSkin[] _limbSkins=new HorseLimbSkin[4];
    private Vector2[] _bodyPixels,_tailPixels,_bodyVertices,_tailVertices;
    private Vector2[] _tackVertices;
    private MuscleBinding[] _hindBindings,_foreBindings;
    private Vector2 _hindNormal,_foreNormal;
    private int _hindEndIndex,_foreEndIndex;
    private Vector2 _saddle,_bridle;
    private Node2D _layers;
    private HorseLocomotion.LegPose _hindPose, _forePose;
    // 返回实际身体网格中的锚点，不另采一份理想曲线供测试自证。
    public Vector2 UpperMuscleEnd(bool front) => _bodyVertices[front?_foreEndIndex:_hindEndIndex];

    public override void _Ready()
    {
        HorseAppearanceCatalog.RetainBuiltIn(Appearance);
        Skeleton=new Skeleton2D {Name="Skeleton2D"}; AddChild(Skeleton);
        for(int i=0;i<_bones.Length;i++)
        {
            _bones[i]=new Bone2D {Name=HorseSkeletonDefinition.Name(i)};
            _bones[i].SetAutocalculateLengthAndAngle(false); _bones[i].SetLength(8);
            int parent=HorseSkeletonDefinition.Parent(i);
            (parent<0?(Node)Skeleton:_bones[parent]).AddChild(_bones[i]);
        }
        _layers=new Node2D {Name="彩绘层"}; AddChild(_layers);
        using var source=Appearance.PartsAtlas.GetImage();
        if(source.IsCompressed()) source.Decompress();
        // 肩胯的外表面来自整身图，覆盖腿根，避免同一处肌肉被两张皮肤重复画出。
        // 全部Z=0以保留城市YSort；近腿远端在身体前，鞍具最后覆盖活动的肩胯。
        AddLeg(0,source); AddLeg(2,source);
        TailSkin=BuildMesh("连续尾毛",HorseArtAtlas.Tail,10,24,out _tailPixels);
        BodySkin=BuildMesh("连续躯干颈头",HorseArtAtlas.Body,56,28,out _bodyPixels,true);
        AddLeg(1,source); AddLeg(3,source);
        _tackVertices=new Vector2[HorseArtAtlas.TackOutline.Length];
        TackSkin=new Polygon2D {Name="鞍具表层",Texture=Appearance.PartsAtlas,UV=HorseArtAtlas.TackOutline,
            Polygon=HorseArtAtlas.TackOutline,TextureFilter=TextureFilterEnum.LinearWithMipmaps,Material=HorseSurfaceMaterial.Shared};
        _layers.AddChild(TackSkin);
        _bodyVertices=new Vector2[_bodyPixels.Length]; _tailVertices=new Vector2[_tailPixels.Length];
        _hindBindings=new MuscleBinding[_bodyPixels.Length]; _foreBindings=new MuscleBinding[_bodyPixels.Length];
        for(int i=0;i<_bodyPixels.Length;i++)
        {
            _hindBindings[i]=BindMuscle(_bodyPixels[i],false);
            _foreBindings[i]=BindMuscle(_bodyPixels[i],true);
            if(_bodyPixels[i]==HorseArtAtlas.MuscleEnd(false)) _hindEndIndex=i;
            if(_bodyPixels[i]==HorseArtAtlas.MuscleEnd(true)) _foreEndIndex=i;
        }
        ApplyPose(0,0);
        foreach(var bone in _bones) bone.Rest=bone.Transform;
    }
    private void AddLeg(int leg,Image source)
    {
        _limbSkins[leg]=new(Appearance.PartsAtlas,source,leg>=2,leg%2==1);
        _layers.AddChild(_limbSkins[leg]);
    }
    private Polygon2D BuildMesh(string name,Rect2 region,int columns,int rows,out Vector2[] pixels,bool muscleAnchors=false)
    {
        var xs=new List<float>(); var ys=new List<float>();
        for(int x=0;x<=columns;x++) xs.Add(region.Position.X+region.Size.X*x/columns);
        for(int y=0;y<=rows;y++) ys.Add(region.Position.Y+region.Size.Y*y/rows);
        if(muscleAnchors) foreach(bool front in new[]{false,true})
        {
            Vector2 end=HorseArtAtlas.MuscleEnd(front);
            if(!xs.Contains(end.X)) xs.Add(end.X);
            if(!ys.Contains(end.Y)) ys.Add(end.Y);
        }
        xs.Sort(); ys.Sort(); columns=xs.Count-1; rows=ys.Count-1;
        pixels=new Vector2[(columns+1)*(rows+1)];
        var vertices=new Vector2[pixels.Length]; var triangles=new Godot.Collections.Array();
        for(int y=0;y<=rows;y++) for(int x=0;x<=columns;x++)
        {
            int i=y*(columns+1)+x;
            pixels[i]=new(xs[x],ys[y]);
            vertices[i]=new(x,y);
            if(x<columns&&y<rows){triangles.Add(new[]{i,i+1,i+columns+2});triangles.Add(new[]{i,i+columns+2,i+columns+1});}
        }
        var mesh=new Polygon2D {Name=name,Texture=Appearance.PartsAtlas,UV=pixels,Polygon=vertices,Polygons=triangles,TextureFilter=TextureFilterEnum.LinearWithMipmaps};
        // 生成PNG在透明背景有低Alpha残留。只剔除几乎不可见的背景，保留毛边；不改写原图。
        mesh.Material=HorseSurfaceMaterial.Shared;
        _layers.AddChild(mesh); return mesh;
    }
    private void SetBone(int id,Transform2D transform)
    {
        _poses[id]=transform;
        int parent=HorseSkeletonDefinition.Parent(id);
        _bones[id].Transform=parent<0?transform:_poses[parent].AffineInverse()*transform;
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
    private readonly record struct MuscleBinding(float Weight,float Along,float Across);
    private MuscleBinding BindMuscle(Vector2 pixel,bool front)
    {
        // 原图肩/臀下缘属于上臂/股骨肌肉。鞍具、马背、腹部与上颈不参与这份绑定。
        float hind=(1-Mathf.SmoothStep(440,490,pixel.X))*Mathf.SmoothStep(220,420,pixel.Y);
        float gearBottom=Mathf.SmoothStep(403,430,pixel.Y);
        float foreWeight=Mathf.SmoothStep(Mathf.Lerp(852,790,gearBottom),Mathf.Lerp(905,850,gearBottom),pixel.X)
            *(1-Mathf.SmoothStep(1020,1100,pixel.X))*Mathf.SmoothStep(220,440,pixel.Y);
        Vector2 start=HorseArtAtlas.MuscleRoot(front), end=HorseArtAtlas.MuscleEnd(front);
        Vector2 sourceAxis=end-start;
        float t=(pixel-start).Dot(sourceAxis)/sourceAxis.LengthSquared();
        Vector2 sourceNormal=new Vector2(-sourceAxis.Y,sourceAxis.X).Normalized();
        float offset=(pixel-start).Dot(sourceNormal)*.073f*Appearance.BodyWidth;
        float taper=Mathf.Lerp(1,.72f,Mathf.Clamp(t,0,1));
        return new(front?foreWeight:hind,t,offset*taper);
    }
    private static Vector2 MusclePoint(MuscleBinding binding,HorseLocomotion.LegPose pose,Vector2 normal) =>
        pose.Root+(pose.Proximal-pose.Root)*binding.Along+normal*binding.Across;
    private static Vector2 UpperNormal(HorseLocomotion.LegPose pose)
    {
        Vector2 axis=pose.Proximal-pose.Root;
        return new Vector2(-axis.Y,axis.X).Normalized();
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
            var q=HorseLocomotion.Sample(leg>=2,leg%2==1,phase,weight,Appearance.BodyWidth);
            int bone=HorseSkeletonDefinition.FirstLegBone+leg*HorseSkeletonDefinition.SegmentsPerLeg;
            if(leg==1) { _hindPose=q; _hindNormal=UpperNormal(q); }
            if(leg==3) { _forePose=q; _foreNormal=UpperNormal(q); }
            SetSegment(bone,q.Root,q.Proximal); SetSegment(bone+1,q.Proximal,q.Joint);
            SetSegment(bone+2,q.Joint,q.Fetlock); SetSegment(bone+3,q.Fetlock,q.Hoof);
            _limbSkins[leg].Apply(q,p);
        }
        for(int i=0;i<_bodyVertices.Length;i++)
        {
            Vector2 point=BodyPoint(_bodyPixels[i],p);
            // 权重和原图投影只在建模时求一次，逐帧只更新骨轴，保持已经修好的显示节拍。
            var hind=_hindBindings[i]; var fore=_foreBindings[i];
            if(hind.Weight>0) point=point.Lerp(MusclePoint(hind,_hindPose,_hindNormal),hind.Weight);
            if(fore.Weight>0) point=point.Lerp(MusclePoint(fore,_forePose,_foreNormal),fore.Weight);
            _bodyVertices[i]=point;
        }
        BodySkin.Polygon=_bodyVertices;
        for(int i=0;i<_tackVertices.Length;i++) _tackVertices[i]=p.SkinPoint(HorseArtAtlas.BodyPoint(HorseArtAtlas.TackOutline[i],Appearance.BodyWidth));
        TackSkin.Polygon=_tackVertices;
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
