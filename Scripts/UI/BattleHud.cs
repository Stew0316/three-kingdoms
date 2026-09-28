using Godot;

public partial class BattleHud : CanvasLayer
{
    private Arena _arena;
    private Label _playerHealth;
    private Label _enemyHealth;
    private ProgressBar _playerBar;
    private ProgressBar _enemyBar;
    private Label _state;
    private Label _skills;
    private Label _clock;
    private Control _overlay;
    private Label _result;
    private Label _subtitle;
    private Button _resume;
    private bool _showDebug;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _arena = GetParent<Arena>();
        var root = new Control { Name = "Layout", MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var font = new SystemFont { FontNames = new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC" } };
        root.Theme = new Theme { DefaultFont = font, DefaultFontSize = 12 };
        AddChild(root);
        AddPanel(root, new Rect2(16, 10, 608, 53), new Color("1d282b"));
        AddLabel(root, "吕布", new Rect2(29, 16, 110, 22), 18, new Color("a7ded2"));
        AddLabel(root, "魏延", new Rect2(501, 16, 110, 22), 18, new Color("e1a48c"), HorizontalAlignment.Right);
        AddLabel(root, "演 武 场", new Rect2(248, 15, 144, 23), 18, new Color("e0ca93"), HorizontalAlignment.Center);
        _clock = AddLabel(root, "", new Rect2(268, 41, 104, 18), 10, new Color("a5b2ad"), HorizontalAlignment.Center);
        _playerHealth = AddLabel(root, "", new Rect2(100, 21, 119, 18), 10, new Color("c5d1c8"), HorizontalAlignment.Right);
        _enemyHealth = AddLabel(root, "", new Rect2(421, 21, 99, 18), 10, new Color("d4c6b5"));
        _playerBar = AddBar(root, new Rect2(29, 45, 190, 6), new Color("70b8a7"));
        _enemyBar = AddBar(root, new Rect2(421, 45, 190, 6), new Color("c87960"));
        _skills = AddLabel(root, "", new Rect2(105, 66, 430, 17), 11, new Color("ded0ac"), HorizontalAlignment.Center);
        _state = AddLabel(root, "", new Rect2(26, 100, 550, 18), 10, new Color("f3dfb4"));
        AddLabel(root, "WASD / 方向键 移动    J / 左键 普攻    K 突进    L 横扫    空格 闪避", new Rect2(14, 329, 612, 16), 11, new Color("d8cfb6"), HorizontalAlignment.Center);
        AddLabel(root, "红色扇形是敌方预警 · 绕开后反击    |    Esc 暂停    R 重开    F3 状态", new Rect2(14, 345, 612, 14), 9, new Color("9ba9a1"), HorizontalAlignment.Center);

        _overlay = new Control { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_overlay);
        var dim = new ColorRect { Color = new Color(0, 0, 0, .55f) };
        _overlay.AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddPanel(_overlay, new Rect2(170, 103, 300, 163), new Color("202c2e"));
        _result = AddLabel(_overlay, "", new Rect2(180, 121, 280, 34), 25, new Color("e4cb8f"), HorizontalAlignment.Center);
        _subtitle = AddLabel(_overlay, "", new Rect2(180, 163, 280, 20), 12, new Color("bdc9bd"), HorizontalAlignment.Center);
        _resume = AddButton(_overlay, "继续", new Rect2(206, 213, 106, 30));
        _resume.Pressed += TogglePause;
        var restart = AddButton(_overlay, "重新挑战 · R", new Rect2(326, 213, 110, 30));
        restart.Pressed += Restart;
        var player = _arena.GetNode<Combatant>("Fighters/LvBu");
        var enemy = _arena.GetNode<Combatant>("Fighters/WeiYan");
        player.Health.Damaged += UpdatePlayerHealth;
        enemy.Health.Damaged += UpdateEnemyHealth;
        UpdatePlayerHealth(player.Health.CurrentHealth, player.Health.MaxHealth);
        UpdateEnemyHealth(enemy.Health.CurrentHealth, enemy.Health.MaxHealth);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input.IsActionPressed("restart_battle"))
        {
            // 重载会立即把旧 HUD 移出场景树；必须先处理输入，之后不再访问其 Viewport。
            GetViewport().SetInputAsHandled();
            Restart();
            return;
        }
        else if (input.IsActionPressed("pause_game")) { GetViewport().SetInputAsHandled(); TogglePause(); }
        else if (input is InputEventKey key && key.Pressed && !key.Echo && key.PhysicalKeycode == Key.F3)
            _showDebug = !_showDebug;
    }

