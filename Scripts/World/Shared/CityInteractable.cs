using Godot;

/// <summary>拥有稳定内容 ID 的交互锚点；实际交互由城市控制器依据范围与遮挡统一派发。</summary>
public partial class CityInteractable : Node2D
{
    public string ContentId { get; set; } = "";
    public string Title { get; set; } = "";
    public bool IsNpc { get; set; }
    public Color Robe { get; set; } = new("647b73");
    public bool Highlighted { get; set; }
    public bool QuestMarker { get; set; }
    public bool ShowLabel { get; set; } = true; // 洞窟用HUD显示目标名，避免墙边文字盖住人物与火焰。
    private Font _font;
    public override void _Ready() { _font = new SystemFont { FontNames = GameFontNames.CreateChineseFallbacks() }; }
    public override void _Draw()
    {
        if (IsNpc)
        {
            DrawEllipse(new(0, 1), new(10, 4), new Color(0, 0, 0, .22f));
            DrawRect(new Rect2(-6, -8, 5, 9), new Color("283536")); DrawRect(new Rect2(2, -8, 5, 9), new Color("283536"));
            DrawColoredPolygon(new Vector2[] { new(-7,-27),new(6,-27),new(10,-7),new(-10,-7) }, Robe);
            DrawLine(new(-7,-15),new(7,-15),new Color("d1b074"),2);
            DrawLine(new(-7,-24),new(-11,-12),Robe.Darkened(.2f),5); DrawLine(new(7,-24),new(10,-12),Robe.Darkened(.2f),5);
            DrawCircle(new(0,-31),6,new Color("d9ac7b")); DrawRect(new Rect2(-6,-38,12,6),new Color("283536"));
            DrawRect(new Rect2(-3,-41,6,4),new Color("283536"));
            if (QuestMarker) DrawString(_font,new(-3,-48),"!",HorizontalAlignment.Left,-1,19,new Color("ffda80"));
        }
        if (Highlighted) DrawArc(new(0,2),14,0,Mathf.Tau,32,new Color("ffe0a0"),1.5f);
        if (ShowLabel && (IsNpc || Highlighted))
        {
            var size = _font.GetStringSize(Title, HorizontalAlignment.Left, -1, 10);
            DrawRect(new Rect2(-size.X/2-4,8,size.X+8,16),new Color(.06f,.13f,.13f,.88f));
            DrawString(_font,new(-size.X/2,20),Title,HorizontalAlignment.Left,-1,10,new Color("f5e2b4"));
        }
    }
    private void DrawEllipse(Vector2 center, Vector2 radius, Color color)
    {
        DrawSetTransform(center, 0, radius); DrawCircle(Vector2.Zero, 1, color); DrawSetTransform(Vector2.Zero);
    }
}
