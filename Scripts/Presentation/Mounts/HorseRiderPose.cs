using Godot;

/// <summary>独立骑乘姿态采样；返回骑手自身骨架坐标，不依赖武将、城市或某套骨骼节点名。</summary>
public static class HorseRiderPose
{
    public readonly record struct Pose(float Waist,float Chest,float Head,Vector2 FarFoot,Vector2 NearFoot,
        Vector2 FarHand,Vector2 NearHand,float FootRoll,float Cape,float Crest);
    public static Pose Sample(float pitch,float lean,float cycle,float weight,float pelvisHeight)
    {
        float waist=pitch*.7f, chest=.045f+lean-waist*.82f;
        float upper=waist+chest, phase=cycle*Mathf.Tau;
        Vector2 seat=new(0,-pelvisHeight-2);
        Vector2 FollowSaddle(Vector2 point) => seat+(point-seat).Rotated(pitch);
        Vector2 reinFollow=new(lean*4,-lean*2);
        return new(waist,chest,.015f-upper*.9f,
            FollowSaddle(new(10,-9)),FollowSaddle(new(15,-5)),
            FollowSaddle(new(16,-36))+reinFollow,FollowSaddle(new(19,-32))+reinFollow,
            pitch*.7f,-.08f-lean*.45f+Mathf.Sin(phase-.65f)*.035f*weight,
            -upper*.12f+Mathf.Sin(phase-.9f)*.025f*weight);
    }
}
