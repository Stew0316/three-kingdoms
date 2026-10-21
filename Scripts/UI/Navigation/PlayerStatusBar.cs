using Godot;
using System;

/// <summary>统一底栏只读取真实人物、技能冷却和库存；未实现的内力明确显示为未启用。</summary>
public partial class PlayerStatusBar : Control
{
    public Combatant Player { get; set; }
    public Func<bool> CanUse { get; set; }
    public Button[] Skills { get; } = new Button[4];
    public Button[] Items { get; } = new Button[4];
    private Label _health, _name;
    private ColorRect _red;
    private readonly Label[] _cooldowns=new Label[4];
    private readonly ColorRect[] _covers=new ColorRect[4];
    private AdventureSession Session => AdventureSession.Instance;
    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Stop;
        Theme=UiStyles.ChineseTheme(10);
        var panel=new Panel { Size=new(640,58),MouseFilter=MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel",UiStyles.Panel(new Color("142728"),new Color("a48b5d"),1,0)); AddChild(panel);
        _name=Text("",new(12,4),new(126,14),10);
        Bar(new(12,21),new(126,13),new Color("234f76"),1);
        Text("内力 · 未启用",new(12,20),new(126,14),9);
        _red=Bar(new(12,38),new(126,13),new Color("b7473e"),0);
        _health=Text("",new(12,37),new(126,14),9);
        for(int i=0;i<4;i++)
        {
            int slot=i;
            Skills[i]=Slot(new(152+i*62,6),new(58,46));
            Skills[i].Pressed+=()=> { if(CanUse() && !Player.IsMounted) Player.TryAction(InputActions.SkillActions[slot]); };
            var cover=new ColorRect { Color=new Color(0,0,0,.64f),MouseFilter=MouseFilterEnum.Ignore };
            Skills[i].AddChild(cover); _covers[i]=cover;
            var countdown=new Label { Position=new(1,19),Size=new(56,23),HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=MouseFilterEnum.Ignore };
            countdown.AddThemeFontSizeOverride("font_size",17); countdown.AddThemeColorOverride("font_color",Colors.White);
            countdown.AddThemeColorOverride("font_shadow_color",Colors.Black); countdown.AddThemeConstantOverride("shadow_offset_x",1);
            Skills[i].AddChild(countdown); _cooldowns[i]=countdown;
            Items[i]=Slot(new(412+i*55,6),new(51,46));
            Items[i].Pressed+=()=>UseItem(slot);
        }
        Refresh();
    }
    public bool UseItem(int slot)
    {
        if(!CanUse() || Player.IsDead || slot!=0 || !Session.Progress.HasTorch) return false;
        if(Session.TorchInUse) Session.PutAwayTorch(); else Session.UseTorch();
        return true;
    }
    public override void _Process(double delta) => Refresh();
    private void Refresh()
    {
        _name.Text=Player.Ui.DisplayName+"  ·  行旅";
        _red.Size=new(126*Mathf.Clamp((float)Player.Health.CurrentHealth/Math.Max(1,Player.Health.MaxHealth),0,1),13);
        _health.Text=$"{Player.Health.CurrentHealth} / {Player.Health.MaxHealth}";
        for(int i=0;i<4;i++)
        {
            var action=InputActions.SkillActions[i]; var skill=Player.GetSkill(action);
            float remaining=Player.Cooldown(action);
            Skills[i].Text=$"{Session.Bindings.Label(InputActions.SkillSlots[i])}\n{(remaining>0 ? " " : skill?.DisplayName ?? "空槽")}";
            Skills[i].TooltipText=skill?.Description ?? "尚未装备技能";
            Skills[i].Disabled=skill==null || remaining>0 || !CanUse() || !Player.CanAct || Player.IsMounted;
            _cooldowns[i].Text=remaining<=0 ? "" : remaining<10 ? $"{remaining:0.0}" : $"{Math.Ceiling(remaining):0}";
            _covers[i].Visible=remaining>0;
            float height=skill==null ? 0 : 44*Mathf.Clamp(remaining/Mathf.Max(.01f,skill.Cooldown),0,1);
            _covers[i].Position=new(1,45-height); _covers[i].Size=new(56,height);
            int count=i==0 && Session.Progress.Items.TryGetValue(AdventureProgress.TorchId,out int total) ? total : 0;
            Items[i].Text=$"{Session.Bindings.Label(InputActions.ItemSlots[i])}   ×{count}\n{(i==0 ? Session.TorchInUse ? "收火把" : "火把" : "空槽")}";
            Items[i].Disabled=count==0 || !CanUse() || Player.IsDead;
            Items[i].TooltipText=i==0 ? $"火把库存 {count}；可反复使用，不消耗数量。" : "尚未配置快捷物品";
        }
    }
    private Button Slot(Vector2 position,Vector2 size)
    {
        var button=new Button { Position=position,Size=size,FocusMode=FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size",9);
        button.AddThemeStyleboxOverride("normal",UiStyles.Panel(new Color("2c3c37"),new Color("897b58"),1,3));
        button.AddThemeStyleboxOverride("hover",UiStyles.Panel(new Color("445346"),new Color("e8d4a2"),1,3));
        button.AddThemeStyleboxOverride("disabled",UiStyles.Panel(new Color("202e2c"),new Color("526054"),1,3));
        button.AddThemeColorOverride("font_color",new Color("efdfb8"));
        button.AddThemeColorOverride("font_disabled_color",new Color("a5aa9b"));
        AddChild(button); return button;
    }
    private ColorRect Bar(Vector2 position,Vector2 size,Color color,double value)
    {
        var background=new ColorRect { Position=position,Size=size,Color=new Color("101b20"),MouseFilter=MouseFilterEnum.Ignore };
        AddChild(background);
        var bar=new ColorRect { Position=position,Size=new(size.X*(float)value,size.Y),Color=color,MouseFilter=MouseFilterEnum.Ignore };
        AddChild(bar); return bar;
    }
    private Label Text(string text,Vector2 position,Vector2 size,int font)
    {
        var label=new Label { Text=text,Position=position,Size=size,HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size",font); label.AddThemeColorOverride("font_color",new Color("efdfb8")); AddChild(label); return label;
    }
}
