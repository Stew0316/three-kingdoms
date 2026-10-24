using Godot;

/// <summary>雪山外景共享布局：道路轮廓同时供显示、碰撞与小地图使用，入口为观景终点。</summary>
public static class SnowMountainLayout
{
    public const string MountainTexture="res://Source/Terrain/SnowMountain/SnowMountainV1.png";
    public const string MeadowTexture="res://Source/Terrain/SnowMountain/ColdMeadowV2.png";
    public const string BankTexture="res://Source/Terrain/SnowMountain/RockyBankV1.png";
    public static readonly Rect2 WorldBounds=new(0,-704,2304,1344);
    public static readonly Rect2 ExteriorBounds=new(932,-704,1372,1344);
    public static readonly Rect2 MountainBounds=new(1010,-650,1280,853.3333f);
    public static readonly Vector2 Entrance=new(1650,0);
    public static readonly Vector2[] UpperBank={new(932,242),new(1080,230),new(1260,210),new(1430,190),new(1500,120),new(1580,100),new(1608,-12)};
    public static readonly Vector2[] LowerBank={new(932,320),new(1100,346),new(1280,370),new(1470,360),new(1640,320),new(1740,220),new(1730,90),new(1692,-12)};
    public static Vector2[] PathPolygon()
    {
        var points=new System.Collections.Generic.List<Vector2>(UpperBank);
        for(int i=LowerBank.Length-1;i>=0;i--) points.Add(LowerBank[i]);
        return points.ToArray();
    }
}
