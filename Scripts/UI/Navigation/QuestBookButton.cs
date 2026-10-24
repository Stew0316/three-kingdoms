using Godot;

/// <summary>侧边任务书按钮，只负责图标与任务提示光；任务内容和点击行为由装配方绑定。</summary>
public partial class QuestBookButton : Button
{
    private bool _hasQuest;
    private double _glowPhase;
    private TextureRect _book;

    /// <summary>存在待处理任务时显示黄色柔光，不在控件内读取或修改任务状态。</summary>
    public bool HasQuest
    {
        get => _hasQuest;
        set
        {
            if(_hasQuest==value) return;
            _hasQuest=value;
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        Flat=true;
        Text="";
        MouseFilter=MouseFilterEnum.Stop;
        MouseDefaultCursorShape=CursorShape.PointingHand;
        FocusMode=FocusModeEnum.None;
        // 按钮只保留图标；图像与画在边界外的光晕都不创建额外鼠标命中区。
        foreach(string state in new[]{"normal","hover","pressed","disabled","focus"})
            AddThemeStyleboxOverride(state,new StyleBoxEmpty());
        _book=new TextureRect {
            Name="BookIcon",
            Texture=GD.Load<Texture2D>("res://Source/UI/Icons/Quest/QuestBookV1.png"),
            ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter=TextureFilterEnum.Linear,
            MouseFilter=MouseFilterEnum.Ignore
        };
        AddChild(_book);
        _book.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if(string.IsNullOrEmpty(TooltipText)) TooltipText="任务";
        MouseEntered+=()=> { _book.SelfModulate=new Color(1.08f,1.06f,1.02f); };
        MouseExited+=()=> { _book.SelfModulate=Colors.White; };
    }

    public override void _Process(double delta)
    {
        // 菜单画布可能持续处理暂停状态，仍显式冻结呼吸相位，避免任务提示偷偷推进。
        if(!_hasQuest || GetTree().Paused) return;
        _glowPhase=(_glowPhase+delta*1.45)%Mathf.Tau;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if(!_hasQuest) return;
        float breath=.84f+.16f*Mathf.Sin((float)_glowPhase);
        Vector2 center=Size*.5f;
        // 用窄椭圆轮廓的多层透明线模拟模糊，书本覆盖内沿，不绘制发光底板。
        for(int layer=12;layer>=0;layer--)
        {
            float distance=layer*.29f;
            float opacity=.095f*Mathf.Exp(-distance*distance/4.1f)*breath;
            var contour=new Vector2[65];
            for(int i=0;i<contour.Length;i++)
            {
                float angle=i*Mathf.Tau/64;
                contour[i]=center+new Vector2(Mathf.Cos(angle)*(Size.X*.355f+distance),
                    Mathf.Sin(angle)*(Size.Y*.35f+distance));
            }
            DrawPolyline(contour,new Color(1,.75f,.22f,opacity),1.1f,true);
        }
    }
}
