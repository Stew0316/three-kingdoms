using Godot;
using System.Collections.Generic;

/// <summary>时钟日期下方的轻量消息；最多三条，自动淡入淡出，不拦截操作，随游戏暂停。</summary>
public partial class MessageToast : Control
{
    private sealed class Entry { public Panel Panel; public string Text; public float Age,Duration; }
    private readonly List<Entry> _entries=new();
    public int Count => _entries.Count;
    public override void _Ready() { MouseFilter=MouseFilterEnum.Ignore; Position=new(0,50); Size=new(640,0); }
    public void ShowMessage(string text,float seconds=3)
    {
        if(string.IsNullOrWhiteSpace(text)) return;
        foreach(var item in _entries)
            if(item.Text==text) { item.Age=0; item.Duration=Mathf.Max(1,seconds); return; }
        if(_entries.Count==3) { _entries[0].Panel.QueueFree(); _entries.RemoveAt(0); }
        var theme=UiStyles.ChineseTheme(11);
        var font=theme.DefaultFont;
        float width=Mathf.Clamp(font.GetStringSize(text,fontSize:11).X+28,140,540);
        var panel=new Panel { Size=new(width,30),Theme=theme,MouseFilter=MouseFilterEnum.Ignore,Modulate=new Color(1,1,1,0) };
        panel.AddThemeStyleboxOverride("panel",new StyleBoxFlat {
            BgColor=new Color("24251eef"),BorderColor=new Color("f3ce64"),
            BorderWidthLeft=1,BorderWidthRight=1,BorderWidthTop=1,BorderWidthBottom=1,
            CornerRadiusTopLeft=4,CornerRadiusTopRight=4,CornerRadiusBottomLeft=4,CornerRadiusBottomRight=4,
            ShadowColor=new Color(1,1,.95f,.36f),ShadowSize=7 });
        var label=new Label { Text=text,Position=new(12,6),Size=new(width-24,18),
            HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,
            AutowrapMode=TextServer.AutowrapMode.WordSmart,MouseFilter=MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size",11);
        label.AddThemeColorOverride("font_color",new Color("ffe07a"));
        panel.AddChild(label); AddChild(panel);
        // 入树后中文字体与字号才完整继承，重新设尺寸，避免创建时默认字体的最小宽度撑出消息框。
        label.ResetSize(); label.Size=new(width-24,18);
        // 长消息可换行，增加高度后后续消息仍按实际高度排列。
        float height=Mathf.Max(30,label.GetMinimumSize().Y+12); panel.Size=new(width,height);
        _entries.Add(new Entry { Panel=panel,Text=text,Duration=Mathf.Max(1,seconds) });
        Layout();
    }
    public override void _Process(double delta)
    {
        for(int i=_entries.Count-1;i>=0;i--)
        {
            var item=_entries[i]; item.Age+=(float)delta;
            if(item.Age>=item.Duration) { item.Panel.QueueFree(); _entries.RemoveAt(i); continue; }
            float alpha=Mathf.Min(Mathf.Clamp(item.Age/.16f,0,1),Mathf.Clamp((item.Duration-item.Age)/.25f,0,1));
            item.Panel.Modulate=new Color(1,1,1,alpha);
        }
        Layout();
    }
    private void Layout()
    {
        float y=0;
        foreach(var item in _entries)
        {
            item.Panel.Position=new((640-item.Panel.Size.X)/2,y-4*(1-Mathf.Clamp(item.Age/.16f,0,1)));
            y+=item.Panel.Size.Y+8;
        }
    }
}
