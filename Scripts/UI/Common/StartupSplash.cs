using Godot;

/// <summary>黑底致谢页；文字组在屏幕正中，猫头鹰与工作室名称横排位于底部正中。</summary>
public partial class StartupSplash : CanvasLayer
{
    private Label _error;
    private bool _failed;
    private SceneTree _sceneTree;
    private bool _waitingForDraw;
    public override void _Ready()
    {
        Name="StartupSplash"; Layer=120; ProcessMode=ProcessModeEnum.Always;
        var root=new ColorRect { Name="BlackBackground",Color=Colors.Black,MouseFilter=Control.MouseFilterEnum.Stop };
        AddChild(root); root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var font=new SystemFont { FontNames=GameFontNames.CreateChineseFallbacks() };
        var message=new VBoxContainer { Name="ThankYou",AnchorLeft=.5f,AnchorRight=.5f,AnchorTop=.5f,AnchorBottom=.5f,
            OffsetLeft=-300,OffsetRight=300,OffsetTop=-37,OffsetBottom=37,Alignment=BoxContainer.AlignmentMode.Center };
        message.AddThemeConstantOverride("separation",9); root.AddChild(message);
        AddLabel(message,"谢谢你玩我的游戏",30,font);
        AddLabel(message,"thank you for playing my games.",17,font);
        var brand=new HBoxContainer { Name="StudioBrand",AnchorLeft=.5f,AnchorRight=.5f,AnchorTop=1,AnchorBottom=1,
            OffsetLeft=-130,OffsetRight=130,OffsetTop=-82,OffsetBottom=-22,Alignment=BoxContainer.AlignmentMode.Center };
        brand.AddThemeConstantOverride("separation",14); root.AddChild(brand);
        // 采样指定原图的猫头鹰区域；材质去除深底，不改动原图与品牌形状。
        var owl=new TextureRect { Name="OwlLogo",Texture=new AtlasTexture {
            Atlas=GD.Load<Texture2D>("res://BaseSource/logo-owl-dark-v2.png"),Region=new Rect2(225,145,805,705) },
            CustomMinimumSize=new(65,60),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,TextureFilter=CanvasItem.TextureFilterEnum.Linear,
            Material=new ShaderMaterial { Shader=GD.Load<Shader>("res://Resources/Shaders/StartupLogo.gdshader") } };
        brand.AddChild(owl); AddLabel(brand,"stew studio",24,font);
        _error=new Label { HorizontalAlignment=HorizontalAlignment.Center,AnchorLeft=0,AnchorRight=1,AnchorTop=.7f,AnchorBottom=.7f };
        _error.AddThemeFontOverride("font",font); _error.AddThemeFontSizeOverride("font_size",12); root.AddChild(_error);
    }
    private static void AddLabel(Node parent,string text,int size,Font font)
    {
        var label=new Label { Text=text,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center };
        label.AddThemeFontOverride("font",font); label.AddThemeFontSizeOverride("font_size",size);
        label.AddThemeColorOverride("font_color",Colors.White); parent.AddChild(label);
    }
    public void ShowError()
    {
        _failed=true; DisconnectLoadingSignals();
        _error.Text="加载失败，请关闭游戏后重试。";
    }
    public void DismissAfterSceneChange()
    {
        _sceneTree=GetTree(); _sceneTree.SceneChanged+=OnSceneChanged;
    }
    private void OnSceneChanged()
    {
        _sceneTree.SceneChanged-=OnSceneChanged;
        _sceneTree=null;
        if(DisplayServer.GetName()=="headless") { QueueFree(); return; }
        _waitingForDraw=true;
        RenderingServer.FramePostDraw+=OnFirstSceneDraw;
    }
    private void OnFirstSceneDraw()
    {
        DisconnectLoadingSignals();
        if(!_failed) QueueFree();
    }
    private void DisconnectLoadingSignals()
    {
        if(_sceneTree!=null) { _sceneTree.SceneChanged-=OnSceneChanged; _sceneTree=null; }
        if(_waitingForDraw) RenderingServer.FramePostDraw-=OnFirstSceneDraw;
        _waitingForDraw=false;
    }
    public override void _ExitTree() => DisconnectLoadingSignals();
}
