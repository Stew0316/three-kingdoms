using Godot;

/// <summary>按真实路程播放三拍跑步；躯干、头颈、四肢和骑手共享一个姿态时钟。</summary>
public partial class CityHorse : Node2D
{
    public HorseAppearance Appearance { get; set; }
    public Combatant Rider { get; set; }
    public float GaitPhase { get; private set; }
    public float GaitWeight { get; private set; } // 起停仅混合视觉姿势，不改变玩法移速。
    public float BodyBob => HorseLocomotion.Bob(GaitPhase, GaitWeight);
    public float BodyPitch => HorseLocomotion.BodyPitch(GaitPhase, GaitWeight);
    public float RiderLean => HorseLocomotion.RiderLean(GaitPhase, GaitWeight);
    public Vector2 SaddleOffset
    {
        get
        {
            Vector2 offset = HorseLocomotion.BodyPoint(new(0, -43), GaitPhase, GaitWeight) - new Vector2(0, -43);
            return new(offset.X * Facing, offset.Y);
        }
    }
    public float Facing { get; private set; } = 1;
    public const float CycleDistance = HorseLocomotion.CycleDistance;
    private float _lastDistance;
    private Vector2 _bridlePoint;
    private static readonly Color Outline = new("493b30"), Leather = new("49392c"), Ivory = new("e0d4b7");

    public override void _Ready()
    {
        ProcessPriority = -10; // 马先更新，骨架在同帧读取鞍点和俯仰。
        ResetGait();
    }
    public void ResetGait()
    {
        _lastDistance = Rider?.WalkDistance ?? 0;
        GaitPhase = GaitWeight = 0;
        Facing = Rider?.FacingDirection.X < -.1f ? -1 : 1;
    }
    /// <summary>离线预览使用正式绘制路径，运行中的骑手不接受测试覆写。</summary>
    internal void SetPreviewPose(float phase, float weight)
    {
        if (Rider != null) return;
        GaitPhase = Mathf.PosMod(phase, 1);
        GaitWeight = weight;
        QueueRedraw();
    }
    public override void _Process(double delta)
    {
        if (Rider != null)
        {
            float distance = Mathf.Max(0, Rider.WalkDistance - _lastDistance);
            _lastDistance = Rider.WalkDistance;
            if (Rider.IsMounted)
            {
                if (Mathf.Abs(Rider.FacingDirection.X) > .1f) Facing = Mathf.Sign(Rider.FacingDirection.X);
                GaitPhase = Mathf.PosMod(GaitPhase + distance / CycleDistance, 1);
                GaitWeight = Mathf.MoveToward(GaitWeight, Rider.WalkVelocity.LengthSquared() > 1 ? 1 : 0, (float)delta * 9);
            }
            else GaitWeight = 0;
        }
        QueueRedraw();
    }
    public static Vector2 SampleHoof(float phase) => HorseLocomotion.HoofOffset(phase, true);

    private Transform2D BodyTransform
    {
        get
        {
            Vector2 pivot = HorseLocomotion.BodyPivot;
            var mirror = new Transform2D(new(Facing, 0), Vector2.Down, Vector2.Zero);
            return mirror * new Transform2D(BodyPitch, pivot + new Vector2(0, BodyBob) - pivot.Rotated(BodyPitch));
        }
    }

    public override void _Draw()
    {
        if (Appearance == null) return;
        Color coat = Appearance.Coat, shade = coat.Darkened(.24f);
        DrawSetTransform(new(0, 3), 0, new Vector2(35, 7));
        DrawCircle(Vector2.Zero, 1, new Color(0, 0, 0, .14f));
        DrawSetTransform(Vector2.Zero, 0, new Vector2(Facing, 1));
        DrawLeg(false, false, shade);
        DrawLeg(true, false, shade);
        DrawSetTransformMatrix(BodyTransform);
        DrawTail();
        DrawBody(coat, shade);
        DrawNeckAndHead(coat, shade);
        DrawSetTransform(Vector2.Zero, 0, new Vector2(Facing, 1));
        DrawLeg(false, true, coat);
        DrawLeg(true, true, coat);
        DrawSetTransformMatrix(BodyTransform);
        // 肩胛和臀肌遮住近端骨接缝，四肢从有体积的胸臀下伸出。
        SmoothShape(coat, false, new(-28,-35),new(-22,-42),new(-14,-37),new(-13,-29),new(-19,-24),new(-26,-28));
        SmoothShape(coat, false, new(12,-37),new(19,-40),new(25,-34),new(22,-25),new(17,-23),new(14,-29));
        DrawArc(new(-21,-34), 7, -2.3f, .6f, 14, coat.Lightened(.10f), .8f, true);
        DrawLine(new(20,-36), new(15,-29), shade.Lightened(.08f), .9f, true);
        if (Appearance.Dappled)
            foreach (var p in new Vector2[] {new(-23,-36),new(-18,-33),new(-24,-30),new(-18,-27)})
                DrawCircle(p, 1.7f, coat.Lightened(.16f));
        DrawTack();
        DrawSetTransform(Vector2.Zero);
        if (Rider != null && Rider.IsMounted && Rider.Rig != null)
        {
            Vector2 hand = ToLocal(Rider.Rig.Skeleton.ToGlobal(Rider.Rig.ReferencePrimaryGrip));
            Vector2 slack = _bridlePoint.Lerp(hand, .48f) + new Vector2(0, 4);
            DrawPolyline(new[] { _bridlePoint, slack, hand }, Leather, 1.0f, true);
        }
    }

