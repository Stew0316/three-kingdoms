using Godot;

/// <summary>首城 HUD 装配与刷新，与地图进度逻辑分开维护。</summary>
public partial class FirstCity
{
    private void BuildHud()
    {
        var layer=new CanvasLayer(); AddChild(layer);
        var root=new Control { MouseFilter=Control.MouseFilterEnum.Ignore }; root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); layer.AddChild(root);
        root.Theme=UiStyles.ChineseTheme(11);
        var top=new Panel { Position=new(10,48),Size=new(620,54),MouseFilter=Control.MouseFilterEnum.Ignore }; top.AddThemeStyleboxOverride("panel",CityDialogue.Box(new Color("162d30e8"),new Color("8c865d"))); root.AddChild(top);
        Label LabelAt(Vector2 pos,Vector2 size,int font=11)
        { var label=new Label { Position=pos,Size=size,MouseFilter=Control.MouseFilterEnum.Ignore }; label.AddThemeFontSizeOverride("font_size",font); root.AddChild(label); return label; }
        var title=LabelAt(new(22,53),new(132,22),18); title.Text=Config.CityName+" · 初行"; title.Modulate=new Color("f0d89f");
        _stats=LabelAt(new(162,56),new(454,20));
        _quest=LabelAt(new(23,79),new(590,18)); _quest.Modulate=new Color("c7d5b7");
        _hint=LabelAt(new(14,285),new(612,18)); _hint.HorizontalAlignment=HorizontalAlignment.Center;
        _hint.AddThemeColorOverride("font_outline_color",new Color("14201b")); _hint.AddThemeConstantOverride("outline_size",3);
        _notice=LabelAt(new(168,260),new(454,24),11); _notice.HorizontalAlignment=HorizontalAlignment.Center; _notice.AddThemeColorOverride("font_color",new Color("fff1c0")); _notice.AddThemeColorOverride("font_shadow_color",new Color("162d30")); _notice.AddThemeConstantOverride("shadow_offset_x",1); _notice.AddThemeConstantOverride("shadow_offset_y",1);
    }
    private void RefreshHud()
    {
        _stats.Text=$"生命 {Player.Health.CurrentHealth}/{Player.Health.MaxHealth}   铜钱 {Progress.Coins}   伤害 ×{Progress.PowerMultiplier(Config):0.00}   {(MountedHorse==0 ? "徒步" : MountedHorse==1 ? "枣红马" : "青骢马")}";
        _quest.Text=Progress.Trial switch
        {
            AdventureProgress.QuestStage.Available=>"初来乍到  /  与军营教头交谈，接下第一份委托",
            AdventureProgress.QuestStage.Accepted=>"演武试炼  /  出东门击败魏延  ·  地形能力：崩击已习得",
            AdventureProgress.QuestStage.Completed=>"演武试炼  /  已击败魏延，返回军营领取《行军要诀》",
            _=>"演武试炼已完成  /  《行军要诀》已研习  ·  "+(Progress.BoulderBroken ? "旧驿道已开启" : "可用崩击打开东门巨石")
        };
    }
}
