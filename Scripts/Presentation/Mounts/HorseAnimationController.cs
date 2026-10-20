using Godot;

/// <summary>独立动作时钟：路程相位、起停权重、朝向与物理帧间插值，无人物/城市依赖。</summary>
[GlobalClass]
public partial class HorseAnimationController : Node
{
    [Export(PropertyHint.Range,"0.1,30,0.1")] public float TransitionSpeed { get; set; }=9;
    public float GaitPhase { get; private set; }
    public float RenderGaitPhase { get; private set; }
    public float GaitWeight { get; private set; }
    public float Facing { get; private set; }=1;
    public Vector2 RenderedWorldPosition { get; private set; }
    private float _lastDistance,_previousDistance,_currentDistance;
    private Vector2 _previousPosition,_currentPosition;
    private bool _hasMotionSample;
    public void ResetMotion(HorseMotionSample sample)
    {
        _lastDistance=sample.Distance;
        SetPose(0,0,sample.Facing.X<-.1f?-1:1); ResetSamples(sample);
    }
    public void SetPose(float phase,float weight,float facing=1)
    {
        GaitPhase=RenderGaitPhase=Mathf.PosMod(phase,1);
        GaitWeight=Mathf.Clamp(weight,0,1); Facing=facing<0?-1:1;
    }
    private void ResetSamples(HorseMotionSample sample)
    {
        _hasMotionSample=true;
        _previousPosition=_currentPosition=RenderedWorldPosition=sample.WorldPosition;
        _previousDistance=_currentDistance=sample.Distance;
    }
    /// <summary>宿主完成碰撞移动之后调用。传送或路程计数归零时丢弃旧插值段。</summary>
    public void CapturePhysics(HorseMotionSample sample)
    {
        if(sample.Distance<_currentDistance) { ResetMotion(sample); return; }
        float travelled=Mathf.Max(0,sample.Distance-_currentDistance);
        if(!_hasMotionSample || sample.WorldPosition.DistanceTo(_currentPosition)>travelled+1)
        { ResetSamples(sample); return; }
        _previousPosition=_currentPosition; _previousDistance=_currentDistance;
        _currentPosition=sample.WorldPosition; _currentDistance=sample.Distance;
    }
    /// <summary>绘制帧调用，fraction取引擎物理插值比例；不回写宿主的物理位置。</summary>
    public void AdvanceMotion(HorseMotionSample sample,float delta,float fraction)
    {
        if(sample.Distance<_lastDistance) ResetMotion(sample);
        float distance=Mathf.Max(0,sample.Distance-_lastDistance); _lastDistance=sample.Distance;
        if(sample.Enabled)
        {
            if(Mathf.Abs(sample.Facing.X)>.1f) Facing=Mathf.Sign(sample.Facing.X);
            GaitPhase=Mathf.PosMod(GaitPhase+distance/HorseLocomotion.CycleDistance,1);
            GaitWeight=Mathf.MoveToward(GaitWeight,sample.Velocity.LengthSquared()>1?1:0,Mathf.Max(0,delta)*TransitionSpeed);
            if(!_hasMotionSample || !sample.WorldPosition.IsEqualApprox(_currentPosition)) ResetSamples(sample);
            fraction=Mathf.Clamp(fraction,0,1);
            RenderedWorldPosition=_previousPosition.Lerp(_currentPosition,fraction);
            float renderedDistance=Mathf.Lerp(_previousDistance,_currentDistance,fraction);
            RenderGaitPhase=Mathf.PosMod(GaitPhase-(sample.Distance-renderedDistance)/HorseLocomotion.CycleDistance,1);
        }
        else { GaitWeight=0; RenderGaitPhase=GaitPhase; RenderedWorldPosition=sample.WorldPosition; }
    }
    /// <summary>原地展示用时钟；正式移动必须使用实际路程，不能用此方法替代接地约束。</summary>
    public void AdvancePreview(float delta,float speed,float facing=1) =>
        SetPose(GaitPhase+Mathf.Max(0,speed)*Mathf.Max(0,delta)/HorseLocomotion.CycleDistance,speed>0?1:0,facing);
}
