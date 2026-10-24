using Godot;
using System;

/// <summary>拥有稳定内容 ID 的交互锚点；实际交互由城市控制器依据范围与遮挡统一派发。</summary>
public partial class CityInteractable : Node2D
{
    public string ContentId { get; set; } = "";
    public string Title { get; set; } = "";
    public bool IsNpc { get; set; }
    public Color Robe { get; set; } = new("647b73");
    public bool Highlighted { get; set; }
    public bool QuestMarker { get; set; }
    public bool ShowLabel { get; set; } = true; // 入树前决定是否创建头顶提示；不再绘制脚底名字。
    /// <summary>由地图控制器提供当前实际按键与动词，不在交互点里写死默认按键。</summary>
    public string PromptText { get; set; } = "";
    /// <summary>提示文字顶部相对交互脚点的位置；不同身高的目标分别配置。</summary>
    public Vector2 PromptOffset { get; set; } = new(0,-62);
    /// <summary>模态界面及角色动作限制；暂停时仍检查，以免旧提示冻结在场景里。</summary>
    public Func<bool> CanShowPrompt { get; set; }
    public bool PromptVisible => _prompt?.IsVisibleInTree()==true;
    private Font _font;
    private Label _prompt;
    private bool _highlightVisible;
    public override void _Ready()
    {
        _font = new SystemFont { FontNames = GameFontNames.CreateChineseFallbacks() };
        if(!ShowLabel) { SetProcess(false); return; }
        ProcessMode=ProcessModeEnum.Always;
        _prompt=new Label { Name="InteractionPrompt",Position=PromptOffset-new Vector2(80,0),Size=new(160,18),
            Visible=false,HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore,
            ZIndex=100,Material=new CanvasItemMaterial { LightMode=CanvasItemMaterial.LightModeEnum.Unshaded } };
        _prompt.AddThemeFontOverride("font",_font); _prompt.AddThemeFontSizeOverride("font_size",11);
        _prompt.AddThemeColorOverride("font_color",new Color("ffe6b3"));
        _prompt.AddThemeColorOverride("font_outline_color",new Color("17231f"));
        _prompt.AddThemeConstantOverride("outline_size",3);
        AddChild(_prompt);
    }
    public override void _Process(double delta)
    {
        if(_prompt==null) return;
        bool highlighted=Highlighted && (CanShowPrompt?.Invoke() ?? true);
        bool visible=ShowLabel && highlighted && PromptText.Length>0;
        if(_highlightVisible!=highlighted || _prompt.Visible!=visible)
        { _highlightVisible=highlighted; _prompt.Visible=visible; QueueRedraw(); }
        _prompt.Text=PromptText;
        _prompt.Position=PromptOffset-new Vector2(80,0);
    }
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
            // 远处只保留任务感叹号；近处以交谈提示替代，避免两种文字重叠。
            if (QuestMarker && !PromptVisible) DrawString(_font,new(-3,-48),"!",HorizontalAlignment.Left,-1,19,new Color("ffda80"));
        }
        // NPC 靠近时仅显示头顶交谈提示；地面圆圈只用于非 NPC 交互点。
        if (!IsNpc && Highlighted && (CanShowPrompt?.Invoke() ?? true)) DrawArc(new(0,2),14,0,Mathf.Tau,32,new Color("ffe0a0"),1.5f);
    }
    private void DrawEllipse(Vector2 center, Vector2 radius, Color color)
    {
        DrawSetTransform(center, 0, radius); DrawCircle(Vector2.Zero, 1, color); DrawSetTransform(Vector2.Zero);
    }
}
