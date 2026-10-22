using Godot;
using System;

/// <summary>居中紧凑底栏：竖剑生命、技能、铜饰、物品和竖葫芦，不绘制大底板。</summary>
public partial class PlayerStatusBar : Control
{
    // 中央组合横跨 148–492，整体中心保持在 640 逻辑像素画布的正中。
    private const float SlotSize=30, SlotY=26, SkillX=188, ItemX=332;
    public Combatant Player { get; set; }
    public Func<bool> CanUse { get; set; }
    public Button[] Skills { get; } = new Button[4];
    public Button[] Items { get; } = new Button[4];
    private VerticalResourceGauge _health;
    private AdventureItemDefinition _torch;
    private readonly TextureRect[] _skillIcons=new TextureRect[4], _itemIcons=new TextureRect[4];
    private readonly Label[] _cooldowns=new Label[4], _skillKeys=new Label[4], _itemKeys=new Label[4], _counts=new Label[4];
    private readonly ColorRect[] _covers=new ColorRect[4];
    private ColorRect _torchActive;
    private AdventureSession Session => AdventureSession.Instance;

    public override void _Ready()
    {
        // 空白区域不接管鼠标，只有量表与按钮参与命中。
        MouseFilter=MouseFilterEnum.Ignore;
        Theme=UiStyles.ChineseTheme(10);
        _torch=GD.Load<AdventureItemDefinition>("res://Resources/Config/Items/torch.tres");
        _health=new VerticalResourceGauge { Name="HealthSword",Position=new(148,4),Size=new(32,54) };
        AddChild(_health);
        AddChild(new VerticalResourceGauge { Name="ManaGourd",Position=new(460,4),Size=new(32,54),IsMana=true,
            ValueText="内力尚未启用",TooltipText="内力尚未启用，当前没有消耗或恢复数值。" });
        for(int i=0;i<4;i++)
        {
            int slot=i;
            Skills[i]=Slot(new(SkillX+i*SlotSize,SlotY));
            Skills[i].Pressed+=()=> { if(CanUse() && !Player.IsMounted) Player.TryAction(InputActions.SkillActions[slot]); };
            _skillIcons[i]=Icon(Skills[i],"SkillIcon");
            // 暂无专用位图的已装备技能使用通用铜色兵刃纹章，不往槽内塞名称。
            _skillIcons[i].Draw+=()=> { if(_skillIcons[slot].Texture==null && Player.GetSkill(InputActions.SkillActions[slot])!=null) DrawFallback(_skillIcons[slot]); };
            _covers[i]=new ColorRect { Color=new Color(0,0,0,.62f),MouseFilter=MouseFilterEnum.Ignore };
            Skills[i].AddChild(_covers[i]);
            _cooldowns[i]=Overlay(Skills[i],"Cooldown",new(0,9),new(SlotSize,19),12,HorizontalAlignment.Center);
            _skillKeys[i]=Overlay(Skills[i],"Shortcut",new(2,0),new(26,12),8);
            Items[i]=Slot(new(ItemX+i*SlotSize,SlotY));
            Items[i].Pressed+=()=>UseItem(slot);
            _itemIcons[i]=Icon(Items[i],"ItemIcon");
            _itemKeys[i]=Overlay(Items[i],"Shortcut",new(2,0),new(26,12),8);
            _counts[i]=Overlay(Items[i],"Quantity",new(1,17),new(27,12),9,HorizontalAlignment.Right);
        }
        _torchActive=new ColorRect { Name="InUse",Position=new(25,3),Size=new(3,3),
            Color=new Color("ffd586"),MouseFilter=MouseFilterEnum.Ignore };
        Items[0].AddChild(_torchActive);
        // 相邻槽直接相接，只在共用边界画一条线。
        for(int i=1;i<4;i++)
            foreach(float start in new[]{SkillX,ItemX})
                AddChild(new ColorRect { Position=new(start+i*SlotSize-.5f,SlotY),Size=new(1,SlotSize),
                    Color=new Color("907c55"),MouseFilter=MouseFilterEnum.Ignore });
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
        _health.Ratio=Mathf.Clamp((float)Player.Health.CurrentHealth/Math.Max(1,Player.Health.MaxHealth),0,1);
        _health.ValueText=$"生命 {Player.Health.CurrentHealth} / {Player.Health.MaxHealth}";
        _health.TooltipText=_health.ValueText;
        for(int i=0;i<4;i++)
        {
            var action=InputActions.SkillActions[i]; var skill=Player.GetSkill(action);
            float remaining=Player.Cooldown(action);
            _skillIcons[i].Texture=skill?.Icon;
            _skillIcons[i].Visible=skill!=null;
            _skillKeys[i].Text=Session.Bindings.Label(InputActions.SkillSlots[i]);
            Skills[i].TooltipText=skill?.Description ?? "尚未装备技能";
            Skills[i].Disabled=skill==null || remaining>0 || !CanUse() || !Player.CanAct || Player.IsMounted;
            _skillIcons[i].SelfModulate=Skills[i].Disabled && remaining<=0 ? new Color(.48f,.48f,.48f) : Colors.White;
            _cooldowns[i].Text=remaining<=0 ? "" : remaining<10 ? $"{remaining:0.0}" : $"{Math.Ceiling(remaining):0}";
            _covers[i].Visible=remaining>0;
            float height=skill==null ? 0 : SlotSize*Mathf.Clamp(remaining/Mathf.Max(.01f,skill.Cooldown),0,1);
            _covers[i].Position=new(0,SlotSize-height); _covers[i].Size=new(SlotSize,height);

            var item=i==0 ? _torch : null;
            int count=item!=null && Session.Progress.Items.TryGetValue(item.ItemId,out int total) ? total : 0;
            _itemIcons[i].Texture=item?.Icon;
            _itemIcons[i].Visible=item?.Icon!=null;
            _itemKeys[i].Text=Session.Bindings.Label(InputActions.ItemSlots[i]);
            _counts[i].Text=item==null ? "" : count.ToString();
            Items[i].Disabled=count<=0 || !CanUse() || Player.IsDead;
            _itemIcons[i].SelfModulate=count<=0 ? new Color(.35f,.35f,.35f) : Colors.White;
            Items[i].TooltipText=item==null ? "尚未配置快捷物品" :
                $"{item.DisplayName} ×{count} · {(Session.TorchInUse ? "正在使用，点击收起" : "点击使用")}\n{item.Description}";
        }
        _torchActive.Visible=Session.TorchInUse;
    }

