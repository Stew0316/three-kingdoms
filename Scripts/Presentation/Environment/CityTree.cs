using Godot;

/// <summary>首城国槐表现；通行与树干碰撞由 CityMap 创建。</summary>
public partial class CityTree : Node2D
{
    public override void _Draw()
    {
        // 图集槐树与屋舍采用同一绘制风格，树干脚点及物理碰撞保持不变。
        if(CityArtAtlas.Texture!=null)
        {
            var region=CityArtAtlas.Region(5);
            float width=84,height=width*region.Size.Y/region.Size.X;
            TextureFilter=TextureFilterEnum.Linear;
            DrawTextureRectRegion(CityArtAtlas.Texture,new Rect2(-width*.5f,-height+5,width,height),region);
            return;
        }
        DrawRect(new Rect2(-5,-34,10,34),new Color("756b4c"));
        DrawCircle(new(2,-35),30,new Color("596f54")); DrawCircle(new(-12,-48),23,new Color("6d7f5a"));
        DrawCircle(new(10,-53),25,new Color("7b8960")); DrawCircle(new(0,-66),17,new Color("90996a"));
        DrawCircle(new(14,-64),5,new Color("a3a775"));
    }
}
