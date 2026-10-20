using Godot;

/// <summary>同一条整腿图映射到四段定长骨轴，包含蹄；相邻骨段共用几何与UV截面。</summary>
public partial class HorseLimbSkin : Polygon2D
{
    private const int Rows=16, Columns=8, Stride=Columns+1, SegmentVertices=(Rows+1)*Stride;
    public const float FarShade=.68f;
    private readonly bool _front,_near;
    private static readonly float[] ForeRadii={6.2f,3.5f,1.7f,1.5f,2.7f};
    private static readonly float[] HindRadii={7.6f,4.6f,2.0f,1.5f,2.7f};
    private readonly Vector2[] _vertices=new Vector2[SegmentVertices*4];
    // 每条腿独享五个骨轴点与法线缓冲，连续运动时不在每帧制造临时数组。
    private readonly Vector2[] _posePoints=new Vector2[5], _poseNormals=new Vector2[5];
    public float MaximumJointGap { get; private set; }
    public float MinimumSampleAlpha { get; private set; }=1;
    public Vector2 RootSurfaceCenter => (_vertices[0]+_vertices[Columns])*.5f;
    public Vector2 SoleCenter { get; private set; }

    public HorseLimbSkin(Texture2D atlas,Image image,bool front,bool near)
    {
        _front=front; _near=near;
        Name=(near?"近":"远")+(front?"前":"后")+"腿连续皮肤";
        Texture=atlas; TextureFilter=TextureFilterEnum.LinearWithMipmaps; Material=HorseSurfaceMaterial.Shared; Antialiased=true;
        Vector2[] source=HorseArtAtlas.LegAnchors(front);
        Vector2[] sourceNormals=JointNormals(source);
        var uv=new Vector2[_vertices.Length]; var colors=new Color[_vertices.Length];
        var triangles=new Godot.Collections.Array();
        Rect2 window=front?HorseArtAtlas.Foreleg:HorseArtAtlas.Hindleg;
        for(int segment=0;segment<4;segment++) for(int row=0;row<=Rows;row++)
        {
            float t=row/(float)Rows;
            Vector2 center=source[segment].Lerp(source[segment+1],t);
            Vector2 normal=SectionNormal(source,sourceNormals,segment,t);
            // 蹄底使用水平截面，不能把斜系部的截面延伸到地面以下。
            if(segment==3) normal=normal.Lerp(Vector2.Left,Mathf.SmoothStep(.45f,1,t)).Normalized();
            var (left,right)=OpaqueSpan(image,window,center,normal);
            for(int col=0;col<=Columns;col++)
            {
                int i=segment*SegmentVertices+row*Stride+col;
                uv[i]=left.Lerp(right,col/(float)Columns);
                MinimumSampleAlpha=Mathf.Min(MinimumSampleAlpha,image.GetPixel((int)uv[i].X,(int)uv[i].Y).A);
                // 近侧上肢的主肌肉已由身体网格绑定；这里只保留末段重叠，接到肘/膝以下。
                // 不得完全隐藏第一段，否则伸展时两个不同法线的截面间会露出三角缝。
                float alpha=segment==0?(near?Mathf.SmoothStep(.55f,.92f,t):Mathf.SmoothStep(.03f,.80f,t)):1;
                float shade=near?1:FarShade;
                colors[i]=new(shade,shade,shade,alpha);
                if(row<Rows && col<Columns)
                {
                    triangles.Add(new[]{i,i+1,i+Stride+1}); triangles.Add(new[]{i,i+Stride+1,i+Stride});
                }
            }
        }
        UV=uv; VertexColors=colors; Polygons=triangles;
        for(int i=0;i<_vertices.Length;i++) _vertices[i]=new(i%Stride,i/Stride);
        Polygon=_vertices;
    }

