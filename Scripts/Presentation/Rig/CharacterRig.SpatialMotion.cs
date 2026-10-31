using Godot;
using System;
using System.Collections.Generic;

/// <summary>把固定骨长的空间动作投影到现有二维拆件；逐部件换侧背面、排序，并用柔性网格跟随披风。</summary>
public partial class CharacterRig
{
    private Transform2D[] _beforeSpatialBones;
    private Texture2D[] _beforeSpatialTextures;
    private Texture2D[] _beforeSpatialFeet;
    private readonly List<Node2D> _normalLayerOrder = new();
    private AtlasTexture[] _turnTextures;
    private MeshInstance2D _turnCape;
    private SpatialMotionSampler.SkeletonPose _spatialPose;
    private float _spatialWeight;
    private Vector2 _spatialTip;
    public bool SpatialMotionActive { get; private set; }
    public float SpatialMotionTime { get; private set; }
    public float SpatialMotionWeight => _spatialWeight;
    public float SpatialFootError => _spatialPose?.FootError ?? 0;
    public float SpatialGripError => _spatialPose?.GripError ?? 0;
    public IReadOnlyList<Vector3> SpatialJointPositions => _spatialPose?.Points ?? Array.Empty<Vector3>();

    /// <summary>空间投影不污染下一帧的平面IK绑定，也不把临时换图写回共享资源。</summary>
    private void RestoreSpatialBinding()
    {
        if (!SpatialMotionActive) return;
        for(int i=0;i<_bones.Length;i++)
        {
            _bones[i].Transform=_beforeSpatialBones[i];
            _partByBone[i].Texture=_beforeSpatialTextures[i];
            _partByBone[i].Visible=(i!=15 || Actor.Presentation.Model.CrestVisible && !Actor.Presentation.Model.ProceduralCrests)
                && (i!=14 || !Actor.IsMounted);
            _partByBone[i].FlipH=i==12 && Actor.Presentation.Model.AlignBootsForward
                || i==10 && Actor.Presentation.Model.FlipFarBootHorizontal;
        }
        for(int i=0;i<2;i++) { _referenceFeet[i].Texture=_beforeSpatialFeet[i]; _referenceFeet[i].FlipH=i==0?Actor.Presentation.Model.FlipFarBootHorizontal:Actor.Presentation.Model.AlignBootsForward; }
        _turnCape.Visible=false;
        _drawOrderedParts.Clear();
        for(int i=0;i<_normalLayerOrder.Count;i++)
        {
            _drawLayers.MoveChild(_normalLayerOrder[i],i);
            if(_normalLayerOrder[i] is Sprite2D sprite) _drawOrderedParts.Add(sprite);
        }
        SpatialMotionActive=false;
    }

    private void InitializeSpatialArtwork()
    {
        if(_beforeSpatialBones!=null) return;
        _beforeSpatialBones=new Transform2D[16]; _beforeSpatialTextures=new Texture2D[16]; _beforeSpatialFeet=new Texture2D[2];
        foreach(Node2D layer in _drawLayers.GetChildren()) _normalLayerOrder.Add(layer);
        var model=Actor.Presentation.Model;
        _turnTextures=new AtlasTexture[16];
        if(model.TurnAtlas!=null && model.TurnRegions.Count==16)
            for(int i=0;i<16;i++) _turnTextures[i]=new AtlasTexture { Atlas=model.TurnAtlas, Region=model.TurnRegions[i] };
        _turnCape=new MeshInstance2D { Name="转身披风网格",Visible=false,TextureFilter=TextureFilterEnum.Linear };
        _drawLayers.AddChild(_turnCape);
    }

    /// <summary>前摇、生效、收势分别映射源动作时间，普攻可以引用完整连招的一个片段。</summary>
    private float SpatialClipTime(CharacterActionAnimationConfig action)
    {
        var spec=Actor.Spec; float time=Actor.ActionTime;
        float end=action.ClipEnd>action.ClipStart?action.ClipEnd:action.SpatialClip.Duration;
        if(time<spec.Prepare) return Mathf.Lerp(action.ClipStart,action.ClipPrepareEnd,time/Mathf.Max(.001f,spec.Prepare));
        if(time<spec.Prepare+spec.Active) return Mathf.Lerp(action.ClipPrepareEnd,action.ClipActiveEnd,(time-spec.Prepare)/Mathf.Max(.001f,spec.Active));
        return Mathf.Lerp(action.ClipActiveEnd,end,Mathf.Clamp((time-spec.Prepare-spec.Active)/Mathf.Max(.001f,spec.Recover),0,1));
    }

