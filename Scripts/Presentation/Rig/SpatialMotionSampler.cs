using Godot;

/// <summary>固定骨长的空间姿态采样。只计算二维美术需要的深度，不创建三维世界或更改命中坐标。</summary>
internal static class SpatialMotionSampler
{
    internal struct Pose
    {
        public Vector3 Root, Grip, FreeHand, FarFoot, NearFoot;
        public Vector2 BladeAngles;
        public float Yaw, Twist, Lean, TwoHand, FarYaw, NearYaw, FarRoll, NearRoll;
        public static Pose From(CharacterSpatialPoseConfig key) => new()
        {
            Root=key.Root, Grip=key.Grip, FreeHand=key.FreeHand, FarFoot=key.FarFoot, NearFoot=key.NearFoot,
            BladeAngles=key.BladeAngles, Yaw=key.Yaw, Twist=key.ChestTwist, Lean=key.Lean,
            TwoHand=key.TwoHandWeight, FarYaw=key.FarFootYaw, NearYaw=key.NearFootYaw,
            FarRoll=key.FarFootRoll, NearRoll=key.NearFootRoll
        };
    }

    internal sealed class SkeletonPose
    {
        public readonly Vector3[] Points = new Vector3[20];
        public Vector3 Blade;
        public float Yaw, Lean, GripWeight, FarYaw, NearYaw, FarRoll, NearRoll;
        public float FootError, GripError;
    }

    /// <summary>连续角不取最短路径，保留完整旋转；位置使用局部三次曲线，足底高度不允许越过地面。</summary>
    internal static Pose Sample(CharacterSpatialMotionConfig clip, float time)
    {
        var keys=clip.Keys;
        if (time<=keys[0].Time) return Pose.From(keys[0]);
        int last=keys.Count-1;
        if (time>=keys[last].Time) return Pose.From(keys[last]);
        int index=0;
        while (index<last-1 && keys[index+1].Time<time) index++;
        var a=Pose.From(keys[index]); var b=Pose.From(keys[index+1]);
        var before=Pose.From(keys[Mathf.Max(0,index-1)]); var after=Pose.From(keys[Mathf.Min(last,index+2)]);
        float duration=keys[index+1].Time-keys[index].Time;
        float t=(time-keys[index].Time)/duration;
        float left=duration/Mathf.Max(.001f,keys[index+1].Time-keys[Mathf.Max(0,index-1)].Time);
        float right=duration/Mathf.Max(.001f,keys[Mathf.Min(last,index+2)].Time-keys[index].Time);
        float H(float p, float q, float prev, float next)
        {
            float t2=t*t,t3=t2*t;
            return (2*t3-3*t2+1)*p+(t3-2*t2+t)*(q-prev)*left
                +(-2*t3+3*t2)*q+(t3-t2)*(next-p)*right;
        }
        Vector3 V(Vector3 p,Vector3 q,Vector3 prev,Vector3 next) => new(H(p.X,q.X,prev.X,next.X),H(p.Y,q.Y,prev.Y,next.Y),H(p.Z,q.Z,prev.Z,next.Z));
        var result=new Pose
        {
            Root=V(a.Root,b.Root,before.Root,after.Root), Grip=V(a.Grip,b.Grip,before.Grip,after.Grip),
            FreeHand=V(a.FreeHand,b.FreeHand,before.FreeHand,after.FreeHand),
            FarFoot=V(a.FarFoot,b.FarFoot,before.FarFoot,after.FarFoot), NearFoot=V(a.NearFoot,b.NearFoot,before.NearFoot,after.NearFoot),
            Yaw=H(a.Yaw,b.Yaw,before.Yaw,after.Yaw), Twist=H(a.Twist,b.Twist,before.Twist,after.Twist),
            Lean=H(a.Lean,b.Lean,before.Lean,after.Lean),
            BladeAngles=new(H(a.BladeAngles.X,b.BladeAngles.X,before.BladeAngles.X,after.BladeAngles.X),H(a.BladeAngles.Y,b.BladeAngles.Y,before.BladeAngles.Y,after.BladeAngles.Y)),
            TwoHand=Mathf.Lerp(a.TwoHand,b.TwoHand,t), FarYaw=H(a.FarYaw,b.FarYaw,before.FarYaw,after.FarYaw),
            NearYaw=H(a.NearYaw,b.NearYaw,before.NearYaw,after.NearYaw),
            FarRoll=Mathf.Lerp(a.FarRoll,b.FarRoll,t), NearRoll=Mathf.Lerp(a.NearRoll,b.NearRoll,t)
        };
        // 两端均为支撑时锁定高度；插值不能制造脚底穿地或无意抬脚。
        result.FarFoot.Y=a.FarFoot.Y==0 && b.FarFoot.Y==0 ? 0 : Mathf.Min(0,result.FarFoot.Y);
        result.NearFoot.Y=a.NearFoot.Y==0 && b.NearFoot.Y==0 ? 0 : Mathf.Min(0,result.NearFoot.Y);
        return result;
    }

    internal static Vector2 Project(Vector3 point) => new(point.X,point.Y+point.Z*.16f);
    internal static Basis Yaw(float degrees) => new(Vector3.Up,Mathf.DegToRad(degrees));

