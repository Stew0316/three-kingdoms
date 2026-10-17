using Godot;

/// <summary>首城导览绘制，只读取地图与玩家位置。</summary>
public partial class CityOverview : Control
{
    private Font _font;
    public override void _Ready() => _font=new SystemFont { FontNames=GameFontNames.CreateChineseFallbacks() };
    public FirstCity City { get; set; }
    public override void _Process(double delta) { if(Visible) QueueRedraw(); }
    public override void _Draw()
    {
        DrawRect(new Rect2(0,0,640,360),new Color("122b2bf5"));
        var font=_font;
        DrawString(font,new(47,35),"槐安城导览    ·    M / Esc 返回",HorizontalAlignment.Left,-1,18,new Color("edd7a3"));
        Vector2 Map(Vector2 p)=>new Vector2(54,56)+p*.43f;
        DrawRect(new Rect2(Map(new(28,32)),new Vector2(904,576)*.43f),new Color("577052"));
        DrawRect(new Rect2(Map(new(28,240)),new Vector2(904,80)*.43f),new Color("b7a27c"));
        foreach(int x in new[]{288,576}) DrawRect(new Rect2(Map(new(x,80)),new Vector2(64,512)*.43f),new Color("b7a27c"));
        var points=new (Vector2,string)[]{(new(100,155),"驿站"),(new(406,148),"军营"),(new(701,155),"铁匠铺"),(new(100,440),"民居"),(new(650,465),"马厩")};
        foreach(var (p,name) in points) { DrawRect(new Rect2(Map(p-new Vector2(10,30)),new(56,34)),new Color("354e50")); DrawString(font,Map(p),name,HorizontalAlignment.Left,-1,12,new Color("efdcb0")); }
        DrawCircle(Map(new(865,365)),8,City.Map.Broken?new Color("cab889"):new Color("9b9f8c"));
        DrawString(font,new(467,165),"东门 → 演武场",HorizontalAlignment.Left,-1,11,new Color("f2dcaa"));
        DrawString(font,new(467,218),"巨石 / 旧驿道",HorizontalAlignment.Left,-1,11,new Color("f2dcaa"));
        DrawString(font,new(467,239),"旧驿道 → 暗窟",HorizontalAlignment.Left,-1,11,new Color("f2dcaa"));
        DrawCircle(Map(City.Player.Position),5,new Color("ffdb6e"));
        DrawString(font,new(47,345),"● 你的位置     接任务 → 演武 → 回城领兵书     军营锻炼 / 铁匠整备 / 马厩试骑",HorizontalAlignment.Left,-1,11,new Color("d1d9be"));
    }
}
