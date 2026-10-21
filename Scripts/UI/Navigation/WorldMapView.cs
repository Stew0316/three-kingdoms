using Godot;
using System;
using System.Linq;

/// <summary>可缩放、拖动、选城的天下图；参考图提供底图，选中路线来自独立内容数据。</summary>
public partial class WorldMapView : Control
{
    public WorldAtlas Atlas { get; set; }
    public string SelectedId { get; private set; } = "luoyang";
    public event Action<WorldAtlas.City> Selected;
    public float Zoom { get; private set; } = 1;
    private Texture2D _texture;
    private Font _font;
    private Vector2 _pan, _pressPosition;
    private bool _dragging, _moved;
    private float FitScale => Mathf.Min(Size.X / Atlas.Width, Size.Y / Atlas.Height);
    private float MapScale => FitScale * Zoom;
    private Vector2 Origin => (Size - new Vector2(Atlas.Width, Atlas.Height) * MapScale) / 2 + _pan;

    public override void _Ready()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Stop;
        _texture = GD.Load<Texture2D>("res://Source/Town.png");
        _font = new SystemFont { FontNames = GameFontNames.CreateChineseFallbacks() };
    }
    public void ResetView() { Zoom = 1; _pan = Vector2.Zero; QueueRedraw(); }
    public void ChangeZoom(float factor) => ZoomAt(factor, Size / 2);
    private void ZoomAt(float factor, Vector2 anchor)
    {
        Vector2 mapPoint = (anchor - Origin) / MapScale;
        Zoom = Mathf.Clamp(Zoom * factor, 1, 5);
        _pan = anchor - (Size - new Vector2(Atlas.Width, Atlas.Height) * MapScale) / 2 - mapPoint * MapScale;
        ClampPan(); QueueRedraw();
    }
    private void ClampPan()
    {
        Vector2 excess = (new Vector2(Atlas.Width, Atlas.Height) * MapScale - Size) / 2;
        _pan = new Vector2(Mathf.Clamp(_pan.X, -Mathf.Max(0, excess.X), Mathf.Max(0, excess.X)),
            Mathf.Clamp(_pan.Y, -Mathf.Max(0, excess.Y), Mathf.Max(0, excess.Y)));
    }
    public void SelectCity(string id, bool focus = false)
    {
        var city = Atlas.Find(id); SelectedId = id;
        if (focus) { Zoom = 2.2f; _pan = (new Vector2(Atlas.Width, Atlas.Height) / 2 - city.Position) * MapScale; ClampPan(); }
        Selected?.Invoke(city); QueueRedraw();
    }
    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton button)
        {
            if (button.Pressed && button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
                ZoomAt(button.ButtonIndex == MouseButton.WheelUp ? 1.2f : 1 / 1.2f, button.Position);
            if (button.ButtonIndex == MouseButton.Left)
            {
                if (button.Pressed) { _dragging = true; _moved = false; _pressPosition = button.Position; }
                else
                {
                    if (_dragging && !_moved)
                    {
                        var city = Atlas.Cities.OrderBy(c => (Origin + c.Position * MapScale).DistanceTo(button.Position)).First();
                        if ((Origin + city.Position * MapScale).DistanceTo(button.Position) < 14) SelectCity(city.Id);
                    }
                    _dragging = false;
                }
            }
            AcceptEvent();
        }
        if (input is InputEventMouseMotion motion && _dragging)
        {
            _moved |= motion.Position.DistanceTo(_pressPosition) > 3;
            if (_moved) { _pan += motion.Relative; ClampPan(); QueueRedraw(); }
            AcceptEvent();
        }
    }
    public override void _Draw()
    {
        if (_texture == null) return;
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("182724"));
        DrawTextureRect(_texture, new Rect2(Origin, new Vector2(Atlas.Width, Atlas.Height) * MapScale), false);
        foreach (var route in Atlas.Connections(SelectedId))
        {
            var points = Atlas.Polyline(route).Select(p => Origin + p * MapScale).ToArray();
            DrawPolyline(points, new Color("16352ce0"), 5, true);
            DrawPolyline(points, new Color(route.Kind == "water" ? "7ee0ee" : "ffe5a1"), 2, true);
        }
        Vector2 selected = Origin + Atlas.Find(SelectedId).Position * MapScale;
        DrawArc(selected, 8, 0, Mathf.Tau, 32, new Color("fff0b0"), 2, true);
        string name = Atlas.Find(SelectedId).Name;
        DrawStyleBox(UiStyles.Panel(new Color("152b29"), new Color("d6bb82")), new Rect2(selected + new Vector2(10, -12), new Vector2(56, 23)));
        DrawString(_font, selected + new Vector2(17, 4), name, fontSize: 12, modulate: new Color("f7e5b8"));
    }
}