    private void UpdateSpatialMotion()
    {
        if(Actor.CurrentState!=Combatant.State.Attack || Actor.IsMounted) return;
        var action=Actor.Presentation.Animation.GetAction(Actor.CurrentMotion);
        var clip=action?.SpatialClip;
        if(clip==null || clip.Keys.Count<2) return;
        InitializeSpatialArtwork();
        for(int i=0;i<16;i++) { _beforeSpatialBones[i]=_bones[i].Transform; _beforeSpatialTextures[i]=_partByBone[i].Texture; }
        for(int i=0;i<2;i++) _beforeSpatialFeet[i]=_referenceFeet[i].Texture;
        SpatialMotionActive=true;
        SpatialMotionTime=SpatialClipTime(action);
        var pose=SpatialMotionSampler.Sample(clip,SpatialMotionTime);
        _spatialPose=SpatialMotionSampler.Solve(pose,Actor.Presentation.Model);
        _spatialWeight=AnimationCurves.Smooth(Actor.ActionTime/Mathf.Max(.001f,clip.BlendIn))
            *AnimationCurves.Smooth((Actor.Spec.Duration-Actor.ActionTime)/Mathf.Max(.001f,clip.BlendOut));
        DrawSpatialSkeleton(_spatialPose,pose,clip);
    }

    private void DrawSpatialSkeleton(SpatialMotionSampler.SkeletonPose pose,SpatialMotionSampler.Pose sample,CharacterSpatialMotionConfig clip)
    {
        var p=pose.Points; var model=Actor.Presentation.Model;
        Transform2D root=_drawLayers.GlobalTransform.AffineInverse()*Skeleton.GlobalTransform;
        Vector2 P(int i)=>SpatialMotionSampler.Project(p[i]);
        int[] ends={1,2,-1,4,5,-1,7,8,-1,10,16,12,17,-1,-1,-1};
        var draw=new List<(Node2D layer,float depth)>();
        float yaw=Mathf.PosMod(pose.Yaw+180,360)-180;
        bool back=Mathf.Abs(yaw)>125,side=Mathf.Abs(yaw)>55 && !back;
        float sideSign=Mathf.Sin(Mathf.DegToRad(yaw))<0?-1:1;
        float torsoWidth=Mathf.Sqrt(Mathf.Pow(Mathf.Cos(Mathf.DegToRad(yaw)),2)+.40f*Mathf.Pow(Mathf.Sin(Mathf.DegToRad(yaw)),2));
        for(int i=0;i<16;i++)
        {
            if(i is 13 or 15) { _partByBone[i].Visible=false; continue; }
            var sprite=_partByBone[i]; Transform2D baseline=sprite.Transform;
            int end=ends[i]; float angle=Mathf.DegToRad(pose.Lean),lengthScale=1;
            if(end>=0 && i!=0 && i!=1)
            {
                Vector2 v=P(end)-P(i); angle=v.Angle()-Mathf.Pi/2;
                lengthScale=Mathf.Max(.08f,v.Length()/p[i].DistanceTo(p[end]));
            }
            if(i is 0 or 1 or 2) angle=Mathf.DegToRad(pose.Lean*(i==0?.3f:i==2?.6f:1));
            if(i is 5 or 8) angle=(P(i)-P(i-1)).Angle()-Mathf.Pi/2;
            if(i==14)
            {
                Vector2 blade=SpatialMotionSampler.Project(pose.Blade);
                angle=blade.Angle()+Mathf.Pi/2; lengthScale=blade.Length();
            }
            Vector2 position=P(i);
            // 业务骨骼保留，调试线和兵器挂点与最终绘制一致；下一帧会恢复其平面绑定。
            var boneTransform=Skeleton.GlobalTransform*new Transform2D(angle,position);
            _bones[i].GlobalTransform=_bones[i].GlobalTransform.InterpolateWith(boneTransform,_spatialWeight);
            Transform2D art=_partOffsets[i]; float width=1;
            int alternate=-1;
            if(i is 0 or 1 or 2)
            {
                alternate=back?(i==0?5:i==1?3:1):side?(i==0?4:i==1?2:0):-1;
                width=torsoWidth;
            }
            else if(back) alternate=i is 3 or 6?6:i is 4 or 7?7:i is 9 or 11?8:i is 10 or 12?9:i is 5 or 8?13:-1;
            if(alternate>=0 && _turnTextures[alternate]!=null)
            {
                sprite.Texture=_turnTextures[alternate];
                Vector2 size=sprite.Texture.GetSize();
                Rect2 rect=model.PartRects[i];
                if(i is 0 or 1 or 2)
                {
                    width=side?1:torsoWidth;
                    float w=rect.Size.X*(side?.75f:1);
                    art=new Transform2D(0,new Vector2(w,rect.Size.Y)/size,0,new Vector2(-w*.5f,rect.Position.Y));
                }
                else
                {
                    float length=i is 5 or 8?9:p[i].DistanceTo(p[end]);
                    float w=i is 3 or 6?16:i is 4 or 7?12:i is 9 or 11?18:i is 5 or 8?8:14;
                    art=new Transform2D(0,new Vector2(w,length+3)/size,0,new Vector2(-w*.5f,-2));
                    if(i is 5 or 8) art.Origin=new Vector2(-4,-5);
                }
            }
            sprite.FlipH=(i is 0 or 1 or 2) && sideSign<0;
            var transform=root*new Transform2D(angle,position)*new Transform2D(0,new Vector2(width,lengthScale),0,Vector2.Zero)*art;
            sprite.Transform=baseline.InterpolateWith(transform,_spatialWeight);
            float depth=end>=0?(p[i].Z+p[end].Z)*.5f:p[i].Z;
            if(i==14) depth=p[14].Z+pose.Blade.Z*18;
            if(i is 5 or 8) depth+=1.5f;
            if(i is 0 or 1 or 2) depth+=1;
            draw.Add((sprite,depth));
        }
        _spatialTip=P(14)+SpatialMotionSampler.Project(pose.Blade)*_referenceFrame.Blade;
        for(int foot=0;foot<2;foot++)
        {
            var sprite=_referenceFeet[foot]; Transform2D baseline=sprite.Transform;
            float footYaw=foot==0?pose.FarYaw:pose.NearYaw;
            float roll=Mathf.DegToRad(foot==0?pose.FarRoll:pose.NearRoll);
            float front=Mathf.Cos(Mathf.DegToRad(footYaw));
            if(Mathf.Abs(front)>.72f && _turnTextures[10]!=null)
            {
                sprite.Texture=_turnTextures[front>0?10:11]; sprite.FlipH=false;
                Vector2 size=sprite.Texture.GetSize();
                var art=new Transform2D(0,new Vector2(12,10)/size,0,new Vector2(-6,model.FootHeight-10));
                sprite.Transform=baseline.InterpolateWith(root*new Transform2D(roll,P(16+foot))*art,_spatialWeight);
            }
            else
            {
                bool flip=Mathf.Sin(Mathf.DegToRad(footYaw))<0;
                sprite.FlipH=flip;
                var art=_referenceFootOffsets[foot];
                // 支撑脚以脚底为基准，旋转仅用于腾空脚，避免踝点稳定却靴底翘入地面。
                sprite.Transform=baseline.InterpolateWith(root*new Transform2D(roll,P(16+foot))*art,_spatialWeight);
            }
            draw.Add((sprite,p[16+foot].Z+.1f));
        }
        UpdateSpatialCape(pose,sample,clip);
        draw.Add((_turnCape,p[13].Z-1));
        draw.Sort((a,b)=>a.depth.CompareTo(b.depth));
        _drawOrderedParts.Clear();
        foreach(var item in draw)
        {
            _drawLayers.MoveChild(item.layer,_drawLayers.GetChildCount()-1);
            if(item.layer is Sprite2D sprite) _drawOrderedParts.Add(sprite);
        }
    }

