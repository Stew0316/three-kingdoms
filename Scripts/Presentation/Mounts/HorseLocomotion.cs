using Godot;

/// <summary>三拍跑步：后随肢、对角双肢、领前肢、腾空。接地轨迹与关节折叠分别设计。</summary>
public static class HorseLocomotion
{
    public const float CycleDistance = 128; // 一轮实际路程；190/250速度约1.48/1.95轮每秒。
    public const float SupportFraction = .30f;
    public const float HalfStride = CycleDistance * SupportFraction * .5f;
    public static readonly Vector2 BodyPivot = new(0, -34);
    public readonly record struct LegPose(Vector2 Root, Vector2 Proximal, Vector2 Joint,
        Vector2 Fetlock, Vector2 Hoof, float HoofAngle, bool Supporting, float Phase);

    // 肩/髋至肘/膝藏于肌肉内；外露腕/飞节以下保持细长，不画成四根等粗杆。
    public static Vector4 Lengths(bool front) => front ? new(11, 17, 13, 5) : new(13, 16, 13, 5);
    public static float Touchdown(bool front, bool near) => front ? (near ? .46f : .22f) : (near ? .22f : 0);
    public static float LegPhase(bool front, bool near, float phase) => Mathf.PosMod(phase - Touchdown(front, near), 1);

    // 兼容原接口，整身关键姿势与挂点统一由HorseBodyMotion提供。
    public static float Bob(float phase, float weight) => HorseBodyMotion.Bob(phase, weight);
    public static float BodyPitch(float phase, float weight) => HorseBodyMotion.ChestPitch(phase, weight);
    public static float NeckPitch(float phase, float weight) => HorseBodyMotion.NeckPitch(phase, weight);
    public static float RiderLean(float phase, float weight) => (.07f + .025f * Mathf.Sin((phase - .15f) * Mathf.Tau)) * weight;
    public static Vector2 BodyPoint(Vector2 point, float phase, float weight) =>
        HorseBodyMotion.Sample(phase, weight).SkinPoint(point);

    /// <summary>承重蹄相对世界静止；离地后先折腕/飞节、再前送、最后伸蹄接地。</summary>
    public static Vector2 HoofOffset(float phase, bool front)
    {
        float t = Mathf.PosMod(phase, 1);
        if (t < SupportFraction) return new(HalfStride - CycleDistance * t, 0);
        float u = (t - SupportFraction) / (1 - SupportFraction);
        // 三段手工轨迹：回收点位于身后，前肢较高，后肢从腹下向前踏进。
        Vector2 a = new(-HalfStride, 0), b = front ? new(-16, -18) : new(-9, -13);
        Vector2 c = front ? new(21, -9) : new(22, -7), d = new(HalfStride, 0);
        Vector2 groundTangent = new(-CycleDistance * (1 - SupportFraction), 0);
        Vector2 middleTangent = new(78, 0), reachTangent = new(14, 35);
        if (u < .40f) return Hermite(a, b, groundTangent * .40f, middleTangent * .40f, u / .40f);
        if (u < .80f) return Hermite(b, c, middleTangent * .40f, reachTangent * .40f, (u - .40f) / .40f);
        return Hermite(c, d, reachTangent * .20f, groundTangent * .20f, (u - .80f) / .20f);
    }

