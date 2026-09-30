using Godot;

/// <summary>透明地面视口：光环、方向、攻击预警和旋斩。尺寸统一使用战斗平面单位。</summary>
public partial class GroundEffects : Node2D
{
    public Combatant Actor { get; set; }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        if(Actor.IsDead) return;
        var cfg=Actor.Presentation; var color=cfg?.AuraColor ?? new Color("ffaa55");
        float radius=cfg?.AuraRadius ?? 21, t=Actor.VisualTime;
        DrawCircle(Vector2.Zero,radius*.8f,new Color(0,0,0,.24f));
        DrawCircle(Vector2.Zero,radius,new Color(color,.055f));
        for(int i=0;i<3;i++) DrawArc(Vector2.Zero,radius+i*2,0,Mathf.Tau,80,new Color(color,.55f-i*.16f),1.2f,true);
        for(int i=0;i<8;i++)
        {
            float a=i*Mathf.Tau/8+t*.35f;
            DrawArc(Vector2.Zero,radius-3,a,a+.23f,8,new Color(color,.85f),1.2f,true);
        }
        Vector2 f=Actor.FacingDirection;
        DrawColoredPolygon(new[]{f*(radius+7),f*(radius+2)+f.Orthogonal()*2,f*(radius+2)-f.Orthogonal()*2},color);
        if(Actor.CounterRemaining>0)
        {
            float p=1-Actor.CounterRemaining/Actor.CounterDuration;
            float a=p*Mathf.Tau*1.5f;
            for(int i=0;i<3;i++) DrawArc(Vector2.Zero,Actor.CounterRadius-i*5,a+i*.3f,a+4.4f,70,new Color(color,(1-p)*(.9f-i*.2f)),4-i,true);
        }
        if(Actor.BattleFinished || Actor.CurrentState!=Combatant.State.Attack || Actor.ActionTime>Actor.Spec.Prepare+Actor.Spec.Active) return;
        float angle=Actor.CastDirection.Angle(), half=Actor.Spec.HalfAngle;
        Color warning=Actor.IsEnemy ? new("ff784b") : new("ffd797");
        var points=new Vector2[42]; points[0]=Vector2.Zero;
        for(int i=0;i<=40;i++) points[i+1]=Vector2.FromAngle(angle-half+half*2*i/40)*Actor.Spec.Range;
        DrawColoredPolygon(points,new Color(warning,.13f));
        DrawArc(Vector2.Zero,Actor.Spec.Range,angle-half,angle+half,50,new Color(warning,.9f),1,true);
        if(Actor.Spec.Prepare>0) DrawArc(Vector2.Zero,Actor.Spec.Range*Mathf.Clamp(Actor.ActionTime/Actor.Spec.Prepare,0,1),angle-half,angle+half,50,new Color(warning,.5f),1,true);
    }
}
