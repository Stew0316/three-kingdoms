using Godot;

/// <summary>坐骑共用的侧视关节链。前肢与后肢分别标定，四段骨长恒定，只有关节角度变化。</summary>
public static class HorseLocomotion
{
    public const float CycleDistance = 96;
    public const float SupportFraction = .42f; // 两次对角承重之间保留短暂腾空。
    public const float HalfStride = CycleDistance * SupportFraction * .5f;
    public readonly record struct LegPose(Vector2 Root, Vector2 Proximal, Vector2 Joint,
        Vector2 Fetlock, Vector2 Hoof, float HoofAngle, bool Supporting, float Phase);

    /// <summary>各段分别是上臂/股骨、前臂/胫骨、管骨、系部；属于显示比例，不是现实厘米。</summary>
    public static Vector4 Lengths(bool front) => front ? new(11, 17, 13, 5) : new(13, 16, 13, 5);
    public static float LegPhase(bool front, bool near, float phase) => Mathf.PosMod(phase + (front == near ? 0 : .5f), 1);
    public static float Bob(float phase, float weight) => -1.1f * (.5f + .5f * Mathf.Cos((phase - .46f) * Mathf.Tau * 2)) * weight;

    /// <summary>承重阶段相对马身按恒速后移，抵消世界平移；飞行首尾保留同一水平切线。</summary>
    public static Vector2 HoofOffset(float phase, bool front)
    {
        float t = Mathf.PosMod(phase, 1);
        if (t < SupportFraction) return new(HalfStride - CycleDistance * t, 0);
        float u = (t - SupportFraction) / (1 - SupportFraction);
        float u2 = u * u, u3 = u2 * u;
        float tangent = -CycleDistance * (1 - SupportFraction);
        float x = (2*u3-3*u2+1)*-HalfStride + (u3-2*u2+u)*tangent
            + (-2*u3+3*u2)*HalfStride + (u3-u2)*tangent;
        float lift = Mathf.Pow(Mathf.Sin(u * Mathf.Pi), 2);
        return new(x, -lift * (front ? 15 : 12));
    }

    public static LegPose Sample(bool front, bool near, float phase, float weight, float bodyWidth = 1)
    {
        weight = Mathf.Clamp(weight, 0, 1);
        float t = LegPhase(front, near, phase);
        float depth = near ? 2.5f : -3.5f;
        Vector2 root = new((front ? 18 : -19) * bodyWidth, (front ? -35 : -36) + depth + Bob(phase,weight));
        Vector2 step = HoofOffset(t, front) * weight;
        Vector2 hoof = new(root.X + step.X, depth + step.Y);
        float swing = Mathf.Clamp((t-SupportFraction)/(1-SupportFraction),0,1);
        float tuck = Mathf.Pow(Mathf.Sin(swing*Mathf.Pi),2) * weight;
        // 回收时蹄尖向下折，落地前展开；承重期间蹄底保持平放。
        float hoofAngle = tuck * (front ? .85f : .6f);
        Vector4 lengths = Lengths(front);
        Vector2 pastern = Vector2.FromAngle(Mathf.DegToRad(58) + hoofAngle) * lengths.W;
        Vector2 fetlock = hoof - pastern;

        // 上臂的肘留在肩后，后肢膝向前；伸展时近端角被下两段的可达圆约束。
        float reach = step.X / HalfStride;
        float preferred = Mathf.DegToRad(front ? 150 - 48*reach : 60 - 25*reach);
        Vector2 target = fetlock - root;
        float distance = target.Length();
        float maxReach = lengths.Y + lengths.Z - .35f;
        float cosine = (distance*distance + lengths.X*lengths.X - maxReach*maxReach) / (2*distance*lengths.X);
        float spread = Mathf.Acos(Mathf.Clamp(cosine,-1,1));
        float relative = Mathf.Wrap(preferred - target.Angle(), -Mathf.Pi, Mathf.Pi);
        float angle = target.Angle() + Mathf.Clamp(relative,-spread,spread);
        Vector2 proximal = root + Vector2.FromAngle(angle) * lengths.X;
        Vector2 joint = SolveJoint(proximal,fetlock,lengths.Y,lengths.Z,front ? -1 : 1);
        return new(root,proximal,joint,fetlock,hoof,hoofAngle,t<SupportFraction,t);
    }

    /// <summary>解析两骨求解：前腕向前折、后飞节向后折，分支固定，不在过零时翻膝。</summary>
    private static Vector2 SolveJoint(Vector2 root, Vector2 tip, float upper, float lower, float bend)
    {
        Vector2 axis = tip-root;
        float distance = axis.Length();
        Vector2 direction = axis / Mathf.Max(distance,.0001f);
        float along = (upper*upper-lower*lower+distance*distance) / (2*Mathf.Max(distance,.0001f));
        float height = Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
        return root + direction*along + new Vector2(-direction.Y,direction.X)*height*bend;
    }
}

