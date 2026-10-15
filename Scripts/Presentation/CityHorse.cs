using Godot;

/// <summary>共用对角小跑步态，按实际路程推进；外观资源与城市移速分别配置。</summary>
public partial class CityHorse : Node2D
{
    public HorseAppearance Appearance { get; set; }
    public Combatant Rider { get; set; }
    public float GaitPhase { get; private set; } // 归一化周期，停下或顶墙不继续推进。
    public float GaitWeight { get; private set; } // 只平滑姿态，不延迟实际移动。
    public float BodyBob => HorseLocomotion.Bob(GaitPhase,GaitWeight);
    public float Facing { get; private set; } = 1;
    public const float CycleDistance = HorseLocomotion.CycleDistance; // 两马共用步长，190/250速度约1.98/2.60轮每秒。
    private float _lastDistance;
    private static readonly Color Outline = new("39322b"), Leather = new("49392c"), Ivory = new("e0d4b7");

    public override void _Ready()
    {
        ProcessPriority = -10; // 先更新马背，武将骨架读取同一帧的起伏和朝向。
        ResetGait();
    }
    public void ResetGait()
    {
        _lastDistance = Rider?.WalkDistance ?? 0;
        GaitPhase = GaitWeight = 0;
        Facing = Rider?.FacingDirection.X < -.1f ? -1 : 1;
    }
    /// <summary>仅离线姿态验收调用；预览节点没有骑手，不干预游戏中的路程时钟。</summary>
    internal void SetPreviewPose(float phase, float weight)
    {
        if (Rider != null) return;
        GaitPhase = Mathf.PosMod(phase,1);
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

    public static Vector2 SampleHoof(float phase) => HorseLocomotion.HoofOffset(phase,true);

    public override void _Draw()
    {
        if (Appearance == null) return;
        var a = Appearance;
        Color coat = a.Coat, shade = coat.Darkened(.24f);
        DrawSetTransform(new(0, 3), 0, new Vector2(32, 7));
        DrawCircle(Vector2.Zero, 1, new Color(0, 0, 0, .16f));
        // 原生几何直接在最终视口绘制，不经过低分辨率中间贴图。
        DrawSetTransform(Vector2.Zero, 0, new Vector2(Facing, 1));
        DrawLeg(false, false, shade);
        DrawLeg(true, false, shade);
        DrawSetTransform(new(0, BodyBob), 0, new Vector2(Facing, 1));
        float tail = Mathf.Sin(GaitPhase * Mathf.Tau) * 2 * GaitWeight;
        Shape(a.Mane, new(-24,-33),new(-30,-32),new(-34,-21),new(-37+tail,-13),new(-32+tail,-14),new(-27,-23));
        DrawSetTransform(new(0, BodyBob), 0, new Vector2(Facing * a.BodyWidth, 1));
        Shape(coat, new(-27,-28),new(-27,-36),new(-23,-41),new(-15,-43),new(-5,-42),new(8,-42),new(19,-40),new(24,-34),new(23,-27),new(16,-22),new(-14,-22),new(-22,-24));
        DrawColoredPolygon(new Vector2[]{new(-23,-29),new(-10,-27),new(14,-27),new(22,-32),new(18,-24),new(11,-22),new(-15,-23)},shade);
        DrawPolyline(new Vector2[]{new(-22,-38),new(-15,-40),new(-5,-39)},coat.Lightened(.16f),2,true);
        if (a.Dappled)
            foreach(var p in new Vector2[]{new(-20,-35),new(-15,-32),new(-22,-29),new(-16,-26)})
                DrawCircle(p, 2.1f, coat.Lightened(.19f));
        DrawSetTransform(new(0, BodyBob), 0, new Vector2(Facing, 1));
        Shape(coat, new(11,-36),new(17,-45),new(22,-55),new(28,-58),new(32,-52),new(29,-42),new(25,-29),new(19,-26));
        DrawColoredPolygon(new Vector2[]{new(17,-36),new(23,-49),new(25,-54),new(22,-42),new(23,-31)},shade);
        if(a.LongMane)
            Shape(a.Mane,new(25,-57),new(21,-54),new(17,-46),new(12,-37),new(16,-33),new(19,-39),new(19,-35),new(23,-46));
        else
            Shape(a.Mane,new(25,-57),new(21,-57),new(19,-52),new(17,-48),new(16,-43),new(14,-39),new(18,-40),new(24,-52));
        Shape(coat,new(25,-55),new(24,-64),new(28,-61),new(30,-55));
        Shape(coat.Lightened(.09f),new(30,-56),new(32,-64),new(34,-61),new(34,-54));
        Shape(coat.Lightened(.06f),new(26,-56),new(34,-56),new(38,-51),new(45,-46),new(44,-41),new(39,-40),new(33,-44),new(28,-44));
        Shape(shade,new(40,-47),new(45,-46),new(44,-41),new(40,-41),new(38,-44));
        if(a.FaceBlaze) DrawPolyline(new Vector2[]{new(34,-54),new(36,-49),new(40,-45)},Ivory,2.4f,true);
        DrawCircle(new(33,-51),1.1f,Outline);
        DrawCircle(new(43,-44),.75f,Outline);
        DrawPolyline(new Vector2[]{new(29,-55),new(31,-44),new(40,-42)},Leather,1.5f,true);
        DrawLine(new(40,-47),new(38,-41),Leather,1.5f,true);
        // 近腿在躯干前、远腿在后；髋随马背起伏，支撑蹄保持地面高度。
        DrawSetTransform(Vector2.Zero, 0, new Vector2(Facing, 1));
        DrawLeg(false,true,coat);
        DrawLeg(true,true,coat);
        DrawSetTransform(new(0,BodyBob),0,new Vector2(Facing,1));
        if(a.SaddleBags)
        {
            Shape(a.Blanket,new(-14,-41),new(11,-41),new(12,-27),new(-14,-27));
            DrawLine(new(-13,-28),new(11,-28),a.Trim,2,true);
            Shape(new Color("775638"),new(-22,-38),new(-13,-38),new(-12,-27),new(-21,-26));
            DrawLine(new(-21,-34),new(-13,-34),a.Trim,1.5f,true);
        }
        else
        {
            Shape(a.Blanket,new(-13,-41),new(12,-41),new(14,-30),new(2,-24),new(-13,-30));
            DrawPolyline(new Vector2[]{new(-12,-30),new(2,-25),new(13,-30)},a.Trim,2,true);
        }
        DrawLine(new(7,-38),new(5,-23),Leather,2,true);
        Shape(Leather,new(-11,-43),new(-8,-45),new(-5,-42),new(5,-42),new(9,-45),new(12,-42),new(9,-38),new(-9,-38));
        DrawPolyline(new Vector2[]{new(2,-39),new(1,-23),new(5,-23),new(6,-27)},a.Trim,1.3f,true);
        if (Rider != null && Rider.IsMounted)
            DrawPolyline(new Vector2[]{new(39,-43),new(25,-40),new(10,-48)},Leather,1.2f,true);
        DrawSetTransform(Vector2.Zero);
    }

    private void DrawLeg(bool front, bool near, Color color)
    {
        var pose = HorseLocomotion.Sample(front,near,GaitPhase,GaitWeight,Appearance.BodyWidth);
        // 上臂/大腿由厚至薄，前臂/胫骨较细，管骨与系部最细，关节不再是一根等宽折线。
        DrawBone(pose.Root,pose.Proximal,front ? 4.4f : 5.2f,front ? 3.3f : 3.7f,color);
        DrawBone(pose.Proximal,pose.Joint,front ? 3.3f : 3.7f,2.1f,color);
        DrawBone(pose.Joint,pose.Fetlock,1.8f,1.4f,color);
        DrawBone(pose.Fetlock,pose.Hoof,1.5f,1.7f,Appearance.FaceBlaze && near ? Ivory : color);
        DrawCircle(pose.Joint,2.0f,color.Darkened(.1f));
        DrawCircle(pose.Fetlock,1.7f,color);
        // 蹄形随系部回收旋转，承重相位的蹄底始终平放在地面。
        Vector2[] hoof = { new(-2.5f,-3),new(1.4f,-3),new(3.2f,0),new(-2.7f,0) };
        for(int i=0;i<hoof.Length;i++) hoof[i]=pose.Hoof+hoof[i].Rotated(pose.HoofAngle);
        Shape(Appearance.Mane,hoof);
    }

    private void DrawBone(Vector2 start, Vector2 end, float rootWidth, float tipWidth, Color color)
    {
        Vector2 axis=(end-start).Normalized(), side=new(-axis.Y,axis.X);
        Shape(color,start+side*rootWidth,end+side*tipWidth,end-side*tipWidth,start-side*rootWidth);
    }
    /// <summary>细实线沿形体外沿，保留亚像素运动连续性，不使用大面积柔化。</summary>
    private void Shape(Color fill, params Vector2[] points)
    {
        DrawColoredPolygon(points,fill);
        var border = new Vector2[points.Length + 1];
        points.CopyTo(border,0); border[^1]=points[0];
        DrawPolyline(border,Outline,1,true);
    }
}



