using Godot;

/// <summary>铜框日月时钟：中央日光/月光柔和交替，十二枚圆形刻点按时辰明暗变化。</summary>
public partial class DayNightDial : Control
{
    private static readonly Vector2 Center=new(60,23);
    private static readonly Color Gold=new("efbc56"), Blue=new("7db9ef");
    private static readonly Color Bronze=new("927047"), Edge=new("d1ae72"), Ink=new("111e25");
    private ShaderMaterial _haloMaterial;
    // 光晕相位与全局时辰分开：只改变外圈细光丝，不影响五分钟一格的推进。
    private double _haloPhase;
    private float Daylight => Mathf.SmoothStep(-.14f,.14f,Mathf.Sin((float)AdventureSession.Instance.Clock.Position/12*Mathf.Tau));
    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Pass;
        _haloMaterial=new ShaderMaterial { Shader=GD.Load<Shader>("res://Resources/Shaders/DayNightHalo.gdshader") };
        _haloMaterial.SetShaderParameter("daylight",Daylight);
        // 先在表盘后方绘制，再由铜框盖住内缘；扩散区域不参与鼠标命中。
        AddChild(new ColorRect { Name="OuterHalo",Position=Center-new Vector2(48,48),Size=new(96,96),
            Material=_haloMaterial,MouseFilter=MouseFilterEnum.Ignore,ShowBehindParent=true });
    }
    public override void _Process(double delta)
    {
        var clock=AdventureSession.Instance.Clock;
        if(!GetTree().Paused) _haloPhase=(_haloPhase+delta*.20)%Mathf.Tau;
        _haloMaterial.SetShaderParameter("drift_phase",(float)_haloPhase);
        _haloMaterial.SetShaderParameter("daylight",Daylight);
        TooltipText=$"{(clock.IsDay ? "日光" : "月光")} · 第 {clock.Segment+1} / 12 时辰\n每格 {AdventureSession.Instance.Settings.DayNightSecondsPerSegment/60:0.#} 分钟";
        QueueRedraw();
    }
    public override void _Draw()
    {
        var clock=AdventureSession.Instance.Clock;
        // 日出/日落附近缓慢交替中央图案和光色；每个点的周期仍来自唯一全局时钟。
        float day=Daylight;
        Color light=Blue.Lerp(Gold,day);
        DrawCircle(Center+new Vector2(0,1),23,new Color(0,0,0,.32f),true,-1,true);
        DrawCircle(Center,22.2f,new Color("332b22"),true,-1,true);
        DrawArc(Center,21.7f,0,Mathf.Tau,96,Bronze,1.2f,true);
        DrawArc(Center,21.7f,Mathf.Pi,Mathf.Tau,48,Edge,.65f,true);
        DrawCircle(Center,19.8f,Ink,true,-1,true);
        DrawArc(Center,15.1f,0,Mathf.Tau,72,new Color("6a5940"),.65f,true);
        DrawCircle(Center,13.9f,new Color("0a141c"),true,-1,true);
        // 多层低透明度径向光晕，不使用矩形底板或生硬的单色圆饼。
        Glow(Center,14,light,.60f);
        for(int ring=28;ring>=1;ring--)
        {
            float radius=12.3f*ring/28;
            float inner=1-ring/28f;
            var tint=new Color("1a3048").Lerp(new Color("805322"),day).Lerp(light,inner*.28f);
            DrawCircle(Center,radius,tint,true,-1,true);
        }
        Sun(Center,day);
        Moon(Center,1-day);
        // 十二圆点代替横排格子，相邻刻点随格内进度交替点亮。
        for(int i=0;i<12;i++)
        {
            float angle=-Mathf.Pi/2+i*Mathf.Tau/12;
            Vector2 point=Center+Vector2.FromAngle(angle)*18.1f;
            float distance=Mathf.Abs(i-(float)clock.Position); distance=Mathf.Min(distance,12-distance);
            float strength=Mathf.Clamp(1-distance,0,1);
            Color hue=i<6 ? Gold : Blue;
            DrawCircle(point,1.8f,new Color("090e11"),true,-1,true);
            if(strength>0) Glow(point,3.4f,hue,strength*.5f);
            DrawCircle(point,1.25f,hue.Darkened(.82f*(1-strength)),true,-1,true);
            if(strength>.1f) DrawCircle(point+new Vector2(-.3f,-.3f),.45f,new Color(1,.96f,.82f,strength),true,-1,true);
        }
        // 细指针只走表圈，不遮挡日月图案；一整圈对应十二格。
        float hand=-Mathf.Pi/2+(float)clock.Position*Mathf.Tau/12;
        Vector2 direction=Vector2.FromAngle(hand), tangent=direction.Orthogonal();
        DrawColoredPolygon(new[]{Center+direction*14.7f,Center+direction*16.6f+tangent*.8f,Center+direction*16.6f-tangent*.8f},light.Lightened(.35f));
        DrawArc(Center,12.8f,Mathf.Pi*1.08f,Mathf.Pi*1.78f,28,new Color(1,.95f,.8f,.18f),.7f,true);
    }
    private void Sun(Vector2 center,float opacity)
    {
        if(opacity<=0) return;
        Glow(center,10,Gold,opacity*.6f);
        for(int i=0;i<12;i++)
        {
            Vector2 direction=Vector2.FromAngle(i*Mathf.Tau/12);
            DrawLine(center+direction*6.8f,center+direction*(i%2==0 ? 9 : 8),new Color(1,.75f,.3f,opacity*.8f),.7f,true);
        }
        DrawCircle(center,5.3f,new Color(.96f,.66f,.18f,opacity),true,-1,true);
        DrawCircle(center+new Vector2(-.5f,-.6f),4.5f,new Color(1,.84f,.39f,opacity),true,-1,true);
        DrawCircle(center+new Vector2(-1,-1),3.2f,new Color(1,.94f,.68f,opacity*.8f),true,-1,true);
    }
    private void Moon(Vector2 center,float opacity)
    {
        if(opacity<=0) return;
        Glow(center,10,Blue,opacity*.6f);
        // 外弧与内弧组成真正的月牙轮廓，避免拿实色圆遮掉渐变光晕。
        var crescent=new Vector2[50];
        for(int i=0;i<=24;i++)
        {
            float angle=Mathf.Lerp(-Mathf.Pi/2,Mathf.Pi/2,i/24f);
            crescent[i]=center+new Vector2(-Mathf.Cos(angle)*6,Mathf.Sin(angle)*7);
            float y=Mathf.Lerp(7,-7,i/24f);
            crescent[25+i]=center+new Vector2(-Mathf.Sqrt(Mathf.Max(0,1-y*y/49))*2.2f+1.2f,y);
        }
        DrawColoredPolygon(crescent,new Color(.76f,.89f,1,opacity));
        DrawCircle(center+new Vector2(5,-5),.65f,new Color(.84f,.94f,1,opacity*.9f),true,-1,true);
        DrawCircle(center+new Vector2(7,1),.4f,new Color(.72f,.86f,1,opacity*.6f),true,-1,true);
    }
    private void Glow(Vector2 center,float radius,Color color,float opacity)
    {
        for(int i=10;i>=1;i--)
        {
            float weight=1-i/11f;
            DrawCircle(center,radius*i/10,new Color(color.R,color.G,color.B,opacity*weight*.12f),true,-1,true);
        }
    }
}
