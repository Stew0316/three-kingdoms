using Godot;
using System.Linq;

/// <summary>固定朝北、玩家居中的局部地图；骑乘时扩大观察范围，显示真实地面与碰撞轮廓。</summary>
public partial class LocalMinimap : Control
{
    public Combatant Player { get; set; }
    public Node2D World { get; set; }
    public string Region { get; set; }
    public bool Overview { get; set; }
    public float WorldUnitsPerPixel { get; private set; } = 3.2f;
    public Vector2 PlayerScreenPosition => Overview ? ToMap(Player.GlobalPosition) : Size / 2;
    private Font _font;
    private Vector2 Center => Overview ? Region == "暗窟" ? new(576,352) : Region == "演武场" ? new(320,208) : new(480,320) : Player.GlobalPosition;
    private Vector2 ToMap(Vector2 point) => Size / 2 + (point - Center) / WorldUnitsPerPixel;
    public override void _Ready()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Ignore;
        _font = new SystemFont { FontNames = GameFontNames.CreateChineseFallbacks() };
    }
    public override void _Process(double delta)
    {
        if (!IsInstanceValid(Player)) return;
        var settings = AdventureSession.Instance.Settings;
        Vector2 worldSize=Region=="暗窟" ? new(1152,704) : Region=="演武场" ? new(660,300) : new(960,640);
        float target = Overview ? Mathf.Max(worldSize.X/(Size.X-24),worldSize.Y/(Size.Y-40))
            : Player.IsMounted ? settings.MinimapMountedWorldUnitsPerPixel : settings.MinimapWorldUnitsPerPixel;
        WorldUnitsPerPixel = Overview ? target : Mathf.Lerp(WorldUnitsPerPixel, target, 1 - Mathf.Exp(-5 * (float)delta));
        QueueRedraw();
    }
    private void MapRect(Rect2 rect, Color color) => DrawRect(new Rect2(ToMap(rect.Position), rect.Size / WorldUnitsPerPixel), color);
    public override void _Draw()
    {
        if (!IsInstanceValid(Player) || _font == null) return;
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("142925ef"));
        if (Region == "暗窟")
        {
            for (int y = 0; y < CaveLayout.Rows; y++) for (int x = 0; x < CaveLayout.Columns; x++)
                if (CaveLayout.IsFloor(x,y)) MapRect(new Rect2(x*32,y*32,32,32), new Color("52605a"));
            foreach (var candle in CaveLayout.Candles)
                DrawCircle(ToMap(candle.Position), 2.5f, new Color(AdventureSession.Instance.Progress.LitCaveCandles.Contains(candle.Id) ? "f9d67e" : "788c87"));
            Marker(CaveLayout.Entrance, "出口", new Color("e9d0a0"));
        }
        else if (Region == "演武场")
        {
            MapRect(new Rect2(40,96,560,224), new Color("877a5a"));
            if (World is Arena arena)
                foreach (var enemy in arena.Enemies.Where(e => !e.IsDead))
                    DrawCircle(ToMap(enemy.GlobalPosition), 2.7f, new Color("e98b74"));
        }
        else
        {
            MapRect(new Rect2(28,32,904,576), new Color("3a5147"));
            MapRect(new Rect2(28,240,904,80), new Color("baa57f"));
            foreach (int x in new[] {288,576}) MapRect(new Rect2(x,80,64,512), new Color("baa57f"));
            var map = World as CityMap;
            if (map?.Broken == true) MapRect(new Rect2(838,320,52,260), new Color("998e6c"));
            // 建筑与城墙直接读取真实碰撞体，避免另画一套不对应的通路。
            if (map != null)
                foreach (var body in map.GetChildren().OfType<StaticBody2D>())
                    foreach (var shape in body.GetChildren().OfType<CollisionShape2D>())
                        if (shape.Shape is RectangleShape2D rectangle && body.CollisionLayer != 0)
                            MapRect(new Rect2(shape.GlobalPosition - rectangle.Size / 2, rectangle.Size), new Color("203a34"));
            Marker(new(169,234), "驿", new Color("e9d0a0"));
            Marker(new(467,266), "军", new Color("e9d0a0"));
            Marker(new(748,242), "铁", new Color("e9d0a0"));
            Marker(new(710,542), "马", new Color("e9d0a0"));
            Marker(new(903,294), "东门", new Color("e9d0a0"));
        }
        Vector2 p = PlayerScreenPosition, forward = Player.FacingDirection.Normalized(), side = forward.Orthogonal();
        DrawCircle(p, 6, new Color("142925"));
        DrawColoredPolygon(new[] { p + forward*6, p - forward*4 + side*3.5f, p - forward*2, p - forward*4 - side*3.5f }, new Color("ffdf89"));
        DrawRect(new Rect2(1,1,Size.X-2,Size.Y-2), new Color("c3ac77"), false, 1);
        DrawRect(new Rect2(2,2,Size.X-4,17), new Color("142925ed"));
        DrawString(_font, new(7,14), Region, fontSize:10, modulate:new Color("ead9b2"));
        DrawString(_font, new(Size.X-17,14), "北", fontSize:10, modulate:new Color("ead9b2"));
        DrawRect(new Rect2(2,Size.Y-16,Size.X-4,14), new Color("142925ed"));
        DrawString(_font, new(7,Size.Y-5), Overview ? "▲ 当前位置" : Player.IsMounted ? "附近  ·  骑乘视野" : "附近  ·  徒步视野", fontSize:9, modulate:new Color("c8d3ba"));
    }
    private void Marker(Vector2 position, string text, Color color)
    {
        Vector2 p = ToMap(position);
        if (p.X<8 || p.Y<28 || p.X>Size.X-12 || p.Y>Size.Y-24) return;
        DrawCircle(p, 2, color);
        DrawString(_font,p+new Vector2(4,3),text,fontSize:10,modulate:color);
    }
}
