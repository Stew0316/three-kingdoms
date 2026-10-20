using Godot;

/// <summary>外观组件：骨架、连续蒙皮、鞍具与阴影；只接收姿态，不维护动作时钟。</summary>
[GlobalClass]
public partial class HorseVisual : Node2D
{
    [Export] public HorseAppearance Appearance { get; set; }
    [Export] public bool ShowShadow { get; set; }=true;
    public HorsePaintedRig PaintedRig { get; private set; }
    public float Facing { get; private set; }=1;
    private float _phase,_weight,_submittedPhase,_submittedWeight,_submittedFacing;
    private bool _hasSubmittedPose;
    private HorseAppearance _boundAppearance;
    private static PackedScene _rigScene;
    public override void _Ready() => SetPose(0,0);
    public void SetPose(float phase,float weight,float facing=1)
    {
        _phase=Mathf.PosMod(phase,1); _weight=Mathf.Clamp(weight,0,1); Facing=facing<0?-1:1;
        if(!IsInsideTree()) return;
        if(Appearance?.PartsAtlas==null) { ClearRig(); QueueRedraw(); return; }
        if(_boundAppearance!=Appearance)
        {
            ClearRig();
            _rigScene??=GD.Load<PackedScene>("res://Components/Mounts/HorseRig.tscn");
            PaintedRig=_rigScene.Instantiate<HorsePaintedRig>();
            PaintedRig.Appearance=Appearance; AddChild(PaintedRig); _boundAppearance=Appearance;
        }
        if(!IsVisibleInTree()) { _hasSubmittedPose=false; return; }
        if(_hasSubmittedPose && _submittedPhase==_phase && _submittedWeight==_weight && _submittedFacing==Facing) return;
        PaintedRig.Scale=new(Facing,1); PaintedRig.ApplyPose(_phase,_weight);
        _submittedPhase=_phase; _submittedWeight=_weight; _submittedFacing=Facing; _hasSubmittedPose=true; QueueRedraw();
    }
    private void ClearRig()
    {
        if(PaintedRig!=null) { RemoveChild(PaintedRig); PaintedRig.QueueFree(); PaintedRig=null; }
        _boundAppearance=null; _hasSubmittedPose=false;
    }
    public override void _Draw()
    {
        if(!ShowShadow || Appearance?.PartsAtlas==null) return;
        var shadow=HorseShadow.Sample(_phase,_weight,Appearance.BodyWidth);
        DrawSetTransform(shadow.Offset,0,shadow.Scale);
        DrawCircle(Vector2.Zero,1,new Color(0,0,0,shadow.Opacity)); DrawSetTransform(Vector2.Zero);
    }
}
