using Godot;

/// <summary>三拍跑步的整身姿态。变换均把马的中立坐标映射到当前姿态，不是骨节点局部变换。</summary>
public static class HorseBodyMotion
{
    // 事件时刻对应后肢接地、对角承重、领前肢承重和腾空，角度是游戏表现参数。
    private static readonly float[] Events = { 0, .12f, .22f, .36f, .46f, .60f, .76f, .88f };
    private static readonly float[] Heights = { -.8f, 0, -.6f, -1.2f, -.4f, .4f, -1.5f, -2.4f };
    private static readonly float[] ChestAngles = { -2.5f, -3.5f, -2, .5f, 2.5f, 3.2f, .5f, -2 };
    private static readonly float[] PelvisAngles = { -3, -1, 1.5f, 3, 2, -2, -4, -3.5f };
    private static readonly float[] LowerNeckAngles = { -2, -1, 1, 4, 2, -2, -4, -3 };
    private static readonly float[] UpperNeckAngles = { 1, 1, 0, -1, -1, .5f, 1.5f, 1.5f };
    private static readonly float[] HeadAngles = { -1, -1, 0, 1, 2, 0, -1, -1 };
    private static readonly float[] ShoulderEvents = { 0, .15f, .30f, .45f, .65f, .85f };
    private static readonly float[] ShoulderAngles = { -6, 1, 7, 4, -2, -7 };

    /// <summary>各皮肤部位共享的中立坐标映射；关节、鞍座与骑手应从同一份姿态读取。</summary>
    public readonly struct Pose
    {
        public Transform2D Chest { get; }
        public Transform2D Pelvis { get; }
        public Transform2D LowerNeck { get; }
        public Transform2D UpperNeck { get; }
        public Transform2D Head { get; }
        public Transform2D NearShoulder { get; }
        public Transform2D FarShoulder { get; }
        public Vector2 NeckRoot { get; }
        public Vector2 NeckMiddle { get; }
        public Vector2 Poll { get; }
        public Vector2 Saddle { get; }
        public Vector2 TailRoot { get; }
        public float ChestAngle { get; }
        private readonly float _bodyWidth;

        internal Pose(float phase, float weight, float bodyWidth)
        {
            _bodyWidth = bodyWidth;
            ChestAngle = ChestPitch(phase, weight);
            Vector2 chestPivot = new(8, -35);
            Vector2 pelvisPivot = new(-20 * bodyWidth, -35);
            Chest = Map(chestPivot, chestPivot + new Vector2(0, Bob(phase, weight)), ChestAngle);
            // 骨盆中心随胸廓平移，额外滚动只发生在腰骶部，不把整副躯干锁成刚体。
            float pelvisAngle = ChestAngle + Angle(PelvisAngles, phase, weight);
            Pelvis = Map(pelvisPivot, Chest * pelvisPivot, pelvisAngle);

            Vector2 neckRoot = new(18 * bodyWidth, -35);
            Vector2 neckMiddle = new(30, -47.5f);
            Vector2 poll = new(40, -55);
            LowerNeck = Chest * Map(neckRoot, neckRoot, Angle(LowerNeckAngles, phase, weight));
            UpperNeck = LowerNeck * Map(neckMiddle, neckMiddle, Angle(UpperNeckAngles, phase, weight));
            NeckRoot = Chest * neckRoot;
            NeckMiddle = LowerNeck * neckMiddle;
            Poll = UpperNeck * poll;
            // 头的世界俯仰独立采样，抵消父段累计角度；两段颈的长度仍由刚性变换保持。
            Head = Map(poll, Poll, Angle(HeadAngles, phase, weight));

            NearShoulder = Shoulder(Chest, phase, weight, bodyWidth, true);
            FarShoulder = Shoulder(Chest, phase, weight, bodyWidth, false);
            // 鞍点与胸腹皮肤使用同一混合，尾根则直接跟随骨盆。
            Saddle = BlendSkin(new(0, -43), Chest, Pelvis, bodyWidth);
            TailRoot = Pelvis * new Vector2(-27 * bodyWidth, -43);
        }

        /// <summary>前腿根随各自肩胛，后腿根随骨盆；远近层深在整身运动后加入。</summary>
        public Vector2 LegRoot(bool front, bool near)
        {
            // 肩/髋关节位于胸臀上半部，不能放在腹缘，否则站立上臂水平、后腿像蹲伏。
            Vector2 rest = new((front ? 18 : -19) * _bodyWidth, front ? -39 : -40);
            Transform2D transform = front ? (near ? NearShoulder : FarShoulder) : Pelvis;
            // 肩髋藏在躯干宽度内，投影深差小于蹄底，防止远腿根穿出马背。
            return transform * rest + new Vector2(0, near ? 1.5f : -1.5f);
        }

        /// <summary>皮肤的根边留在胸臀表面；肩胛在皮下运动，不让腿根整块贴片沿马身滑动。</summary>
        public Vector2 LegSkinRoot(bool front,bool near)
        {
            Vector2 rest=new((front?18:-19)*_bodyWidth,front?-39:-40);
            return (front?Chest:Pelvis)*rest+new Vector2(0,near?1.5f:-1.5f);
        }

        /// <summary>胸腹连续皮肤按中立横坐标平滑混合胸廓与骨盆，避免腰部硬切缝。</summary>
        public Vector2 SkinPoint(Vector2 restPoint) => BlendSkin(restPoint, Chest, Pelvis, _bodyWidth);
    }

