using Godot;

/// <summary>以清明上河图气质的独立古建素材绘制紧凑屋舍，足点/碰撞由CityMap管理。</summary>
public partial class CityBuilding : Node2D
{
    private Font _font;
    private Rect2 _region;
    public Vector2 BuildingSize { get; set; }
    public string Title { get; set; }="";
    public Color Roof { get; set; } // 保留地图布置接口，材质色彩现由统一美术图集决定。
    public int Kind { get; set; }
    public override void _Ready()
    {
        _font=new SystemFont { FontNames=GameFontNames.CreateChineseFallbacks() };
        _region=CityArtAtlas.Region(Kind);
        TextureFilter=TextureFilterEnum.Linear;
    }
    public override void _Draw()
    {
        // 屋檐可以略伸出物理墙基；固定正面入口位置，不因美术留白而移动交互点。
        float width=BuildingSize.X+18;
        float height=Kind==4 ? BuildingSize.Y+4 : width*_region.Size.Y/_region.Size.X;
        DrawTextureRectRegion(CityArtAtlas.Texture,new Rect2(-width/2,-height,width,height),_region);
        if(Kind==0) DrawWineBanner(width,height);
        if(Kind==1) DrawMilitaryGate(height);
        if(Kind==2) DrawSmithSign(width,height);
        // 字体交给引擎排版，生成图仅保留空匾；设施名称在缩放后依然清晰。
        string caption=Kind==1 ? "军营" : Title;
        var textSize=_font.GetStringSize(caption,HorizontalAlignment.Left,-1,10);
        float plaqueY=Kind==4 ? -height*.56f : -height*.39f;
        DrawRect(new Rect2(-textSize.X/2-4,plaqueY-9,textSize.X+8,14),new Color("483c2ce8"));
        DrawLine(new(-textSize.X/2-4,plaqueY-9),new(textSize.X/2+4,plaqueY-9),new Color("b59c6c"),1);
        DrawString(_font,new(-textSize.X/2,plaqueY+2),caption,HorizontalAlignment.Left,-1,10,new Color("edddb3"));
    }

    /// <summary>驿站兼具沿途歇脚酒肆的外观；幌字由引擎绘制，不使用生成字形。</summary>
    private void DrawWineBanner(float width,float height)
    {
        Vector2 p=new(width*.42f,-height*.49f);
        DrawLine(p+new Vector2(-5,-8),p+new Vector2(20,-8),new Color("705337"),3);
        DrawLine(p+new Vector2(7,-8),p+new Vector2(7,-2),new Color("937344"),1);
        DrawLine(p+new Vector2(17,-8),p+new Vector2(17,-2),new Color("937344"),1);
        DrawColoredPolygon(new Vector2[]{p+new Vector2(3,-2),p+new Vector2(22,-2),p+new Vector2(21,26),p+new Vector2(13,23),p+new Vector2(3,26)},new Color("dcc59a"));
        DrawLine(p+new Vector2(3,0),p+new Vector2(22,0),new Color("8d4b38"),2);
        DrawString(_font,p+new Vector2(5,17),"酒",HorizontalAlignment.Left,-1,15,new Color("80432f"));
    }

    /// <summary>门框与交叉刀剑强化军营用途，所有构件停留在原建筑轮廓内。</summary>
    private void DrawMilitaryGate(float height)
    {
        float lintel=-height*.40f;
        foreach(int side in new[]{-1,1})
        {
            DrawRect(new Rect2(side*24-2,lintel,4,-lintel-6),new Color("634a32"));
            DrawRect(new Rect2(side*24-4,-8,8,5),new Color("9c937a"));
        }
        DrawRect(new Rect2(-29,lintel-4,58,5),new Color("795a39"));
        Vector2 crest=new(0,lintel-14);
        foreach(int side in new[]{-1,1})
        {
            // 左刀右剑使用不同刃形，低饱和金属色在灰瓦前保持识别。
            Vector2 grip=crest+new Vector2(-side*9,9),tip=crest+new Vector2(side*13,-13);
            DrawLine(grip,crest+new Vector2(-side*3,3),new Color("483b2f"),3,true);
            DrawLine(crest+new Vector2(-side*5,0),crest+new Vector2(0,side==1?5:4),new Color("b5975f"),2,true);
            DrawColoredPolygon(new Vector2[]{crest+new Vector2(-side*3,1),tip,
                crest+new Vector2(side*(side==1?8:13),-5),crest+new Vector2(side,4)},new Color(side==1 ? "b9bfaf" : "8f9f99"));
        }
    }

    /// <summary>锤形吊牌挂在工作棚左侧，与炉火及已有铁框灯错开。</summary>
    private void DrawSmithSign(float width,float height)
    {
        Vector2 p=new(-width*.41f,-height*.48f);
        DrawLine(p+new Vector2(4,-7),p+new Vector2(-16,-7),new Color("705337"),3);
        DrawLine(p+new Vector2(-9,-7),p+new Vector2(-9,1),new Color("716c55"),1);
        DrawColoredPolygon(new Vector2[]{p+new Vector2(-19,1),p+new Vector2(1,1),p+new Vector2(2,17),p+new Vector2(-9,22),p+new Vector2(-20,17)},new Color("514331"));
        DrawLine(p+new Vector2(-8,7),p+new Vector2(-11,18),new Color("bd9c67"),3,true);
        DrawColoredPolygon(new Vector2[]{p+new Vector2(-16,3),p+new Vector2(-3,5),p+new Vector2(-4,11),p+new Vector2(-17,9)},new Color("acb0a0"));
    }
}
