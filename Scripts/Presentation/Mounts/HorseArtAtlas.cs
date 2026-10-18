using Godot;

/// <summary>V2四件原图的采样与标定。整身和整腿保留连续毛皮，不再拼接独立肌肉圆片。</summary>
public static class HorseArtAtlas
{
    public static readonly Rect2 Body = new(225,0,1150,491);
    public static readonly Rect2 Foreleg = new(305,495,190,510);
    public static readonly Rect2 Hindleg = new(675,493,215,512);
    public static readonly Rect2 Tail = new(1045,492,370,532);
    // 源锚点依次对应肩/髋、肘/膝、腕/飞节、球节、蹄底，沿同一整腿图取样。
    public static Vector2[] LegAnchors(bool front) => front
        ? new[]{new Vector2(424,528),new Vector2(374,608),new Vector2(363,759),new Vector2(349,903),new Vector2(396,995)}
        : new[]{new Vector2(749,524),new Vector2(802,620),new Vector2(731,753),new Vector2(780,903),new Vector2(834,995)};
    public static Vector2 BodyPoint(Vector2 pixel,float width) =>
        new((pixel.X-650)*.073f*width,-43+(pixel.Y-230)*.078f);
    public static readonly Vector2 BridlePixel = new(1265,267);
}