    /// <summary>领口固定，披风各段依照稍早的身体转角拖后；暂停时采样时间不变，披风也不独自推进。</summary>
    private void UpdateSpatialCape(SpatialMotionSampler.SkeletonPose pose,SpatialMotionSampler.Pose sample,CharacterSpatialMotionConfig clip)
    {
        var model=Actor.Presentation.Model;
        AtlasTexture atlas=_turnTextures[15] ?? (AtlasTexture)_beforeSpatialTextures[13];
        _turnCape.Texture=atlas.Atlas; _turnCape.Visible=true;
        _turnCape.Transform=_drawLayers.GlobalTransform.AffineInverse()*Skeleton.GlobalTransform;
        _turnCape.Modulate=new Color(1,1,1,_spatialWeight);
        var points=new Vector3[28]; var uv=new Vector2[28]; var indices=new int[108];
        int index=0;
        for(int row=0;row<7;row++)
        {
            float q=row/6f;
            var delayed=SpatialMotionSampler.Sample(clip,Mathf.Max(0,SpatialMotionTime-q*.14f));
            float turn=Mathf.DegToRad(delayed.Yaw+delayed.Twist);
            float lag=Mathf.Clamp((sample.Yaw+sample.Twist-delayed.Yaw-delayed.Twist)*.018f,-1.1f,1.1f);
            for(int col=0;col<4;col++)
            {
                float u=col/3f;
                Vector3 local=new((u-.5f)*48*(.65f+.35f*q)+Mathf.Sin(lag)*q*20,q*62-Mathf.Abs(lag)*q*q*13,-q*6);
                Vector3 world=pose.Points[13]+new Basis(Vector3.Up,turn)*local;
                Vector2 projected=SpatialMotionSampler.Project(world);
                points[row*4+col]=new Vector3(projected.X,projected.Y,0);
                uv[row*4+col]=(atlas.Region.Position+new Vector2(u,q)*atlas.Region.Size)/atlas.Atlas.GetSize();
                if(row<6 && col<3)
                {
                    int start=row*4+col;
                    indices[index++]=start; indices[index++]=start+1; indices[index++]=start+4;
                    indices[index++]=start+1; indices[index++]=start+5; indices[index++]=start+4;
                }
            }
        }
        using var arrays=new Godot.Collections.Array(); arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex]=points; arrays[(int)Mesh.ArrayType.TexUV]=uv; arrays[(int)Mesh.ArrayType.Index]=indices;
        // 复用网格资源，避免每帧产生等待终结器释放的原生资源；截图共享同一份当前网格。
        var mesh=_turnCape.Mesh as ArrayMesh ?? new ArrayMesh();
        mesh.ClearSurfaces(); mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arrays); _turnCape.Mesh=mesh;
    }
}
