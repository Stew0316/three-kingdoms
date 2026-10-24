using Godot;

/// <summary>圆形天空日月钟：日月按全局时辰升落，金属外圈的十二枚镶点连续交接。</summary>
public partial class DayNightDial : Control
{
    private static readonly Vector2 Center=new(60,23);
    private static readonly Color Gold=new("ffce45"), Blue=new("55caff");
    private ShaderMaterial _skyMaterial;
    // 天空与刻点只有同一个时间源；暂停、切图和调时均直接服从会话时钟。
    private float Daylight => Mathf.SmoothStep(-.18f,.18f,Mathf.Sin((float)AdventureSession.Instance.Clock.Position/12*Mathf.Tau));
    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Pass;
        _skyMaterial=new ShaderMaterial { Shader=GD.Load<Shader>("res://Resources/Shaders/DayNightSky.gdshader") };
        _skyMaterial.SetShaderParameter("cycle",(float)AdventureSession.Instance.Clock.Position/12);
        // 天空独立圆形裁切，太阳/月牙不会越过表框。
        AddChild(new ColorRect { Name="Sky",Position=Center-new Vector2(15,15),Size=new(30,30),
            Material=_skyMaterial,MouseFilter=MouseFilterEnum.Ignore });
    }
    public override void _Process(double delta)
    {
        var clock=AdventureSession.Instance.Clock;
        _skyMaterial.SetShaderParameter("cycle",(float)clock.Position/12);
        QueueRedraw();
    }
    public override void _Draw()
    {
        float position=(float)AdventureSession.Instance.Clock.Position;
        Color light=Blue.Lerp(Gold,Daylight);
        DrawCircle(Center+new Vector2(0,1),23,new Color(0,0,0,.45f),true,-1,true);
        Glow(Center,25,light,.12f);
        DrawCircle(Center,22.5f,new Color("242928"),true,-1,true);
        DrawArc(Center,22,0,Mathf.Tau,96,new Color("111817"),1.3f,true);
        DrawArc(Center,21.6f,Mathf.Pi,Mathf.Tau,48,new Color("979078"),.8f,true);
        DrawArc(Center,21.6f,0,Mathf.Pi,48,new Color("4d4839"),.9f,true);
        DrawCircle(Center,20.7f,new Color("121d24").Lerp(new Color("302c16"),Daylight),true,-1,true);
        // 铸铜分段与浅刻纹保留厚度，不在天空两侧附加尖角。
        for(int i=0;i<24;i++)
        {
            float angle=i*Mathf.Tau/24;
            Vector2 radial=Vector2.FromAngle(angle);
            DrawLine(Center+radial*21,Center+radial*22.1f,new Color("635f50"),.55f,true);
        }
        DrawArc(Center,15.5f,0,Mathf.Tau,96,light.Darkened(.40f),1.5f,true);
        DrawArc(Center,15.1f,0,Mathf.Tau,96,light.Lightened(.25f),.65f,true);
        // 全圈随昼夜变金/蓝，相邻镶点交接亮度；保留原有十二时辰语义。
        for(int i=0;i<12;i++)
        {
            float angle=-Mathf.Pi/2+i*Mathf.Tau/12;
            Vector2 point=Center+Vector2.FromAngle(angle)*18.6f;
            float distance=Mathf.Abs(i-position); distance=Mathf.Min(distance,12-distance);
            float strength=Mathf.SmoothStep(0,1,Mathf.Clamp(1-distance,0,1));
            DrawCircle(point+new Vector2(0,.3f),2.05f,new Color("080e12"),true,-1,true);
            Glow(point,3.2f,light,.18f+strength*.7f);
            DrawArc(point,1.65f,0,Mathf.Tau,24,light.Darkened(.24f),.6f,true);
            DrawCircle(point,1.13f,light.Darkened(.85f*(1-strength)),true,-1,true);
            DrawCircle(point+new Vector2(-.2f,-.35f),.5f,new Color(1,1,.9f,.15f+strength*.85f),true,-1,true);
        }
    }
    private void Glow(Vector2 center,float radius,Color color,float opacity)
    {
        for(int i=8;i>=1;i--)
            DrawCircle(center,radius*i/8,new Color(color.R,color.G,color.B,opacity*(1-i/9f)*.12f),true,-1,true);
    }
}