    /// <summary>先求支撑、髋胸和兵器，再解算四肢；整条骨链在三维空间固定长度，画面缩短来自投影。</summary>
    internal static SkeletonPose Solve(Pose pose,CharacterModelConfig model)
    {
        var result=new SkeletonPose { Yaw=pose.Yaw+pose.Twist, Lean=pose.Lean, GripWeight=pose.TwoHand,
            FarYaw=pose.FarYaw,NearYaw=pose.NearYaw,FarRoll=pose.FarRoll,NearRoll=pose.NearRoll };
        var p=result.Points;
        Basis hips=Yaw(pose.Yaw), chest=new Basis(Vector3.Back,Mathf.DegToRad(pose.Lean))*Yaw(result.Yaw);
        float upper=model.JointPositions[4].Length(), lower=model.JointPositions[5].Length();
        float thigh=model.ThighLength,shin=model.LowerLegLength-model.FootHeight;
        Vector3 root=new(pose.Root.X,-model.PelvisHeight+pose.Root.Y,pose.Root.Z);
        Vector3[] soles={pose.FarFoot,pose.NearFoot};
        for(int side=0;side<2;side++)
        {
            Vector3 hip=hips*new Vector3((side==0?-1:1)*model.HipHalfWidth,2,0);
            Vector3 ankle=soles[side]-new Vector3(0,model.FootHeight,0);
            float horizontal=Mathf.Pow(ankle.X-root.X-hip.X,2)+Mathf.Pow(ankle.Z-root.Z-hip.Z,2);
            float height=Mathf.Sqrt(Mathf.Max(1,Mathf.Pow(thigh+shin-.3f,2)-horizontal));
            root.Y=Mathf.Max(root.Y,ankle.Y-hip.Y-height);
        }
        p[0]=root;
        p[1]=root+new Basis(Vector3.Back,Mathf.DegToRad(pose.Lean*.65f))*new Vector3(0,model.JointPositions[1].Y,0);
        p[2]=p[1]+chest*new Vector3(0,model.JointPositions[2].Y,1);
        p[3]=p[1]+chest*new Vector3(-14,-2,0);
        p[6]=p[1]+chest*new Vector3(14,-3,0);
        p[9]=root+hips*new Vector3(-model.HipHalfWidth,2,0);
        p[11]=root+hips*new Vector3(model.HipHalfWidth,2,0);
        p[13]=p[1]+chest*new Vector3(0,-11,-6);
        p[15]=p[2]+chest*new Vector3(0,-25,0);
        float az=Mathf.DegToRad(pose.BladeAngles.X),el=Mathf.DegToRad(pose.BladeAngles.Y);
        result.Blade=(chest*new Vector3(Mathf.Sin(az)*Mathf.Cos(el),-Mathf.Sin(el),Mathf.Cos(az)*Mathf.Cos(el))).Normalized();
        Vector3 grip=p[1]+chest*pose.Grip;
        float separation=10;
        // 双手约束共同移动兵器，不能分别截断两条手臂而让其中一只手脱柄。
        for(int iteration=0;iteration<10;iteration++)
        {
            grip=Reach(p[3],grip,12,upper+lower-.7f);
            if(pose.TwoHand>.001f)
            {
                Vector3 secondary=grip+result.Blade*separation;
                Vector3 corrected=Reach(p[6],secondary,12,upper+lower-.7f);
                grip+=(corrected-secondary)*pose.TwoHand;
            }
        }
        p[14]=grip;
        Vector3 secondaryTarget=(p[1]+chest*pose.FreeHand).Lerp(grip+result.Blade*separation,pose.TwoHand);
        p[5]=Reach(p[3],grip,12,upper+lower-.3f);
        p[8]=Reach(p[6],secondaryTarget,12,upper+lower-.3f);
        p[4]=Joint(p[3],p[5],upper,lower,chest*new Vector3(-1,.65f,-.15f));
        p[7]=Joint(p[6],p[8],upper,lower,chest*new Vector3(1,.65f,-.15f));
        result.GripError=p[5].DistanceTo(grip);
        if(pose.TwoHand>.999f) result.GripError=Mathf.Max(result.GripError,p[8].DistanceTo(grip+result.Blade*separation));
        for(int side=0;side<2;side++)
        {
            int hip=side==0?9:11,knee=hip+1;
            Vector3 target=soles[side]-new Vector3(0,model.FootHeight,0);
            p[16+side]=Reach(p[hip],target,12,thigh+shin-.15f);
            p[knee]=Joint(p[hip],p[16+side],thigh,shin,hips*new Vector3(side==0?-.12f:.12f,0,1));
            p[18+side]=p[16+side]+new Vector3(0,model.FootHeight,0);
            result.FootError=Mathf.Max(result.FootError,p[18+side].DistanceTo(soles[side]));
        }
        return result;
    }

    private static Vector3 Reach(Vector3 root,Vector3 end,float min,float max)
    {
        Vector3 v=end-root;
        return root+(v.LengthSquared()<.0001f?Vector3.Down:v.Normalized())*Mathf.Clamp(v.Length(),min,max);
    }

    private static Vector3 Joint(Vector3 root,Vector3 end,float first,float second,Vector3 pole)
    {
        Vector3 delta=end-root; float distance=delta.Length(); Vector3 direction=delta/distance;
        float along=(first*first-second*second+distance*distance)/(2*distance);
        Vector3 bend=pole-direction*pole.Dot(direction);
        if(bend.LengthSquared()<.001f) bend=direction.Cross(Vector3.Right);
        bend=bend.Normalized();
        return root+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,first*first-along*along));
    }
}