    public override void _Process(double delta)
    {
        // 子节点 Ready 先于 Arena.Ready；运行帧开始后主场景引用才可用。
        if (_arena.Player == null) return;
        _clock.Text = $"吕布 · 对 · 魏延   {_arena.BattleSeconds:0}s";
        _skills.Text = $"K 突进 {ReadyText(CombatAction.Dash)}     L 横扫 {ReadyText(CombatAction.Sweep)}     空格 闪避 {ReadyText(CombatAction.Dodge)}";
        _state.Visible = _showDebug;
        _state.Text = $"吕布 {_arena.Player.CurrentState} / {_arena.Player.Phase}     魏延 {_arena.Enemy.CurrentState} / {_arena.Enemy.GetNode<WeiYanController>("Controller").Decision}";
        bool paused = GetTree().Paused;
        _overlay.Visible = _arena.Finished || paused;
        if (!_overlay.Visible) return;
        _result.Text = _arena.Finished ? _arena.Result : "稍作休整";
        _subtitle.Text = _arena.Finished ? $"用时 {_arena.BattleSeconds:0.0} 秒 · 按 R 再战一局" : "按 Esc 继续对战";
        _resume.Visible = !_arena.Finished;
    }

    private string ReadyText(CombatAction action) => _arena.Player.Cooldown(action) <= 0 ? "就绪" : $"{_arena.Player.Cooldown(action):0.0}s";
    private void UpdatePlayerHealth(int current, int max) { _playerBar.MaxValue = max; _playerBar.Value = current; _playerHealth.Text = $"{current} / {max}"; }
    private void UpdateEnemyHealth(int current, int max) { _enemyBar.MaxValue = max; _enemyBar.Value = current; _enemyHealth.Text = $"{current} / {max}"; }
    private void TogglePause() { if (!_arena.Finished) GetTree().Paused = !GetTree().Paused; }
    private void Restart() { GetTree().Paused = false; GetTree().ReloadCurrentScene(); }

    private static StyleBoxFlat Box(Color color) => new() { BgColor = color, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 };
    private static void AddPanel(Control parent, Rect2 rect, Color color)
    {
        var panel = new Panel { Position = rect.Position, Size = rect.Size, MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", Box(color)); parent.AddChild(panel);
    }
    private static Label AddLabel(Control parent, string text, Rect2 rect, int size, Color color, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new Label { Text = text, Position = rect.Position, Size = rect.Size, HorizontalAlignment = align, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", color); parent.AddChild(label); return label;
    }
    private static ProgressBar AddBar(Control parent, Rect2 rect, Color color)
    {
        var bar = new ProgressBar { Position = rect.Position, Size = rect.Size, ShowPercentage = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        // Godot 默认主题即使隐藏百分比仍保留字体最小高度；细血条要同时覆盖字体尺寸。
        bar.AddThemeFontSizeOverride("font_size", 1);
        var background = Box(new Color("111b1e"));
        var fill = Box(color);
        foreach (var style in new[] { background, fill })
        {
            style.CornerRadiusTopLeft = style.CornerRadiusTopRight = 2;
            style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight = 2;
        }
        bar.AddThemeStyleboxOverride("background", background); bar.AddThemeStyleboxOverride("fill", fill);
        parent.AddChild(bar);
        bar.Size = rect.Size; // 应用主题之后再设置，撤销构造时默认主题对 Size 的钳制。
        return bar;
    }
    private static Button AddButton(Control parent, string text, Rect2 rect)
    {
        var button = new Button { Text = text, Position = rect.Position, Size = rect.Size };
        button.AddThemeStyleboxOverride("normal", Box(new Color("48534a")));
        button.AddThemeStyleboxOverride("hover", Box(new Color("65715e")));
        button.AddThemeStyleboxOverride("pressed", Box(new Color("343f38")));
        parent.AddChild(button); return button;
    }
}
