using Godot;

/// <summary>槐安古建筑图集的只读采样。保留原图透明度，按各格可见内容自动收紧包围盒。</summary>
public static class CityArtAtlas
{
    public const string AssetPath="res://Source/City/HuaiAn/huai-an-buildings-qingming-v2.png";
    private static Texture2D _texture;
    private static Rect2[] _regions;
    public static Texture2D Texture => _texture ??= GD.Load<Texture2D>(AssetPath);

    /// <summary>三列两行依次为驿站、军营、铁匠、民居、马厩、国槐；只裁取矩形，不改写PNG。</summary>
    public static Rect2 Region(int index)
    {
        if(_regions==null)
        {
            _regions=new Rect2[6];
            using var image=Texture.GetImage();
            if(image.IsCompressed()) image.Decompress();
            // v2驿站右檐超过名义格线，国槐顶接近中线；用各资产间的透明间隙分区，避免切檐或串入邻屋。
            var windows=new Rect2I[] {
                new(0,0,536,512),new(536,0,488,512),new(1024,0,512,488),
                new(0,512,512,512),new(512,512,512,512),new(1024,488,512,536)
            };
            for(int tile=0;tile<6;tile++)
            {
                int left=windows[tile].Position.X,top=windows[tile].Position.Y;
                int width=windows[tile].Size.X,height=windows[tile].Size.Y;
                int minX=left+width,minY=top+height,maxX=left,maxY=top;
                // 很淡的接地阴影不参与尺寸估算，绘制仍保留包围盒内的原始透明像素。
                for(int y=top;y<top+height;y++) for(int x=left;x<left+width;x++)
                    if(image.GetPixel(x,y).A>.16f)
                    { minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y); }
                minX=Mathf.Max(left,minX-2);minY=Mathf.Max(top,minY-2);
                maxX=Mathf.Min(left+width-1,maxX+2);maxY=Mathf.Min(top+height-1,maxY+2);
                _regions[tile]=new Rect2(minX,minY,Mathf.Max(1,maxX-minX+1),Mathf.Max(1,maxY-minY+1));
            }
        }
        return _regions[Mathf.Clamp(index,0,5)];
    }
}
