using Godot;

/// <summary>壁烛/随身火把的局部光源；共享渐变纹理，每盏灯各自启用，不提升环境亮度。</summary>
public partial class CaveFlame : Node2D
{
    public bool IsTorch { get; set; }
    public bool Burning { get; set; }
    public PointLight2D Light { get; private set; }
    private float _time;
    private static Texture2D _lightTexture;
    private CaveFireSprite _fire;
    public override void _Ready()
    {
        Light=CreateLight(IsTorch ? 108 : 215);
        Light.Position=new(0,-7); Light.Enabled=Burning; AddChild(Light);
        // 火焰本体自发光，不被自身依附的岩壁阴影压黑；灯台和木柄仍接受场景光照。
        _fire=new CaveFireSprite { Material=new CanvasItemMaterial { LightMode=CanvasItemMaterial.LightModeEnum.Unshaded } };
        AddChild(_fire);
    }
    public static PointLight2D CreateLight(float radius)
    {
        if(_lightTexture==null)
        {
            using var img=Image.CreateEmpty(256,256,false,Image.Format.Rgba8);
            for(int y=0;y<256;y++) for(int x=0;x<256;x++)
            {
                float d=new Vector2(x-127.5f,y-127.5f).Length()/128;
                float value=Mathf.Pow(Mathf.Clamp(1-d,0,1),1.25f);
                img.SetPixel(x,y,new Color(value,value,value,1));
            }
            _lightTexture=ImageTexture.CreateFromImage(img);
        }
        return new PointLight2D { Texture=_lightTexture,TextureScale=radius/128,
            Color=new Color("ffe9bf"),Energy=1.1f,ShadowEnabled=true,
            ShadowFilter=Light2D.ShadowFilterEnum.Pcf5 };
    }
    public override void _Process(double delta)
    {
        _time+=(float)delta;
        Light.Enabled=Burning;
        Light.Energy=(IsTorch ? .8f : 1.1f)+Mathf.Sin(_time*4.1f)*.025f;
        _fire.Visible=Burning; _fire.Time=_time; _fire.QueueRedraw();
        QueueRedraw();
    }
    public override void _Draw()
    {
        if(IsTorch)
        {
            if(!Burning) return;
            DrawLine(new(0,1),new(0,-14),new Color("967048"),3);
            DrawRect(new Rect2(-3,-18,6,8),new Color("bda578"));
        }
        else
        {
            DrawRect(new Rect2(-8,-11,16,4),new Color("454c46"));
            DrawLine(new(-7,-8),new(0,0),new Color("8b8066"),2);
            DrawRect(new Rect2(-3,-21,6,11),new Color("dfc797"));
            DrawLine(new(0,-24),new(0,-21),new Color("514a39"),1);
        }
    }
}

/// <summary>独立自发光火焰轮廓，不承载任何点火状态或玩法。</summary>
public partial class CaveFireSprite : Node2D
{
    public float Time { get; set; }
    public override void _Draw()
    {
        float sway=Mathf.Sin(Time*5)*1.1f;
        DrawColoredPolygon(new Vector2[]{new(-4,-19),new(-3,-25),new(sway+1,-33),new(4,-23),new(3,-18)},new Color("eeb364"));
        DrawColoredPolygon(new Vector2[]{new(-2,-19),new(sway,-28),new(2,-19)},new Color("fff0bd"));
    }
}
