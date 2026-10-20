using Godot;

/// <summary>可选缰绳/马镫组件，接触点可用Marker2D或接口供给，不依赖人物类型。</summary>
[GlobalClass]
public partial class HorseRiderAttachments : Node2D
{
    [Export] public HorseVisual Horse { get; set; }
    [Export] public Node2D ReinTarget { get; set; }
    [Export] public Node2D FootTarget { get; set; }
    public IHorseRiderContacts Rider { get; set; }
    private static readonly Color Leather=new("49392c");
    private readonly Vector2[] _curve=new Vector2[13],_stirrup=new Vector2[4];
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        if(Horse?.PaintedRig==null) return;
        // 绘制时读取端点，骑手即使晚于马更新，也使用同帧已求解的手脚。
        bool hasRider=Rider?.HasRiderContacts==true;
        Vector2 saddle=ToLocal(Horse.PaintedRig.SaddleWorld); float facing=Horse.Facing;
        Vector2 sole=FootTarget!=null?ToLocal(FootTarget.GlobalPosition):hasRider?
            ToLocal(Rider.StirrupFootWorld):saddle+new Vector2(6*facing,20);
        DrawLine(saddle+new Vector2(2*facing,3),sole+new Vector2(-2*facing,-3),Leather,.85f,true);
        _stirrup[0]=sole+new Vector2(-3,-3); _stirrup[1]=sole+new Vector2(-3,.6f);
        _stirrup[2]=sole+new Vector2(3,.6f); _stirrup[3]=sole+new Vector2(3,-3);
        DrawPolyline(_stirrup,Horse.Appearance.Trim,.8f,true);
        if(ReinTarget==null && !hasRider) return;
        Vector2 mouth=ToLocal(Horse.PaintedRig.BridleWorld);
        Vector2 hand=ReinTarget!=null?ToLocal(ReinTarget.GlobalPosition):ToLocal(Rider.ReinHandWorld);
        Vector2 mid=mouth.Lerp(hand,.5f)+new Vector2(0,3.2f);
        for(int i=0;i<_curve.Length;i++)
        {
            float t=i/(float)(_curve.Length-1);
            _curve[i]=mouth*(1-t)*(1-t)+2*mid*t*(1-t)+hand*t*t;
        }
        DrawPolyline(_curve,Leather,.8f,true);
    }
}