    public static LegPose Sample(bool front, bool near, float phase, float weight, float bodyWidth = 1)
    {
        weight = Mathf.Clamp(weight, 0, 1);
        float t = LegPhase(front, near, phase);
        float depth = near ? 2.5f : -3.5f;
        Vector2 anchor = new((front ? 18 : -19) * bodyWidth, front ? -35 : -36);
        Vector2 root = HorseBodyMotion.Sample(phase, weight, bodyWidth).LegRoot(front, near);
        Vector2 step = HoofOffset(t, front) * weight;
        // 蹄的前后基线不随躯干俯仰偏移，否则承重阶段会沿地面打滑。
        // 领肢比随肢略向前探，不把同侧两腿做成一份动画的机械复制。
        float leadReach = front ? (near ? 1.8f : -.5f) : (near ? 1.0f : -.5f);
        // 中立站姿远蹄略错开，四肢在俯视侧身中可辨；进入跑步后回到真实支撑轨迹。
        float standingStagger = near ? 0 : front ? 3.5f : -3f;
        Vector2 hoof = new(anchor.X + step.X + leadReach * weight + standingStagger * (1-weight), depth + step.Y);
        float swing = Mathf.Clamp((t - SupportFraction) / (1 - SupportFraction), 0, 1);
        float fold = Mathf.Pow(Mathf.Sin(swing * Mathf.Pi), 2) * weight;
        float hoofAngle = fold * (front ? 1.05f : .70f);
        Vector4 lengths = Lengths(front);
        Vector2 fetlock = hoof - Vector2.FromAngle(Mathf.DegToRad(65) + hoofAngle) * lengths.W;

        // 承重时腕以下近乎伸直，摆动才明显折叠。先决定折叠量，再解近端骨，
        // 避免把蹄点塞进一条恒定弯曲的腿，造成站立时也像蹲着的机械折杆。
        float distance = root.DistanceTo(fetlock);
        float desiredReach = lengths.Y + lengths.Z - (front ? .22f : 2.4f) - fold * (front ? 12 : 6);
        // 近共线位置留出余量，并平滑接入可达范围，避免硬截断产生瞬时膝速变化。
        float minReach = Mathf.Abs(distance - lengths.X) + .30f;
        float maxReach = Mathf.Min(distance + lengths.X - .60f, lengths.Y + lengths.Z - .05f);
        float reach = SmoothMax(desiredReach, minReach, .6f);
        reach = -SmoothMax(-reach, -maxReach, .6f);
        Vector2 proximal = SolveJoint(root, fetlock, lengths.X, reach, front ? 1 : -1);
        // 肘始终在肩后下方，膝在髋前下方；不能为了追随蹄点把上臂/股骨翻到背上。
        float preferredAngle = Mathf.PosMod((proximal - root).Angle(), Mathf.Tau);
        if (!front && preferredAngle > Mathf.Pi) preferredAngle -= Mathf.Tau;
        if(front)
        {
            // 上臂先带动肘向后承重、向前回收，再由前臂与管骨追随蹄点。
            // 不能通过强缩肘到蹄的距离反推上臂，否则收腿时肘会翻到胸内、上臂近乎水平。
            float angle=t<.30f ? Mathf.Lerp(110,140,Mathf.SmoothStep(0,.30f,t))
                : t<.55f ? Mathf.Lerp(140,132,Mathf.SmoothStep(.30f,.55f,t))
                : t<.80f ? Mathf.Lerp(132,102,Mathf.SmoothStep(.55f,.80f,t))
                : Mathf.Lerp(102,110,Mathf.SmoothStep(.80f,1,t));
            // 承重和站立仍以腕部伸展为优先；只有离地收腿时交给上臂引导，避免站姿屈腕。
            preferredAngle=Mathf.Lerp(Mathf.Clamp(preferredAngle,Mathf.DegToRad(105),Mathf.DegToRad(168)),Mathf.DegToRad(angle),fold);
        }
        else preferredAngle=Mathf.Clamp(preferredAngle,Mathf.DegToRad(22),Mathf.DegToRad(88));
        float axisAngle = (fetlock - root).Angle();
        float safeReach = lengths.Y + lengths.Z - .22f;
        float spread = Mathf.Acos(Mathf.Clamp((distance * distance + lengths.X * lengths.X - safeReach * safeReach)
            / (2 * distance * lengths.X), -1, 1));
        float relativeAngle = Mathf.Wrap(preferredAngle - axisAngle, -Mathf.Pi, Mathf.Pi);
        proximal = root + Vector2.FromAngle(axisAngle + Mathf.Clamp(relativeAngle, -spread, spread)) * lengths.X;
        Vector2 joint = SolveJoint(proximal, fetlock, lengths.Y, lengths.Z, front ? -1 : 1);
        return new(root, proximal, joint, fetlock, hoof, hoofAngle, t < SupportFraction, t);
    }

    private static Vector2 SolveJoint(Vector2 root, Vector2 tip, float upper, float lower, float bend)
    {
        Vector2 axis = tip - root;
        float distance = Mathf.Max(axis.Length(), .0001f);
        Vector2 direction = axis / distance;
        float along = (upper * upper - lower * lower + distance * distance) / (2 * distance);
        float height = Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
        return root + direction * along + new Vector2(-direction.Y, direction.X) * height * bend;
    }

    private static Vector2 Hermite(Vector2 a, Vector2 b, Vector2 da, Vector2 db, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return (2 * t3 - 3 * t2 + 1) * a + (t3 - 2 * t2 + t) * da
            + (-2 * t3 + 3 * t2) * b + (t3 - t2) * db;
    }

    private static float SmoothMax(float a, float b, float width)
    {
        float overlap = Mathf.Max(width - Mathf.Abs(a - b), 0);
        return Mathf.Max(a, b) + overlap * overlap / (4 * width);
    }

}
