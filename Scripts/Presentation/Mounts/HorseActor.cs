using Godot;

/// <summary>可复用马场景入口，组合动作、外观和可选骑乘连接；不负责输入、碰撞或玩法速度。</summary>
[GlobalClass]
public partial class HorseActor : Node2D
{
    [Export] public HorseAppearance Appearance { get; set; }
    [Export] public bool AutoPlayPreview { get; set; }
    [Export(PropertyHint.Range,"0,500,1")] public float PreviewSpeed { get; set; }=190;
    [Export] public bool PreviewFaceLeft { get; set; }
    public IHorseMotionSource MotionSource { get; set; }
    public IHorseRiderContacts RiderContacts { get; set; }
    public HorseAnimationController Animation { get; private set; }
    public HorseVisual Visual { get; private set; }
    public HorseRiderAttachments Attachments { get; private set; }
    public HorsePaintedRig PaintedRig => Visual?.PaintedRig;
    public float GaitPhase => Animation?.GaitPhase ?? 0;
    public float RenderGaitPhase => Animation?.RenderGaitPhase ?? 0;
    public float GaitWeight => Animation?.GaitWeight ?? 0;
    public float Facing => Animation?.Facing ?? 1;
    public Vector2 RenderedGlobalPosition => GlobalPosition;
    public float BodyBob => HorseLocomotion.Bob(RenderGaitPhase,GaitWeight);
    public float BodyPitch => HorseLocomotion.BodyPitch(RenderGaitPhase,GaitWeight);
    public float RiderLean => HorseLocomotion.RiderLean(RenderGaitPhase,GaitWeight);
    public const float CycleDistance=HorseLocomotion.CycleDistance;
    public Vector2 SaddleOffset
    {
        get
        {
            Vector2 offset=HorseBodyMotion.Sample(RenderGaitPhase,GaitWeight,Appearance?.BodyWidth??1).Saddle-new Vector2(0,-43);
            return new(offset.X*Facing,offset.Y);
        }
    }
    private static PackedScene _scene;
    public static HorseActor Create(HorseAppearance appearance=null)
    {
        appearance??=HorseAppearanceCatalog.Chestnut;
        _scene??=GD.Load<PackedScene>("res://Components/Mounts/Horse.tscn");
        var horse=_scene.Instantiate<HorseActor>();
        horse.Appearance=appearance;
        return horse;
    }
    public override void _Ready()
    {
        ProcessPriority=-10; // 马先更新，骑手和镜头再读取同帧姿态。
        ProcessPhysicsPriority=10; // 默认宿主物理回调完成后取样。
        // 场景提供组合；代码实例化时也复用同一组组件，不维护第二套动画实现。
        Animation=GetNodeOrNull<HorseAnimationController>("Animation");
        if(Animation==null) { Animation=new() {Name="Animation"}; AddChild(Animation); }
        Visual=GetNodeOrNull<HorseVisual>("Visual");
        if(Visual==null) { Visual=new() {Name="Visual"}; AddChild(Visual); }
        Attachments=GetNodeOrNull<HorseRiderAttachments>("RiderAttachments");
        if(Attachments==null) { Attachments=new() {Name="RiderAttachments"}; AddChild(Attachments); }
        Attachments.Horse=Visual; ResetGait();
    }
    /// <summary>上马、换马或更换运动来源后重置；无来源时保留场景摆放位置。</summary>
    public void ResetGait()
    {
        if(Animation==null) return;
        if(MotionSource!=null)
        {
            var sample=MotionSource.HorseMotion;
            Animation.ResetMotion(sample); GlobalPosition=sample.WorldPosition;
        }
        else Animation.SetPose(0,0,PreviewFaceLeft?-1:1);
        RefreshVisual();
    }
    public void SetPreviewPose(float phase,float weight,float facing=1)
    {
        if(MotionSource!=null || Animation==null) return;
        AutoPlayPreview=false; Animation.SetPose(phase,weight,facing); RefreshVisual();
    }
    public override void _PhysicsProcess(double delta)
    {
        if(MotionSource!=null) Animation.CapturePhysics(MotionSource.HorseMotion);
    }
    public override void _Process(double delta)
    {
        if(MotionSource!=null)
        {
            Animation.AdvanceMotion(MotionSource.HorseMotion,(float)delta,(float)Engine.GetPhysicsInterpolationFraction());
            // 只调整表现节点；宿主碰撞体的位置和移动规则不回写。
            GlobalPosition=Animation.RenderedWorldPosition;
        }
        else if(AutoPlayPreview) Animation.AdvancePreview((float)delta,PreviewSpeed,PreviewFaceLeft?-1:1);
        RefreshVisual();
    }
    private void RefreshVisual()
    {
        if(Visual==null) return;
        Visual.Appearance=Appearance; Visual.SetPose(RenderGaitPhase,GaitWeight,Facing);
        Attachments.Rider=RiderContacts; Attachments.QueueRedraw();
    }
    public static Vector2 SampleHoof(float phase) => HorseLocomotion.HoofOffset(phase,true);
    public static HorseShadow.Pose SampleShadow(float phase,float weight,float bodyWidth=1) => HorseShadow.Sample(phase,weight,bodyWidth);
}
