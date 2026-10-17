using Godot;
using System.Collections.Generic;

/// <summary>32骨全身马架：骨链驱动彩绘拆件，胸腹连续蒙皮，绘制层与骨骼层级分离。</summary>
public partial class HorsePaintedRig : Node2D
{
    public HorseAppearance Appearance { get; set; }
    public Skeleton2D Skeleton { get; private set; }
    public IReadOnlyList<Bone2D> Bones => _bones;
    public IReadOnlyList<Sprite2D> Parts => _parts;
    public IReadOnlyList<HorseLimbSkin> LimbSkins => _limbSkins;
    public Vector2 SaddleWorld => ToGlobal(_saddle);
    public Vector2 BridleWorld => _head.ToGlobal(new Vector2(1288,178)-HorseArtAtlas.Regions[5].Position);
    public Vector2 HoofWorld(bool front,bool near)
    {
        int leg=(front?2:0)+(near?1:0);
        return _hooves[leg].ToGlobal(_solePixels[leg]-HorseArtAtlas.Regions[_hoofParts[leg]].Position);
    }
    private readonly Bone2D[] _bones = new Bone2D[32];
    private readonly Transform2D[] _poses = new Transform2D[32];
    private readonly List<Sprite2D> _parts = new();
    private readonly List<(Sprite2D Sprite,int Bone,Transform2D Offset)> _bindings = new();
    private readonly Sprite2D[] _hooves = new Sprite2D[4];
    private readonly HorseLimbSkin[] _limbSkins = new HorseLimbSkin[4];
    private Image _sourceImage;
    private readonly int[] _hoofParts = {21,13,17,9};
    private readonly Vector2[] _solePixels = {new(825,900),new(351,682),new(1282,681),new(824,455)};
    private readonly int[] _parents = { -1,0,0,0,0,4,5,6,6,1,9,10,5,4,0,1,
        1,16,17,18,1,20,21,22,2,24,25,26,3,28,29,30 };
    private Node2D _layers;
    private Polygon2D _barrel;
    private Vector2[] _barrelRest;
    private Vector2 _saddle;
    private Sprite2D _head;

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
            (_parents[i]<0 ? (Node)Skeleton : _bones[_parents[i]]).AddChild(_bones[i]);
        }
        _layers=new Node2D {Name="彩绘层"}; AddChild(_layers);
        // 全部保持Z=0，按兄弟顺序分层，以免马腿穿到地图地板下或盖住其它YSort角色。
        _sourceImage=Appearance.PartsAtlas.GetImage();
        if(_sourceImage.IsCompressed()) _sourceImage.Decompress();
        AddLeg(0); AddLeg(2);
        BindAxis(22,9,new(1027,724),new(1037,855),16,.63f);
        BindAxis(23,10,new(1248,729),new(1304,928),16,.83f);
        BuildBarrel();
        BindAxis(3,4,new(840,197),new(854,49),16,.90f);
        BindAxis(4,5,new(1010,197),new(1090,58),12.2f,.82f);
        _head=BindFixed(5,6,new(1211,89),new(.103f,.103f));
        // 青骢原图头部已带双耳；采样时剔除耳尖区，再由独立耳片覆盖连接。
        if(Appearance.Dappled)
        {
            var material=new ShaderMaterial {Shader=new Shader {Code="""
                shader_type canvas_item;
                void fragment(){ vec2 pixel=UV/TEXTURE_PIXEL_SIZE; if(pixel.y<57.0 && pixel.x>1180.0) discard; }
                """}};
            _head.Material=material;
        }
        BindAxis(28,7,new(1044,1090),new(1019,969),6,.80f);
        BindAxis(27,8,new(864,1088),new(885,964),7,.82f);
        var upperMane=BindAxis(25,12,new(406,938),new(540,1080),12,Appearance.LongMane?.62f:.30f);
        var lowerMane=BindAxis(26,13,new(605,947),new(745,1090),16,Appearance.LongMane?.60f:.27f);
        // 两束鬃毛的包围盒相交，沿实际空隙限制采样，防止另一束毛尖混进来。
        upperMane.Material=new ShaderMaterial {Shader=new Shader {Code="""
            shader_type canvas_item;
            void fragment(){ vec2 p=UV/TEXTURE_PIXEL_SIZE; if(p.x>530.0+(p.y-900.0)*0.43) discard; }
            """}};
        lowerMane.Material=new ShaderMaterial {Shader=new Shader {Code="""
            shader_type canvas_item;
            void fragment(){ vec2 p=UV/TEXTURE_PIXEL_SIZE; if(p.x<530.0+(p.y-900.0)*0.43) discard; }
            """}};
        AddLeg(1); AddLeg(3);
        _sourceImage.Dispose(); _sourceImage=null;
        // 肩臀已包含在连续躯干中，不再叠加独立晃动的圆形覆盖片。
        BindFixed(24,14,new(178,959),new(.091f,.091f));
        BindFixed(29,15,new(1256,1028),new(Appearance.SaddleBags?.050f:.041f,Appearance.SaddleBags?.050f:.041f));
        ApplyPose(0,0);
        foreach(var bone in _bones) bone.Rest=bone.Transform;
    }

    private Sprite2D BindFixed(int part,int bone,Vector2 sourceAnchor,Vector2 scale)
    {
        var basis=new Transform2D(new(scale.X,0),new(0,scale.Y),Vector2.Zero);
        basis.Origin=basis*(HorseArtAtlas.Regions[part].Position-sourceAnchor);
        return AddPart(part,bone,basis);
    }
    private Sprite2D BindAxis(int part,int bone,Vector2 sourceStart,Vector2 sourceEnd,float length,float thickness=1)
    {
        Vector2 axis=sourceEnd-sourceStart;
        float scale=length/axis.Length();
        var basis=new Transform2D(new(scale,0),new(0,scale*thickness),Vector2.Zero)*new Transform2D(-axis.Angle(),Vector2.Zero);
        basis.Origin=basis*(HorseArtAtlas.Regions[part].Position-sourceStart);
        return AddPart(part,bone,basis);
    }
    private Sprite2D AddPart(int part,int bone,Transform2D offset)
    {
        var sprite=new Sprite2D {Name=$"部件{part:00}",Texture=HorseArtAtlas.Part(Appearance.PartsAtlas,part),Centered=false,TextureFilter=TextureFilterEnum.Linear};
        _layers.AddChild(sprite); _parts.Add(sprite); _bindings.Add((sprite,bone,offset));
        return sprite;
    }
    private void AddLeg(int leg)
    {
        bool front=leg>=2; int bone=16+leg*4;
        int[] ids=leg switch {0=>new[]{18,19,20,21},1=>new[]{10,11,12,13},2=>new[]{14,15,16,17},_=>new[]{6,7,8,9}};
        Vector2[][] anchors={
            new[]{new Vector2(115,733),new Vector2(123,865),new Vector2(385,716),new Vector2(343,877),new Vector2(581,724),new Vector2(583,847)},
            new[]{new Vector2(1028,278),new Vector2(1044,432),new Vector2(1243,277),new Vector2(1278,444),new Vector2(120,495),new Vector2(119,623)},
            new[]{new Vector2(560,512),new Vector2(613,662),new Vector2(799,508),new Vector2(809,672),new Vector2(1010,509),new Vector2(1012,630)},
            new[]{new Vector2(112,271),new Vector2(186,426),new Vector2(367,267),new Vector2(357,439),new Vector2(584,267),new Vector2(580,392)}
        };
        var a=anchors[leg]; var length=HorseLocomotion.Lengths(front);
        _limbSkins[leg]=new HorseLimbSkin(Appearance.PartsAtlas,_sourceImage,front,leg%2==1,ids,a);
        _layers.AddChild(_limbSkins[leg]);
        // 蹄底保持水平；球节锚与落蹄点用带剪切的绑定同时命中，不能把整个蹄当圆棍旋转。
        Vector2 fetlockPixel=leg switch {0=>new(801,779),1=>new(315,555),2=>new(1249,556),_=>new(800,312)};
        Vector2 sole=_solePixels[leg], delta=sole-fetlockPixel;
        Vector2 toe=Vector2.FromAngle(Mathf.DegToRad(65))*length.W;
        float sx=.040f, sy=toe.Y/delta.Y;
        var hoofBasis=new Transform2D(new(sx,0),new((toe.X-sx*delta.X)/delta.Y,sy),Vector2.Zero);
        hoofBasis.Origin=hoofBasis*(HorseArtAtlas.Regions[ids[3]].Position-fetlockPixel);
        _hooves[leg]=AddPart(ids[3],bone+3,hoofBasis);
        if(leg%2==0) _hooves[leg].Modulate=new Color(.78f,.78f,.78f);
    }
    private void BuildBarrel()
    {
        const int columns=8,rows=3;
        var vertices=new Vector2[(columns+1)*(rows+1)]; var uv=new Vector2[vertices.Length];
        var polygons=new Godot.Collections.Array();
        Rect2 region=HorseArtAtlas.Regions[0];
        for(int y=0;y<=rows;y++) for(int x=0;x<=columns;x++)
        {
            int i=y*(columns+1)+x; float u=(float)x/columns,v=(float)y/rows;
            vertices[i]=new(Mathf.Lerp(-32,27,u)*Appearance.BodyWidth,Mathf.Lerp(-45,-22,v));
            uv[i]=region.Position+new Vector2(u*region.Size.X,v*region.Size.Y);
            if(x<columns&&y<rows)
            {
                polygons.Add(new[]{i,i+1,i+columns+2}); polygons.Add(new[]{i,i+columns+2,i+columns+1});
            }
        }
        _barrelRest=vertices;
        // Polygon2D使用原图绝对UV；AtlasTexture的矩形裁剪只适用于Sprite2D，不能直接复用。
        _barrel=new Polygon2D {Name="胸腹连续皮肤",Polygon=vertices,UV=uv,Polygons=polygons,Texture=Appearance.PartsAtlas,TextureFilter=TextureFilterEnum.Linear};
        _layers.AddChild(_barrel);
    }
    private void SetBone(int id,Transform2D transform)
    {
        _poses[id]=transform;
        _bones[id].Transform=_parents[id]<0 ? transform : _poses[_parents[id]].AffineInverse()*transform;
    }
    private void SetSegment(int id,Vector2 start,Vector2 end)
    {
        SetBone(id,new Transform2D((end-start).Angle(),start));
        _bones[id].SetLength(start.DistanceTo(end));
    }
    public void ApplyPose(float phase,float weight)
    {
        if(Skeleton==null) return;
        var p=HorseBodyMotion.Sample(phase,weight,Appearance.BodyWidth);
        _saddle=p.Saddle;
        SetBone(0,p.Chest*new Transform2D(0,new(8,-35)));
        SetBone(1,p.Pelvis*new Transform2D(0,new(-20*Appearance.BodyWidth,-35)));
        SetBone(2,p.FarShoulder*new Transform2D(0,new(18*Appearance.BodyWidth,-35)));
        SetBone(3,p.NearShoulder*new Transform2D(0,new(18*Appearance.BodyWidth,-35)));
        SetSegment(4,p.NeckRoot,p.NeckMiddle); SetSegment(5,p.NeckMiddle,p.Poll);
        SetBone(6,p.Head*new Transform2D(0,new(33,-59)));
        float ear=Mathf.Sin((phase-.25f)*Mathf.Tau)*.045f*weight;
        Vector2 farEar=p.Head*new Vector2(34,-62),nearEar=p.Head*new Vector2(38,-61);
        SetSegment(7,farEar,farEar+Vector2.Up.Rotated(p.Head.Rotation-ear)*6);
        SetSegment(8,nearEar,nearEar+Vector2.Up.Rotated(p.Head.Rotation+ear)*7);
        // 尾巴从骨盆顺次传递，运动增量有小相差；停下回收至同一中立姿态。
        float trail=weight*(.25f+.08f*Mathf.Sin((phase-.12f)*Mathf.Tau));
        Vector2 tailMid=p.TailRoot+new Vector2(-4,15.5f).Rotated(trail+p.Pelvis.Rotation);
        Vector2 tailTip=tailMid+new Vector2(-3,15.7f).Rotated(trail+.09f*weight*Mathf.Sin((phase-.24f)*Mathf.Tau));
        SetSegment(9,p.TailRoot,tailMid); SetSegment(10,tailMid,tailTip); SetSegment(11,tailTip,tailTip+new Vector2(-2,4));
        float maneLag=.025f*weight*Mathf.Sin((phase-.15f)*Mathf.Tau);
        SetSegment(12,p.Poll+new Vector2(-1,-1),(p.NeckMiddle+new Vector2(-3,1)).Lerp(p.Poll,.03f));
        SetSegment(13,p.NeckMiddle+new Vector2(-3,-1),p.NeckRoot+new Vector2(-6,3).Rotated(maneLag));
        SetBone(14,new Transform2D(p.ChestAngle,p.Saddle));
        SetBone(15,p.Pelvis*new Transform2D(0,new(-15*Appearance.BodyWidth,-35)));
        for(int leg=0;leg<4;leg++)
        {
            var q=HorseLocomotion.Sample(leg>=2,leg%2==1,phase,weight,Appearance.BodyWidth);
            int bone=16+leg*4;
            SetSegment(bone,q.Root,q.Proximal); SetSegment(bone+1,q.Proximal,q.Joint); SetSegment(bone+2,q.Joint,q.Fetlock);
            SetBone(bone+3,new Transform2D(q.HoofAngle,q.Fetlock)); _bones[bone+3].SetLength(5);
            _limbSkins[leg].Apply(q,p);
        }
        foreach(var binding in _bindings) binding.Sprite.Transform=_poses[binding.Bone]*binding.Offset;
        var skin=new Vector2[_barrelRest.Length];
        for(int i=0;i<skin.Length;i++) skin[i]=p.SkinPoint(_barrelRest[i]);
        _barrel.Polygon=skin;
    }
}
