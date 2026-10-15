using Godot;
using System;

/// <summary>可复用的模态对话面板，支持多选项、键盘焦点和关闭；业务状态通过回调提交。</summary>
public partial class CityDialogue : CanvasLayer
{
    public bool IsOpen => _shade.Visible;
    private Control _shade;
    private Label _speaker, _body;
    private VBoxContainer _choices;
    public event Action Closed;
    public override void _Ready()
    {
        Layer = 10;
        var root = new Control(); root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); AddChild(root);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        root.Theme = new Theme { DefaultFont = new SystemFont { FontNames = GameFontNames.CreateChineseFallbacks() }, DefaultFontSize = 12 };
        _shade = new Control { Visible = false }; root.AddChild(_shade); _shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var dim = new ColorRect { Color = new Color(0.025f, .045f, .04f, .68f) }; _shade.AddChild(dim); dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var panel = new PanelContainer { Position = new(76, 58), Size = new(488, 255) };
        panel.AddThemeStyleboxOverride("panel", Box(new Color("162d30"), new Color("bda66f"))); _shade.AddChild(panel);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation",10); panel.AddChild(column);
        _speaker = new Label(); _speaker.AddThemeFontSizeOverride("font_size", 19); _speaker.AddThemeColorOverride("font_color", new Color("ead5a0")); column.AddChild(_speaker);
        _body = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new(440, 60) }; column.AddChild(_body);
        _choices = new VBoxContainer(); column.AddChild(_choices);
    }
    public void Open(string speaker, string body, params (string Text, Action Action)[] choices)
    {
        _speaker.Text = speaker; _body.Text = body;
        foreach (var child in _choices.GetChildren()) { _choices.RemoveChild(child); child.QueueFree(); }
        foreach (var choice in choices)
        {
            var button = new Button { Text = choice.Text, CustomMinimumSize = new(440, 27) };
            button.Pressed += () => { Close(); choice.Action?.Invoke(); };
            _choices.AddChild(button);
        }
        var close = new Button { Text = "离开  [Esc]", CustomMinimumSize = new(440, 25) };
        close.Pressed += Close; _choices.AddChild(close);
        _shade.Show(); (_choices.GetChild(0) as Control)?.GrabFocus();
    }
    public void Close() { _shade.Hide(); Closed?.Invoke(); }
    public override void _UnhandledInput(InputEvent input)
    {
        if (!IsOpen) return;
        if (input is InputEventKey { Pressed: true, Echo: false } key && key.Keycode == Key.Escape) Close();
        GetViewport().SetInputAsHandled();
    }
    public static StyleBoxFlat Box(Color fill, Color edge) => new()
    {
        BgColor = fill, BorderColor = edge, BorderWidthBottom = 1, BorderWidthTop = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
        CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5, CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5,
        ContentMarginLeft = 17, ContentMarginRight = 17, ContentMarginTop = 13, ContentMarginBottom = 13
    };
}