    private void DrawBody(Color coat, Color shade)
    {
        float w = Appearance.BodyWidth;
        SmoothShape(coat, true, new(-29*w,-31),new(-28*w,-38),new(-23*w,-43),new(-14*w,-44),
            new(-5,-42),new(8,-43),new(19*w,-42),new(25*w,-36),new(24*w,-28),new(16,-23),new(-2,-23),new(-19*w,-24));
        SmoothShape(shade, false, new(-25*w,-30),new(-10,-27),new(10,-27),new(23*w,-32),new(19,-25),new(3,-22.7f),new(-17,-24));
        DrawPolyline(new[] {new Vector2(-23*w,-39),new Vector2(-17*w,-41),new Vector2(-10,-40)}, coat.Lightened(.16f), 1.4f, true);
    }

    private void DrawNeckAndHead(Color coat, Color shade)
    {
        Vector2 neckRoot = new(17,-34);
        float nod = HorseLocomotion.NeckPitch(GaitPhase, GaitWeight);
        var neck = BodyTransform * new Transform2D(nod, neckRoot - neckRoot.Rotated(nod));
        DrawSetTransformMatrix(neck);
        // 颈部由宽胸渐收至项部，喉下留出弧度，头部单独做小幅俯仰。
        SmoothShape(coat, true, new(11,-37),new(19,-45),new(24,-53),new(29,-57),new(34,-54),
            new(33,-48),new(29,-40),new(26,-30),new(21,-25),new(17,-29));
        SmoothShape(coat.Lightened(.09f), false, new(24,-50),new(28,-53),new(28,-44),new(23,-34),new(20,-31));
        if (Appearance.LongMane)
            SmoothShape(Appearance.Mane, true,new(29,-57),new(24,-55),new(20,-48),new(15,-40),new(12,-36),
                new(16,-34),new(20,-40),new(20,-36),new(24,-45));
        else
            Shape(Appearance.Mane,new(29,-57),new(24,-58),new(22,-53),new(19,-49),new(17,-44),new(16,-40),new(20,-42),new(26,-53));
        Vector2 poll = new(30,-53);
        float headNod = -.35f * nod;
        var head = neck * new Transform2D(headNod, poll - poll.Rotated(headNod));
        DrawSetTransformMatrix(head);
        Shape(coat.Darkened(.1f),new(27,-54),new(26,-62),new(29,-60),new(31,-54));
        Shape(coat,new(32,-55),new(34,-62),new(36,-60),new(35,-53));
        SmoothShape(coat.Lightened(.04f),true,new(28,-56),new(34,-56),new(38,-52),new(42,-48),new(46,-44),
            new(46,-41),new(42,-40),new(37,-43),new(32,-46),new(28,-46));
        SmoothShape(shade,false,new(41,-47),new(46,-44),new(46,-41),new(42,-40),new(39,-43));
        if (Appearance.FaceBlaze) DrawPolyline(new[] {new Vector2(35,-54),new Vector2(37,-49),new Vector2(42,-44)}, Ivory, 1.8f, true);
        DrawCircle(new(34,-51), .95f, new Color("24221e"));
        DrawCircle(new(34.3f,-51.3f), .25f, Ivory);
        DrawCircle(new(44,-43), .65f, Outline);
        DrawPolyline(new[] {new Vector2(30,-55),new Vector2(32,-46),new Vector2(41,-41)}, Leather, 1.2f, true);
        DrawLine(new(42,-47), new(40,-41), Leather, 1.2f, true);
        _bridlePoint = head * new Vector2(41,-42);
    }

    private void DrawTail()
    {
        float follow = Mathf.Sin((GaitPhase - .18f) * Mathf.Tau) * GaitWeight;
        float stream = 5 * GaitWeight;
        SmoothShape(Appearance.Mane, true, new(-27,-36),new(-33,-34),new(-36-stream,-27),
            new(-39-stream,-16+follow*2),new(-36-stream,-13+follow*2),new(-32-stream,-21),new(-30,-30));
    }