    private Button Slot(Vector2 position)
    {
        var button=new Button { Position=position,Size=new(SlotSize,SlotSize),FocusMode=FocusModeEnum.None };
        // 单格淡底只帮助空槽辨识，没有框线、圆角或整条背景。
        foreach(string state in new[]{"normal","hover","pressed","disabled","focus"})
            button.AddThemeStyleboxOverride(state,new StyleBoxFlat { BgColor=new Color(.025f,.035f,.03f,.62f) });
        AddChild(button); return button;
    }

    private static TextureRect Icon(Control parent,string name)
    {
        var icon=new TextureRect { Name=name,Size=new(SlotSize,SlotSize),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,TextureFilter=TextureFilterEnum.Linear,
            MouseFilter=MouseFilterEnum.Ignore };
        parent.AddChild(icon); return icon;
    }

    private static Label Overlay(Control parent,string name,Vector2 position,Vector2 size,int font,HorizontalAlignment alignment=HorizontalAlignment.Left)
    {
        var label=new Label { Name=name,Position=position,Size=size,HorizontalAlignment=alignment,MouseFilter=MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size",font); label.AddThemeColorOverride("font_color",new Color("fff0cf"));
        label.AddThemeColorOverride("font_outline_color",new Color("0b0e0c")); label.AddThemeConstantOverride("outline_size",3);
        parent.AddChild(label); return label;
    }

    private static void DrawFallback(TextureRect target)
    {
        target.DrawArc(new(15,16),9,0,Mathf.Tau,32,new Color("736346"),1,true);
        target.DrawLine(new(9,23),new(22,9),new Color("c6a978"),2,true);
        target.DrawLine(new(9,10),new(22,23),new Color("c6a978"),2,true);
        target.DrawLine(new(7,19),new(12,24),new Color("c6a978"),1,true);
        target.DrawLine(new(18,23),new(23,18),new Color("c6a978"),1,true);
    }

    public override void _Draw()
    {
        // 方孔铜饰和短结绳分隔技能/物品两组，属于游戏化纹饰。
        Vector2 center=new(320,35);
        DrawArc(center,6.8f,0,Mathf.Tau,48,new Color("503e29"),4,true);
        DrawArc(center,6.2f,0,Mathf.Tau,48,new Color("b6975f"),2.5f,true);
        DrawArc(center,7.5f,Mathf.Pi,Mathf.Tau,24,new Color("dfbf7a"),.7f,true);
        DrawRect(new Rect2(center-new Vector2(2.4f,2.4f),new(4.8f,4.8f)),new Color("b6975f"),false,.8f);
        DrawLine(center+new Vector2(0,7),center+new Vector2(0,13),new Color("764038"),1.8f,true);
        DrawCircle(center+new Vector2(0,11),1.5f,new Color("b08c51"),true,-1,true);
        DrawLine(center+new Vector2(0,13),center+new Vector2(-2,19),new Color("8f463d"),1,true);
        DrawLine(center+new Vector2(0,13),center+new Vector2(2,19),new Color("8f463d"),1,true);
    }
}
