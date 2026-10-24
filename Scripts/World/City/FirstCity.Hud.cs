using Godot;

/// <summary>首城只保留临时操作反馈；任务与人物信息由独立入口承接。</summary>
public partial class FirstCity
{
    private void BuildHud()
    {
        var layer=new CanvasLayer { Name="CityFeedback" }; AddChild(layer);
        var root=new Control { MouseFilter=Control.MouseFilterEnum.Ignore }; layer.AddChild(root); root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.Theme=UiStyles.ChineseTheme(11);
        Label Feedback(string name,Vector2 pos,Vector2 size)
        {
            var label=new Label { Name=name,Position=pos,Size=size,Visible=false,
                HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore };
            label.AddThemeFontSizeOverride("font_size",11);
            label.AddThemeColorOverride("font_color",new Color("fff1c0"));
            label.AddThemeColorOverride("font_outline_color",new Color("14201b"));
            label.AddThemeConstantOverride("outline_size",3);
            root.AddChild(label); return label;
        }
        _activityHint=Feedback("ActivityHint",new(140,248),new(360,18));
        _notice=new MessageToast { Name="Notice" }; root.AddChild(_notice);
    }
    private void RefreshHud()
    {
        _activityHint.Text=_training ? $"军营锻炼中 · {TrainingRemaining:0.0} 秒" : _smashRemaining>0 ? "蓄力 · 崩击" : "";
        _activityHint.Visible=_activityHint.Text.Length>0;
    }
}