    private void DrawTack()
    {
        var a = Appearance;
        if (a.SaddleBags)
        {
            Shape(a.Blanket,new(-14,-42),new(11,-42),new(12,-27),new(-14,-27));
            DrawLine(new(-13,-28),new(11,-28),a.Trim,1.3f,true);
            Shape(new Color("775638"),new(-23,-38),new(-14,-38),new(-13,-28),new(-22,-27));
            DrawLine(new(-22,-35),new(-14,-35),a.Trim,1.2f,true);
        }
        else
        {
            Shape(a.Blanket,new(-13,-42),new(12,-42),new(14,-30),new(2,-25),new(-13,-30));
            DrawPolyline(new[] {new Vector2(-12,-30),new Vector2(2,-26),new Vector2(13,-30)},a.Trim,1.4f,true);
        }
        DrawLine(new(7,-39),new(5,-23),Leather,2,true);
        SmoothShape(Leather,true,new(-11,-43),new(-9,-45),new(-5,-42),new(5,-42),new(9,-45),new(11,-42),new(8,-39),new(-8,-39));
        Vector2 stirrup = Rider != null && Rider.IsMounted && Rider.Rig != null
            ? BodyTransform.AffineInverse() * ToLocal(Rider.Rig.RidingSoleWorld)
            : new Vector2(4, -24);
        DrawLine(new(2,-39), stirrup + new Vector2(-2,-3), Leather, 1.1f, true);
        DrawPolyline(new[] {stirrup+new Vector2(-3,-3),stirrup+new Vector2(-3,1),
            stirrup+new Vector2(3,1),stirrup+new Vector2(3,-3)},a.Trim,1.0f,true);
    }

    private void DrawLeg(bool front, bool near, Color color)
    {
        var p = HorseLocomotion.Sample(front, near, GaitPhase, GaitWeight, Appearance.BodyWidth);
        Vector2[] joints = { p.Root, p.Proximal, p.Joint, p.Fetlock, p.Hoof };
        float[] widths = front ? new[] { 4.8f,3.1f,1.6f,1.25f,1.5f } : new[] { 6.1f,4.0f,1.9f,1.3f,1.5f };
        // 先整条轮廓再整条填色，关节内部没有逐段黑边和铆钉圆。
        for (int pass = 0; pass < 2; pass++)
        {
            float edge = pass == 0 ? .6f : 0;
            Color fill = pass == 0 ? Outline : color;
            for (int i = 0; i < joints.Length - 1; i++)
                Bone(joints[i], joints[i+1], widths[i]+edge, widths[i+1]+edge, fill);
            for (int i = 1; i < joints.Length - 1; i++) DrawCircle(joints[i], widths[i]+edge, fill);
        }
        if (Appearance.FaceBlaze && near)
            Bone(p.Fetlock, p.Hoof, 1.25f, 1.5f, Ivory);
        Vector2[] hoof = { new(-2.1f,-2.5f),new(1.1f,-2.5f),new(2.8f,0),new(-2.3f,0) };
        for (int i=0; i<hoof.Length; i++) hoof[i] = p.Hoof + hoof[i].Rotated(p.HoofAngle);
        Shape(Appearance.Mane, hoof);
    }

    private void Bone(Vector2 start, Vector2 end, float rootWidth, float tipWidth, Color color)
    {
        Vector2 axis = (end-start).Normalized(), side = new(-axis.Y,axis.X);
        DrawColoredPolygon(new[] {start+side*rootWidth,end+side*tipWidth,end-side*tipWidth,start-side*rootWidth}, color);
    }
    private void Shape(Color fill, params Vector2[] points)
    {
        DrawColoredPolygon(points, fill);
        var border = new Vector2[points.Length+1];
        points.CopyTo(border,0); border[^1]=points[0];
        DrawPolyline(border, Outline, .8f, true);
    }
    /// <summary>只平滑形体外轮廓，保留原生矢量清晰度；不是模糊贴图。</summary>
    private void SmoothShape(Color fill, bool border, params Vector2[] points)
    {
        const int steps = 5;
        var curve = new Vector2[points.Length*steps];
        for (int i=0; i<points.Length; i++)
        {
            Vector2 a=points[(i+points.Length-1)%points.Length], b=points[i], c=points[(i+1)%points.Length], d=points[(i+2)%points.Length];
            for (int j=0; j<steps; j++)
            {
                float t=(float)j/steps;
                curve[i*steps+j]=AnimationCurves.CatmullRom(a,b,c,d,t);
            }
        }
        if (border) Shape(fill,curve); else DrawColoredPolygon(curve,fill);
    }
}
