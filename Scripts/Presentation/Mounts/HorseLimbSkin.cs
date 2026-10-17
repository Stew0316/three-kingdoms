using Godot;

/// <summary>从肩髋到球节的一张连续皮肤。源图只提供材质，关节处共用几何边界，不能把弯曲图片当直骨旋转。</summary>
public partial class HorseLimbSkin : Polygon2D
{
    private const int RowsPerSegment=12;
    private const int Columns=6;
    private const int Stride=Columns+1;
    private const int SegmentVertices=(RowsPerSegment+1)*Stride;
    private readonly bool _front;
    private readonly bool _near;
    private readonly Vector2[] _vertices=new Vector2[SegmentVertices*3];
    public float MaximumJointGap { get; private set; }
    public float MinimumSampleAlpha { get; private set; }=1;
    public Vector2 RootSurfaceCenter => (_vertices[0]+_vertices[Columns])*.5f;

    public HorseLimbSkin(Texture2D atlas,Image image,bool front,bool near,int[] parts,Vector2[] anchors)
    {
        _front=front; _near=near;
        Name=(near?"近":"远")+(front?"前":"后")+"腿连续皮肤";
        Texture=atlas;
        TextureFilter=TextureFilterEnum.Linear;
        Antialiased=true;
        var uv=new Vector2[_vertices.Length];
        var colors=new Color[_vertices.Length];
        var triangles=new Godot.Collections.Array();
        for(int segment=0;segment<3;segment++)
        {
            Vector2 sourceStart=anchors[segment*2],sourceEnd=anchors[segment*2+1];
            Vector2 sourceNormal=Normal((sourceEnd-sourceStart).Normalized());
            for(int row=0;row<=RowsPerSegment;row++)
            {
                float t=row/(float)RowsPerSegment;
                Vector2 center=sourceStart.Lerp(sourceEnd,t);
                // 沿每条源横截线寻找该部件的实色连续区，消除原图自带折弯造成的空白。
                var (left,right)=OpaqueSpan(image,HorseArtAtlas.Regions[parts[segment]],center,sourceNormal);
                for(int col=0;col<=Columns;col++)
                {
                    int i=segment*SegmentVertices+row*Stride+col;
                    uv[i]=left.Lerp(right,col/(float)Columns);
                    MinimumSampleAlpha=Mathf.Min(MinimumSampleAlpha,image.GetPixel((int)uv[i].X,(int)uv[i].Y).A);
                    float shade=near?1:.78f;
                    // 根部仅在有躯干承托的重叠区融合；关节和下肢保持实色，不能靠透明遮掩断裂。
                    float alpha=segment==0 ? Mathf.SmoothStep(0,.28f,t) : 1;
                    colors[i]=new Color(shade,shade,shade,alpha);
                    if(row<RowsPerSegment && col<Columns)
                    {
                        triangles.Add(new[]{i,i+1,i+Stride+1});
                        triangles.Add(new[]{i,i+Stride+1,i+Stride});
                    }
                }
            }
        }
        UV=uv; VertexColors=colors; Polygons=triangles;
        // 绑定完成后由Apply写入真实姿态；这里给出非退化占位，避免节点进入树时三角化警告。
        for(int i=0;i<_vertices.Length;i++) _vertices[i]=new(i%Stride,i/Stride);
        Polygon=_vertices;
    }

