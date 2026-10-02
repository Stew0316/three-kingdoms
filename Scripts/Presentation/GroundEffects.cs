using Godot;

/// <summary>二维地面表现：接触阴影、光环、角色差异粒子、方向、攻击预警和旋斩。</summary>
public partial class GroundEffects : Node2D
{
    public Combatant Actor { get; set; }
    public override void _Ready()
    {
        Actor ??= GetParentOrNull<Combatant>();
        // 先于角色绘制、晚于地图绘制；不能用负 Z，否则光环和阴影也会被 Z=0 的地图盖住。
        ZIndex=0;
        ShowBehindParent=true;
    }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        if(Actor==null || Actor.IsDead) return;
        var cfg=Actor.Presentation; var color=cfg?.AuraColor ?? new Color("ffaa55");
        float radius=cfg?.AuraRadius ?? 21, t=Actor.VisualTime;
        DrawSetTransform(new Vector2(2,2),0,new Vector2(1,.42f));
        DrawCircle(Vector2.Zero,radius*.72f,new Color(0,0,0,.30f));
        DrawSetTransform(Vector2.Zero);
        DrawSetTransform(Vector2.Zero,0,new Vector2(1,.48f));
        DrawCircle(Vector2.Zero,radius,new Color(color,.065f));
        for(int i=0;i<3;i++) DrawArc(Vector2.Zero,radius+i*2,0,Mathf.Tau,80,new Color(color,.55f-i*.16f),1.2f,true);
        for(int i=0;i<8;i++)
        {
            float a=i*Mathf.Tau/8+t*.35f;
            DrawArc(Vector2.Zero,radius-3,a,a+.23f,8,new Color(color,.85f),1.2f,true);
        }
        DrawSetTransform(Vector2.Zero);
        DrawCharacterParticles(cfg,t,radius);
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

    /// <summary>吕布使用上升火星，魏延使用环绕叶片；参数全部来自角色表现资源。</summary>
    private void DrawCharacterParticles(CharacterPresentationConfig cfg,float time,float radius)
    {
        Color color=cfg?.ParticleColor ?? Colors.White;
        int count=Mathf.Clamp(cfg?.ParticleCount ?? 16,6,24);
        bool orbit=cfg?.OrbitParticles==true;
        for(int i=0;i<count;i++)
        {
            float seed=i*.6180339f;
            float phase=(time*(orbit ? .45f : .62f)+seed)%1;
            float angle=i*2.39996f+time*(orbit ? 1.45f : .18f);
            float radial=radius*(.72f+.22f*Mathf.Sin(seed*19+time));
            Vector2 point=new(Mathf.Cos(angle)*radial,Mathf.Sin(angle)*radial*.42f);
            if(orbit)
            {
                Vector2 tangent=Vector2.FromAngle(angle+Mathf.Pi/2)*2.6f;
                Vector2 normal=tangent.Orthogonal().Normalized()*1.5f;
                DrawColoredPolygon(new[]{point-tangent,point+normal,point+tangent,point-normal},new Color(color,.22f+.58f*Mathf.Sin(phase*Mathf.Pi)));
            }
            else
            {
                point.Y-=phase*18;
                float size=.7f+Mathf.Sin(phase*Mathf.Pi)*1.5f;
                DrawCircle(point,size,new Color(color,.18f+.68f*Mathf.Sin(phase*Mathf.Pi)));
                DrawLine(point,point+new Vector2(-.7f,3.5f),new Color(color,.3f),1,true);
            }
        }
    }
}