    public void Apply(HorseLocomotion.LegPose pose,HorseBodyMotion.Pose body)
    {
        Vector2[] points=_posePoints, normals=_poseNormals;
        points[0]=pose.Root; points[1]=pose.Proximal; points[2]=pose.Joint; points[3]=pose.Fetlock; points[4]=pose.Hoof;
        JointNormals(points,normals);
        normals[0]=-(_front?body.Chest.X:body.Pelvis.X).Normalized();
        Vector2 skinOffset=body.LegSkinRoot(_front,_near)-pose.Root;
        for(int segment=0;segment<4;segment++) for(int row=0;row<=Rows;row++)
        {
            float t=row/(float)Rows;
            Vector2 center=points[segment].Lerp(points[segment+1],t);
            if(segment==0) center+=skinOffset*(1-Mathf.SmoothStep(0,.65f,t));
            Vector2 normal=SectionNormal(points,normals,segment,t);
            // 承重蹄底水平，摆动时随系部转动；蹄底中心就是求解器的地面接点。
            if(segment==3) normal=normal.Lerp(Vector2.Left.Rotated(pose.HoofAngle),Mathf.SmoothStep(.45f,1,t)).Normalized();
            float radius=Radius(segment,t);
            for(int col=0;col<=Columns;col++) _vertices[segment*SegmentVertices+row*Stride+col]=
                center+normal*(radius*(col*2f/Columns-1));
        }
        SoleCenter=(_vertices[^Stride]+_vertices[^1])*.5f;
        MaximumJointGap=0;
        for(int segment=0;segment<3;segment++) for(int col=0;col<=Columns;col++)
            MaximumJointGap=Mathf.Max(MaximumJointGap,_vertices[segment*SegmentVertices+Rows*Stride+col]
                .DistanceTo(_vertices[(segment+1)*SegmentVertices+col]));
        Polygon=_vertices;
    }

    private float Radius(int segment,float t)
    {
        float[] radii=_front?ForeRadii:HindRadii;
        float value=Mathf.Lerp(radii[segment],radii[segment+1],Mathf.SmoothStep(0,1,t));
        if(segment==2) value-=.25f*Mathf.Sin(t*Mathf.Pi);
        if(segment==3) value-=.45f*Mathf.Sin(t*Mathf.Pi);
        return value;
    }
    private static Vector2 Normal(Vector2 axis)=>new(-axis.Y,axis.X);
    private static Vector2[] JointNormals(Vector2[] points)
    {
        var normals=new Vector2[points.Length];
        JointNormals(points,normals);
        return normals;
    }
    private static void JointNormals(Vector2[] points,Vector2[] normals)
    {
        normals[0]=Normal((points[1]-points[0]).Normalized());
        normals[^1]=Normal((points[^1]-points[^2]).Normalized());
        for(int i=1;i<points.Length-1;i++) normals[i]=Normal(((points[i]-points[i-1]).Normalized()+(points[i+1]-points[i]).Normalized()).Normalized());
    }
    private static Vector2 SectionNormal(Vector2[] points,Vector2[] normals,int segment,float t)
    {
        Vector2 straight=Normal((points[segment+1]-points[segment]).Normalized());
        return (t<.25f?normals[segment].Lerp(straight,Mathf.SmoothStep(0,.25f,t)):
            t>.75f?straight.Lerp(normals[segment+1],Mathf.SmoothStep(.75f,1,t)):straight).Normalized();
    }
    private static (Vector2 Left,Vector2 Right) OpaqueSpan(Image image,Rect2 window,Vector2 center,Vector2 normal)
    {
        int extent=(int)window.Size.Length(),start=0,bestStart=0,bestEnd=0;
        float best=float.PositiveInfinity; bool inside=false;
        for(int step=-extent;step<=extent;step++)
        {
            Vector2 p=center+normal*step;
            bool solid=window.HasPoint(p)&&image.GetPixel((int)p.X,(int)p.Y).A>.92f;
            if(solid&&!inside){start=step;inside=true;}
            if((!solid||step==extent)&&inside)
            {
                int end=step-1; float distance=start<=0&&end>=0?0:Mathf.Min(Mathf.Abs(start),Mathf.Abs(end));
                if(end-start>=4&&distance<best){bestStart=start;bestEnd=end;best=distance;}
                inside=false;
            }
        }
        if(!float.IsFinite(best)) throw new System.InvalidOperationException($"V2马腿无实色截面：{center}");
        return(center+normal*(bestStart+2),center+normal*(bestEnd-2));
    }
}
