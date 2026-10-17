using Godot;

/// <summary>实例步态时钟与骑乘连接；真实路程驱动全身骨架，外观不决定速度或碰撞。</summary>
public partial class CityHorse : Node2D
{
    public HorseAppearance Appearance { get; set; }
    public Combatant Rider { get; set; }
    public float GaitPhase { get; private set; }
    public float GaitWeight { get; private set; }
    public float Facing { get; private set; } = 1;
    public float BodyBob => HorseLocomotion.Bob(GaitPhase, GaitWeight);
    public float BodyPitch => HorseLocomotion.BodyPitch(GaitPhase, GaitWeight);
    public float RiderLean => HorseLocomotion.RiderLean(GaitPhase, GaitWeight);
    public HorsePaintedRig PaintedRig { get; private set; }
    public const float CycleDistance = HorseLocomotion.CycleDistance;
    public Vector2 SaddleOffset
    {
        get
        {
            Vector2 offset = HorseBodyMotion.Sample(GaitPhase, GaitWeight, Appearance?.BodyWidth ?? 1).Saddle - new Vector2(0,-43);
            return new(offset.X * Facing, offset.Y);
        }
    }
    private float _lastDistance;
    private HorseAppearance _boundAppearance;
    private Node2D _connections;
    private static readonly Color Leather = new("49392c");

    public override void _Ready()
    {
        ProcessPriority = -10; // 先更新马身，骑手再读取同帧鞍点。
        _connections = new Node2D { Name = "缰绳与马镫" };
        AddChild(_connections);
        _connections.Draw += DrawConnections;
        ResetGait();
    }
    public void ResetGait()
    {
        _lastDistance = Rider?.WalkDistance ?? 0;
        GaitPhase = GaitWeight = 0;
        Facing = Rider?.FacingDirection.X < -.1f ? -1 : 1;
        UpdateRig();
    }
    internal void SetPreviewPose(float phase, float weight)
    {
        if (Rider != null) return;
        GaitPhase = Mathf.PosMod(phase, 1);
        GaitWeight = weight;
        UpdateRig();
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
        UpdateRig();
    }
    private void UpdateRig()
    {
        if (!IsInsideTree() || Appearance?.PartsAtlas == null) return;
        if (_boundAppearance != Appearance)
        {
            if (PaintedRig != null) { RemoveChild(PaintedRig); PaintedRig.QueueFree(); }
            PaintedRig = new HorsePaintedRig { Name = "彩绘马骨架", Appearance = Appearance };
            AddChild(PaintedRig); MoveChild(PaintedRig, 0);
            _boundAppearance = Appearance;
        }
        PaintedRig.Scale = new(Facing, 1);
        PaintedRig.ApplyPose(GaitPhase, GaitWeight);
        QueueRedraw();
        _connections?.QueueRedraw();
    }
    public static Vector2 SampleHoof(float phase) => HorseLocomotion.HoofOffset(phase, true);

    public override void _Draw()
    {
        DrawSetTransform(new(0,3),0,new(34,6));
        DrawCircle(Vector2.Zero,1,new Color(0,0,0,.14f));
        DrawSetTransform(Vector2.Zero);
    }
    private void DrawConnections()
    {
        if (PaintedRig == null) return;
        Vector2 saddle = ToLocal(PaintedRig.SaddleWorld);
        Vector2 sole = Rider?.IsMounted == true && Rider.Rig != null
            ? ToLocal(Rider.Rig.RidingSoleWorld) : saddle + new Vector2(6*Facing,20);
        _connections.DrawLine(saddle+new Vector2(2*Facing,3),sole+new Vector2(-2*Facing,-3),Leather,.85f,true);
        _connections.DrawPolyline(new[]{sole+new Vector2(-3,-3),sole+new Vector2(-3,.6f),
            sole+new Vector2(3,.6f),sole+new Vector2(3,-3)},Appearance.Trim,.8f,true);
        if (Rider?.IsMounted != true || Rider.Rig == null) return;
        Vector2 mouth = ToLocal(PaintedRig.BridleWorld);
        Vector2 hand = ToLocal(Rider.Rig.RidingReinWorld);
        // 细缰绳自然下垂，端点取最终渲染的辔环与拳掌，不能用未求解的手部目标替代。
        Vector2 mid = mouth.Lerp(hand,.5f)+new Vector2(0,3.2f);
        var curve = new Vector2[13];
        for (int i=0;i<curve.Length;i++)
        {
            float t=(float)i/(curve.Length-1);
            curve[i]=mouth*(1-t)*(1-t)+2*mid*t*(1-t)+hand*t*t;
        }
        _connections.DrawPolyline(curve,Leather,.8f,true);
    }
}
