using Godot;

/// <summary>现有Combatant与通用马组件之间的薄适配层；动画、骨架和渲染由HorseActor组合。</summary>
public partial class CityHorse : HorseActor,IHorseMotionSource,IHorseRiderContacts
{
    private Combatant _rider;
    public Combatant Rider
    {
        get => _rider;
        set
        {
            UnbindRider(); _rider=value;
            MotionSource=value==null?null:this;
            RiderContacts=value==null?null:this;
            if(IsNodeReady()) { BindRider(); ResetGait(); }
        }
    }
    private static PackedScene _cityScene;
    public static CityHorse CreateForCity(HorseAppearance appearance)
    {
        _cityScene??=GD.Load<PackedScene>("res://Components/Mounts/CityHorse.tscn");
        var horse=_cityScene.Instantiate<CityHorse>(); horse.Appearance=appearance; return horse;
    }
    HorseMotionSample IHorseMotionSource.HorseMotion =>
        new(_rider.GlobalPosition,_rider.WalkDistance,_rider.WalkVelocity,_rider.FacingDirection,_rider.IsMounted);
    bool IHorseRiderContacts.HasRiderContacts => _rider?.IsMounted==true && _rider.Rig!=null;
    Vector2 IHorseRiderContacts.ReinHandWorld => _rider.Rig.RidingReinWorld;
    Vector2 IHorseRiderContacts.StirrupFootWorld => _rider.Rig.RidingSoleWorld;
    public override void _Ready() { base._Ready(); BindRider(); }
    public override void _ExitTree() => UnbindRider();
    private void BindRider() { if(_rider?.Rig!=null) _rider.Rig.RidingMount=this; }
    private void UnbindRider()
    {
        if(GodotObject.IsInstanceValid(_rider) && GodotObject.IsInstanceValid(_rider.Rig) && _rider.Rig.RidingMount==this)
            _rider.Rig.RidingMount=null;
    }
}
