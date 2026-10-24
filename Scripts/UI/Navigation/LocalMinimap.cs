using Godot;
using System.Linq;

/// <summary>固定朝北、玩家居中的局部地图；骑乘时扩大观察范围，显示真实地面与碰撞轮廓。</summary>
public partial class LocalMinimap : Control
{
    public Combatant Player { get; set; }
    public Node2D World { get; set; }
    public string Region { get; set; }
    public bool Overview { get; set; }
    // 底栏小地图使用圆形遮罩；菜单中的本地导览继续使用完整矩形地图。
    public bool Circular { get; set; }
    private Control _surface;
    public float WorldUnitsPerPixel { get; private set; } = 3.2f;
    public Vector2 PlayerScreenPosition => Overview ? ToMap(Player.GlobalPosition) : Size / 2;
    private Font _font;
    private Texture2D _mountainArtwork;
    private float MapRadius => Mathf.Min(Size.X,Size.Y)/2;
    private Vector2 Center => Overview ? Region == "暗窟" ? new(576,352) : Region == "演武场" ? new(320,208) : SnowMountainLayout.WorldBounds.GetCenter() : Player.GlobalPosition;
    private Vector2 ToMap(Vector2 point) => Size / 2 + (point - Center) / WorldUnitsPerPixel;
    public override void _Ready()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Ignore;
        _font = new SystemFont { FontNames = GameFontNames.CreateChineseFallbacks() };
        if(World is CityMap) _mountainArtwork=GD.Load<Texture2D>(SnowMountainLayout.MountainTexture);
        if(Circular)
        {
            // 父节点只提供圆形 Alpha 遮罩，地图内容由子节点绘制，不显示白色遮罩本身。
            ClipChildren=ClipChildrenMode.Only;
            _surface=new Control { Name="CircularMapSurface",Size=Size,MouseFilter=MouseFilterEnum.Ignore };
            AddChild(_surface); _surface.Draw+=()=>DrawMap(_surface);
        }
    }
    public override void _Process(double delta)
    {
        if (!IsInstanceValid(Player)) return;
        var settings = AdventureSession.Instance.Settings;
        float range=float.IsFinite(settings.MinimapRangeMultiplier) && settings.MinimapRangeMultiplier>0 ? settings.MinimapRangeMultiplier : 2;
        Vector2 worldSize=Region=="暗窟" ? new(1152,704) : Region=="演武场" ? new(660,300) : SnowMountainLayout.WorldBounds.Size;
        float target = Overview ? Mathf.Max(worldSize.X/(Size.X-24),worldSize.Y/(Size.Y-40))
            : (Player.IsMounted ? settings.MinimapMountedWorldUnitsPerPixel : settings.MinimapWorldUnitsPerPixel)*range;
        if(!float.IsFinite(target) || target<=0) target=3.2f*range;
        WorldUnitsPerPixel = Overview ? target : Mathf.Lerp(WorldUnitsPerPixel, target, 1 - Mathf.Exp(-5 * (float)delta));
        QueueRedraw();
        if(_surface!=null) { _surface.Size=Size; _surface.QueueRedraw(); }
    }
    private void MapRect(Control canvas,Rect2 rect, Color color) => canvas.DrawRect(new Rect2(ToMap(rect.Position), rect.Size / WorldUnitsPerPixel), color);
    public override void _Draw()
    {
        if(Circular) DrawCircle(Size/2,MapRadius,Colors.White,true,-1,true);
        else DrawMap(this);
    }
    private void DrawMap(Control canvas)
    {
        if (!IsInstanceValid(Player) || _font == null) return;
        canvas.DrawRect(new Rect2(Vector2.Zero, Size), new Color("142925ef"));
        if (Region == "暗窟")
        {
            for (int y = 0; y < CaveLayout.Rows; y++) for (int x = 0; x < CaveLayout.Columns; x++)
                if (CaveLayout.IsFloor(x,y)) MapRect(canvas,new Rect2(x*32,y*32,32,32), new Color("52605a"));
            foreach (var candle in CaveLayout.Candles)
                canvas.DrawCircle(ToMap(candle.Position), 2.5f, new Color(AdventureSession.Instance.Progress.LitCaveCandles.Contains(candle.Id) ? "f9d67e" : "788c87"));
            Marker(canvas,CaveLayout.Entrance, "出口", new Color("e9d0a0"));
        }
        else if (Region == "演武场")
        {
            MapRect(canvas,new Rect2(40,96,560,224), new Color("877a5a"));
            if (World is Arena arena)
                foreach (var enemy in arena.Enemies.Where(e => !e.IsDead))
                    canvas.DrawCircle(ToMap(enemy.GlobalPosition), 2.7f, new Color("e98b74"));
        }
        else
        {
            MapRect(canvas,new Rect2(28,32,904,576), new Color("3a5147"));
            if(_mountainArtwork!=null)
                canvas.DrawTextureRect(_mountainArtwork,new Rect2(ToMap(SnowMountainLayout.MountainBounds.Position),SnowMountainLayout.MountainBounds.Size/WorldUnitsPerPixel),false,new Color(.72f,.80f,.80f));
            canvas.DrawColoredPolygon(SnowMountainLayout.PathPolygon().Select(ToMap).ToArray(),new Color("7e8d70"));
            MapRect(canvas,new Rect2(28,240,904,80), new Color("baa57f"));
            foreach (int x in new[] {288,576}) MapRect(canvas,new Rect2(x,80,64,512), new Color("baa57f"));
            var map = World as CityMap;
            if (map?.Broken == true) MapRect(canvas,new Rect2(838,320,52,260), new Color("998e6c"));
            // 建筑与城墙直接读取真实碰撞体，避免另画一套不对应的通路。
            if (map != null)
                foreach (var body in map.GetChildren().OfType<StaticBody2D>())
                    foreach (var shape in body.GetChildren().OfType<CollisionShape2D>())
                        if (shape.Shape is RectangleShape2D rectangle && body.CollisionLayer != 0 && shape.GlobalPosition.X<960)
                            MapRect(canvas,new Rect2(shape.GlobalPosition - rectangle.Size / 2, rectangle.Size), new Color("203a34"));
            Marker(canvas,new(169,234), "驿", new Color("e9d0a0"));
            Marker(canvas,new(467,266), "军", new Color("e9d0a0"));
            Marker(canvas,new(748,242), "铁", new Color("e9d0a0"));
            Marker(canvas,new(710,542), "马", new Color("e9d0a0"));
            Marker(canvas,new(903,294), "东门", new Color("e9d0a0"));
            Marker(canvas,SnowMountainLayout.Entrance,"雪山麓",new Color("e0eeee"));
        }
        Vector2 p = PlayerScreenPosition, forward = Player.FacingDirection.Normalized(), side = forward.Orthogonal();
        canvas.DrawCircle(p, 6, new Color("142925"));
        canvas.DrawColoredPolygon(new[] { p + forward*6, p - forward*4 + side*3.5f, p - forward*2, p - forward*4 - side*3.5f }, new Color("ffdf89"));
        if(Circular)
        {
            canvas.DrawArc(Size/2,MapRadius-1,0,Mathf.Tau,64,new Color("9b895f"),1,true);
            canvas.DrawString(_font,new(Size.X/2-3,9),"北",fontSize:7,modulate:new Color("ead9b2"));
            return;
        }
        canvas.DrawRect(new Rect2(1,1,Size.X-2,Size.Y-2), new Color("c3ac77"), false, 1);
        canvas.DrawRect(new Rect2(2,2,Size.X-4,17), new Color("142925ed"));
        canvas.DrawString(_font, new(7,14), Region, fontSize:10, modulate:new Color("ead9b2"));
        canvas.DrawString(_font, new(Size.X-17,14), "北", fontSize:10, modulate:new Color("ead9b2"));
        canvas.DrawRect(new Rect2(2,Size.Y-16,Size.X-4,14), new Color("142925ed"));
        canvas.DrawString(_font, new(7,Size.Y-5), Overview ? "▲ 当前位置" : Player.IsMounted ? "附近  ·  骑乘视野" : "附近  ·  徒步视野", fontSize:9, modulate:new Color("c8d3ba"));
    }
    private void Marker(Control canvas,Vector2 position, string text, Color color)
    {
        Vector2 p = ToMap(position);
        if(Circular)
        {
            if(p.DistanceTo(Size/2)<MapRadius-5) canvas.DrawCircle(p,1.6f,color);
            return;
        }
        if (p.X<8 || p.Y<28 || p.X>Size.X-12 || p.Y>Size.Y-24) return;
        canvas.DrawCircle(p, 2, color);
        canvas.DrawString(_font,p+new Vector2(4,3),text,fontSize:10,modulate:color);
    }
}