    /// <summary>三个骨段共享两道关节截面。只改变表面朝向，骨轴及长度仍来自固定骨长链。</summary>
    public void Apply(HorseLocomotion.LegPose pose,HorseBodyMotion.Pose body)
    {
        Vector2[] points={pose.Root,pose.Proximal,pose.Joint,pose.Fetlock};
        var axes=new Vector2[3]; var normals=new Vector2[4];
        for(int i=0;i<3;i++) axes[i]=(points[i+1]-points[i]).Normalized();
        // 根边只随躯干，不跟着股骨/上臂转成圆盘；运动沿皮肤逐渐传到第一关节。
        normals[0]=-(_front?body.Chest.X:body.Pelvis.X).Normalized();
        for(int i=1;i<3;i++)
        {
            Vector2 sum=axes[i-1]+axes[i];
            normals[i]=Normal(sum.LengthSquared()>.001f ? sum.Normalized() : axes[i]);
        }
        normals[3]=Normal(axes[2]);
        Vector2 rootSkinOffset=body.LegSkinRoot(_front,_near)-pose.Root;
        for(int segment=0;segment<3;segment++)
        for(int row=0;row<=RowsPerSegment;row++)
        {
            float t=row/(float)RowsPerSegment;
            Vector2 center=points[segment].Lerp(points[segment+1],t);
            // 肩胛可以在胸廓上活动，但皮肤根边留在躯干上；到肘/膝前已完全随本条腿。
            if(segment==0) center+=rootSkinOffset*(1-Mathf.SmoothStep(0,.65f,t));
            Vector2 straight=Normal(axes[segment]);
            // 关节附近的圆滑表面过渡，骨段中间保持直线，避免整条腿像橡皮管。
            Vector2 normal=t<.28f ? normals[segment].Lerp(straight,Mathf.SmoothStep(0,.28f,t))
                : t>.72f ? straight.Lerp(normals[segment+1],Mathf.SmoothStep(.72f,1,t)) : straight;
            normal=normal.Normalized();
            float width=Radius(segment,t);
            for(int col=0;col<=Columns;col++)
            {
                int i=segment*SegmentVertices+row*Stride+col;
                _vertices[i]=center+normal*(width*(col*2f/Columns-1));
            }
        }
        MaximumJointGap=0;
        for(int segment=0;segment<2;segment++) for(int col=0;col<=Columns;col++)
            MaximumJointGap=Mathf.Max(MaximumJointGap,_vertices[segment*SegmentVertices+RowsPerSegment*Stride+col]
                .DistanceTo(_vertices[(segment+1)*SegmentVertices+col]));
        Polygon=_vertices;
    }

    // 源图与几何统一使用同向法线；向下的骨段取左侧，根截面才不会翻转。
    private static Vector2 Normal(Vector2 axis) => new(-axis.Y,axis.X);

    private float Radius(int segment,float t)
    {
        float[] radii=_front ? new[]{4.6f,2.7f,1.45f,1.25f} : new[]{6.1f,3.3f,1.7f,1.25f};
        float radius=Mathf.Lerp(radii[segment],radii[segment+1],Mathf.SmoothStep(0,1,t));
        // 管骨中段收细，腕/飞节和球节保留体积，但不另画铰链圆。
        if(segment==2) radius-=.30f*Mathf.Sin(t*Mathf.Pi);
        return radius;
    }

    private static (Vector2 Left,Vector2 Right) OpaqueSpan(Image image,Rect2 window,Vector2 center,Vector2 normal)
    {
        int extent=(int)window.Size.Length();
        float bestDistance=float.PositiveInfinity; int bestStart=0,bestEnd=0,start=0;
        bool inside=false;
        for(int step=-extent;step<=extent;step++)
        {
            Vector2 p=center+normal*step;
            bool opaque=window.HasPoint(p) && image.GetPixel((int)p.X,(int)p.Y).A>.93f;
            if(opaque && !inside) { start=step; inside=true; }
            if((!opaque || step==extent) && inside)
            {
                int end=step-1;
                float distance=start<=0 && end>=0 ? 0 : Mathf.Min(Mathf.Abs(start),Mathf.Abs(end));
                if(end-start>=4 && distance<bestDistance) {bestStart=start;bestEnd=end;bestDistance=distance;}
                inside=false;
            }
        }
        if(!float.IsFinite(bestDistance)) throw new System.InvalidOperationException($"马腿源图无实色截面：{center} / {window}");
        // 向内留两个源像素，线性过滤不会采入背景；原图保持不变。
        return (center+normal*(bestStart+2),center+normal*(bestEnd-2));
    }
}