    public static Pose Sample(float phase, float weight, float bodyWidth = 1) =>
        new(Mathf.PosMod(phase, 1), Mathf.Clamp(weight, 0, 1), Mathf.Max(bodyWidth, .01f));

    /// <summary>胸廓的竖向平移，兼容原起伏读取接口。</summary>
    // 跑步进入屈肢承重后重心较站姿下沉；留出后蹬伸展余量，不能靠拉长骨段追蹄。
    public static float Bob(float phase, float weight) => (3.25f + Curve(Events, Heights, phase)) * Mathf.Clamp(weight, 0, 1);
    public static float ChestPitch(float phase, float weight) => Angle(ChestAngles, phase, Mathf.Clamp(weight, 0, 1));
    /// <summary>兼容接口返回颈根相对胸廓的旋转；正式颈头绘制使用Pose中的三份映射。</summary>
    public static float NeckPitch(float phase, float weight) => Angle(LowerNeckAngles, phase, Mathf.Clamp(weight, 0, 1));

    private static float Angle(float[] values, float phase, float weight) =>
        Mathf.DegToRad(Curve(Events, values, phase)) * weight;

    private static Transform2D Map(Vector2 restPivot, Vector2 posedPivot, float rotation) =>
        new(rotation, posedPivot - restPivot.Rotated(rotation));

    private static Transform2D Shoulder(Transform2D chest, float phase, float weight, float width, bool near)
    {
        float localPhase = HorseLocomotion.LegPhase(true, near, phase);
        float degrees = Curve(ShoulderEvents, ShoulderAngles, localPhase) * weight;
        Vector2 pivot = new(12 * width, -43);
        // 肩胛绕上缘转动并在胸廓上小幅滑动；接地蹄仍由独立地面轨迹约束。
        Vector2 slide = new(-degrees / 7 * .65f * width, 0);
        return chest * Map(pivot, pivot + slide, Mathf.DegToRad(degrees));
    }

    private static Vector2 BlendSkin(Vector2 point, Transform2D chest, Transform2D pelvis, float width)
    {
        float t = Mathf.Clamp((point.X + 20 * width) / (8 + 20 * width), 0, 1);
        t = t * t * (3 - 2 * t);
        return (pelvis * point).Lerp(chest * point, t);
    }

    /// <summary>不等时距的周期Hermite曲线；相邻斜率异号时切线为零，避免承重极值过冲。</summary>
    private static float Curve(float[] times, float[] values, float phase)
    {
        float t = Mathf.PosMod(phase, 1);
        int count = times.Length, index = count - 1;
        for (int i = 0; i < count - 1; i++)
            if (t < times[i + 1]) { index = i; break; }
        int next = (index + 1) % count;
        float start = times[index], end = next == 0 ? 1 : times[next];
        float duration = end - start, u = (t - start) / duration;
        float u2 = u * u, u3 = u2 * u;
        float a = values[index], b = values[next];
        return (2 * u3 - 3 * u2 + 1) * a + (u3 - 2 * u2 + u) * Tangent(times, values, index) * duration
            + (-2 * u3 + 3 * u2) * b + (u3 - u2) * Tangent(times, values, next) * duration;
    }

    private static float Tangent(float[] times, float[] values, int index)
    {
        int count = times.Length, previous = (index + count - 1) % count, next = (index + 1) % count;
        float before = times[index] - (previous > index ? times[previous] - 1 : times[previous]);
        float after = (next < index ? times[next] + 1 : times[next]) - times[index];
        float left = (values[index] - values[previous]) / before;
        float right = (values[next] - values[index]) / after;
        if (left * right <= 0) return 0;
        float firstWeight = 2 * after + before, secondWeight = after + 2 * before;
        return (firstWeight + secondWeight) / (firstWeight / left + secondWeight / right);
    }
}
