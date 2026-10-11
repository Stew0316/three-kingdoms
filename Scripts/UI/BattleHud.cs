using Godot;

/// <summary>对战界面：显示生命、冷却、状态及结算，并接收暂停、重开和调试输入。</summary>
public partial class BattleHud : CanvasLayer
{
    // 当前对局入口，提供角色引用、时长和结算状态。
    private Arena _arena;
    // 玩家当前生命/上限的数字文本。
    private Label _playerHealth;
    // 敌方当前生命/上限的数字文本。
    private Label _enemyHealth;
    // 玩家生命进度条，由生命变化信号更新。
    private ProgressBar _playerBar;
    // 敌方生命进度条，由生命变化信号更新。
    private ProgressBar _enemyBar;
    // F3 调试文本，显示双方状态与 AI 决策。
    private Label _state;
    // 突进、横扫和闪避的就绪/剩余冷却文本。
    private Label _skills;
    private Label _playerPassives, _enemyPassives, _controls; // 技能换装后从运行时装备重新读取。
    // 对战双方名称与本场累计时间。
    private Label _clock;
    // 暂停/结算遮罩及按钮容器，可见时拦截鼠标操作。
    private Control _overlay;
    // 遮罩上的暂停或胜负标题。
    private Label _result;
    // 遮罩上的用时与按键说明。
    private Label _subtitle;
    // 暂停时的继续按钮，结算后隐藏。
    private Button _resume;
    // 是否显示 F3 状态调试文本，与 F4 骨骼显示独立。
    private bool _showDebug;

    /// <summary>按 640×360 布局坐标建立控件并订阅生命信号；暂停时仍处理界面。</summary>
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _arena = GetParent<Arena>();
        var configuredPlayer = _arena.GetNode<Combatant>(SceneNodePaths.Player);
        var configuredEnemy = _arena.GetNode<Combatant>(SceneNodePaths.Enemy);
        var root = new Control { Name = NodeNames.Layout, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var font = new SystemFont { FontNames = GameFontNames.CreateChineseFallbacks() };
        root.Theme = new Theme { DefaultFont = font, DefaultFontSize = 12 };
        AddChild(root);
        AddPanel(root, new Rect2(16, 10, 608, 53), new Color(BattleUiColors.Panel));
        AddLabel(root, configuredPlayer.Ui.DisplayName, new Rect2(29, 16, 110, 22), 18, configuredPlayer.Ui.NameColor);
        AddLabel(root, _arena.IsSoloPractice ? "单人练习" : configuredEnemy.Ui.DisplayName, new Rect2(501, 16, 110, 22), 18, configuredEnemy.Ui.NameColor, HorizontalAlignment.Right);
        AddLabel(root, _arena.IsSoloPractice ? configuredPlayer.Ui.DisplayName + "步态练习" : BattleTexts.ArenaTitle, new Rect2(230, 15, 180, 23), 18, new Color(BattleUiColors.Title), HorizontalAlignment.Center);
        _clock = AddLabel(root, string.Empty, new Rect2(268, 41, 104, 18), 10, new Color(BattleUiColors.SecondaryText), HorizontalAlignment.Center);
        _playerHealth = AddLabel(root, string.Empty, new Rect2(100, 21, 119, 18), 10, new Color(BattleUiColors.PlayerHealthText), HorizontalAlignment.Right);
        _enemyHealth = AddLabel(root, string.Empty, new Rect2(421, 21, 99, 18), 10, new Color(BattleUiColors.EnemyHealthText));
        _playerBar = AddBar(root, new Rect2(29, 45, 190, 6), configuredPlayer.Ui.HealthColor);
        _enemyBar = AddBar(root, new Rect2(421, 45, 190, 6), configuredEnemy.Ui.HealthColor);
        _enemyBar.Visible = _enemyHealth.Visible = !_arena.IsSoloPractice;
        _skills = AddLabel(root, string.Empty, new Rect2(105, 66, 430, 17), 11, new Color(BattleUiColors.SkillText), HorizontalAlignment.Center);
        _state = AddLabel(root, string.Empty, new Rect2(26, 100, 550, 18), 10, new Color(BattleUiColors.DebugText));
        AddPanel(root,new Rect2(22,301,596,23),new Color("f5e4b9"));
        _playerPassives = AddLabel(root, DescribePassives(configuredPlayer), new Rect2(30,304,282,16),10,configuredPlayer.Ui.DetailColor);
        _enemyPassives = AddLabel(root, _arena.IsSoloPractice ? "对手已停用 · 自由移动观察步态" : DescribePassives(configuredEnemy), new Rect2(322,304,286,16),10,configuredEnemy.Ui.DetailColor,HorizontalAlignment.Right);
        _playerPassives.ClipText = _enemyPassives.ClipText = true; // 长技能名不能挤出边框；悬停仍可查看完整内容。
        _playerPassives.MouseFilter = _enemyPassives.MouseFilter = Control.MouseFilterEnum.Pass;
        _controls = AddLabel(root, string.Empty, new Rect2(14, 329, 612, 16), 11, new Color(BattleUiColors.ControlText), HorizontalAlignment.Center);
        AddLabel(root, BattleTexts.DebugControls, new Rect2(14, 345, 612, 14), 9, new Color(BattleUiColors.HelpText), HorizontalAlignment.Center);

        _overlay = new Control { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_overlay);
        var dim = new ColorRect { Color = new Color(0, 0, 0, .55f) };
        _overlay.AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddPanel(_overlay, new Rect2(170, 103, 300, 163), new Color(BattleUiColors.OverlayPanel));
        _result = AddLabel(_overlay, string.Empty, new Rect2(180, 121, 280, 34), 25, new Color(BattleUiColors.OverlayTitle), HorizontalAlignment.Center);
        _subtitle = AddLabel(_overlay, string.Empty, new Rect2(180, 163, 280, 20), 12, new Color(BattleUiColors.OverlayText), HorizontalAlignment.Center);
        _resume = AddButton(_overlay, BattleTexts.Resume, new Rect2(206, 213, 106, 30));
        _resume.Pressed += TogglePause;
        var restart = AddButton(_overlay, BattleTexts.Restart, new Rect2(326, 213, 110, 30));
        restart.Pressed += Restart;
        var player = _arena.GetNode<Combatant>(SceneNodePaths.Player);
        var enemy = _arena.GetNode<Combatant>(SceneNodePaths.Enemy);
        player.Health.Damaged += UpdatePlayerHealth;
        enemy.Health.Damaged += UpdateEnemyHealth;
        UpdatePlayerHealth(player.Health.CurrentHealth, player.Health.MaxHealth);
        UpdateEnemyHealth(enemy.Health.CurrentHealth, enemy.Health.MaxHealth);
    }

    /// <summary>HUD 从相同 Resource 展示数值，避免文案与配置分叉。</summary>
    private static string DescribePassives(Combatant actor)
    {
        var parts=new System.Collections.Generic.List<string>();
        foreach(var passive in actor.Skills.Passives)
            parts.Add(passive.DisplayName + " " + (passive.Effect switch
            {
                PassiveConfig.EffectKind.Reflect => $"{passive.Damage}",
                PassiveConfig.EffectKind.CounterSpin => $"{passive.Chance:P0}/{passive.Damage}",
                _ => $"{passive.Chance:P0}×{passive.CriticalMultiplier:0.0}"
            }));
        return string.Join(" · ",parts);
    }

    /// <summary>处理尚未被其他控件消费的 input；重开前先消费事件，避免访问已离树的 HUD。</summary>
    public override void _UnhandledInput(InputEvent input)
    {
        if (input.IsActionPressed(InputActions.Restart))
        {
            // 重载会立即把旧 HUD 移出场景树；必须先处理输入，之后不再访问其 Viewport。
            GetViewport().SetInputAsHandled();
            Restart();
            return;
        }
        else if (input.IsActionPressed(InputActions.Pause)) { GetViewport().SetInputAsHandled(); TogglePause(); }
        else if (input is InputEventKey key && key.Pressed && !key.Echo && key.PhysicalKeycode == DebugKeys.ToggleState)
            _showDebug = !_showDebug;
        else if (input is InputEventKey boneKey && boneKey.Pressed && !boneKey.Echo && boneKey.PhysicalKeycode == DebugKeys.ToggleBones)
        {
            foreach (var actor in new[] { _arena.Player, _arena.Enemy })
            {
                var rig = actor.Rig;
                rig.ShowBones = !rig.ShowBones;
            }
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>每帧读取对局快照刷新文本；delta 不用于推进战斗时间。</summary>
    public override void _Process(double delta)
    {
        // 子节点 Ready 先于 Arena.Ready；运行帧开始后主场景引用才可用。
        if (_arena.Player == null) return;
        var configuredPlayer = _arena.Player;
        var configuredEnemy = _arena.Enemy;
        _clock.Text = _arena.IsSoloPractice ? $"练习 {_arena.BattleSeconds:0.0}s" : string.Format(BattleTexts.BattleClockFormat,
            configuredPlayer.Ui.DisplayName, configuredEnemy.Ui.DisplayName, _arena.BattleSeconds);
        _skills.Text = $"K {SkillText(CombatAction.Dash)}    L {SkillText(CombatAction.Sweep)}    空格 {SkillText(CombatAction.Dodge)}";
        _playerPassives.Text = DescribePassives(configuredPlayer);
        _enemyPassives.Text = _arena.IsSoloPractice ? "对手已停用 · 自由移动观察步态" : DescribePassives(configuredEnemy);
        _playerPassives.TooltipText = _playerPassives.Text;
        _enemyPassives.TooltipText = _enemyPassives.Text;
        _controls.Text = $"WASD / 方向键 移动    J / 左键 {SkillName(CombatAction.Basic)}    K {SkillName(CombatAction.Dash)}    L {SkillName(CombatAction.Sweep)}    空格 {SkillName(CombatAction.Dodge)}";
        _state.Visible = _showDebug;
        _state.Text = _arena.IsSoloPractice ? $"{configuredPlayer.Ui.DisplayName}：{_arena.Player.Phase}　实际速度 {_arena.Player.WalkVelocity.Length():0}　步态 {_arena.Player.Rig.WalkPhase:0.00}" : string.Format(BattleTexts.DebugStateFormat,
            configuredPlayer.Ui.DisplayName, _arena.Player.CurrentState, _arena.Player.Phase,
            configuredEnemy.Ui.DisplayName, _arena.Enemy.CurrentState,
            _arena.Enemy.GetNode<WeiYanController>(NodeNames.Controller).Decision);
        bool paused = GetTree().Paused;
        _overlay.Visible = _arena.Finished || paused;
        if (!_overlay.Visible) return;
        _result.Text = _arena.Finished ? _arena.Result : BattleTexts.PauseTitle;
        _subtitle.Text = _arena.Finished
            ? string.Format(BattleTexts.FinishedSubtitleFormat, _arena.BattleSeconds)
            : BattleTexts.PausedSubtitle;
        _resume.Visible = !_arena.Finished;
    }

    private string SkillName(CombatAction slot) => _arena.Player.GetSkill(slot)?.DisplayName ?? "空槽";
    private string SkillText(CombatAction slot) => $"{SkillName(slot)} {ReadyText(slot)}";

    /// <summary>把 action 的剩余冷却格式化为“就绪”或保留一位小数的秒数。</summary>
    private string ReadyText(CombatAction action) => _arena.Player.Cooldown(action) <= 0
        ? BattleTexts.Ready
        : string.Format(BattleTexts.CooldownFormat, _arena.Player.Cooldown(action));
    /// <summary>使用 current 当前生命与 max 上限同步玩家血条和数字。</summary>
    private void UpdatePlayerHealth(int current, int max) { _playerBar.MaxValue = max; _playerBar.Value = current; _playerHealth.Text = string.Format(BattleTexts.HealthFormat, current, max); }
    /// <summary>使用 current 当前生命与 max 上限同步敌方血条和数字。</summary>
    private void UpdateEnemyHealth(int current, int max) { _enemyBar.MaxValue = max; _enemyBar.Value = current; _enemyHealth.Text = string.Format(BattleTexts.HealthFormat, current, max); }
    /// <summary>在对战尚未结算时切换暂停；HUD 自身使用 Always 模式保持可操作。</summary>
    private void TogglePause() { if (!_arena.Finished) GetTree().Paused = !GetTree().Paused; }
    /// <summary>先解除暂停，再重载当前场景；调用后旧 HUD 已离树，不应继续访问其节点。</summary>
    private void Restart() { GetTree().Paused = false; GetTree().ReloadCurrentScene(); }

    /// <summary>以 color 创建统一圆角底色样式，每次返回独立资源。</summary>
    private static StyleBoxFlat Box(Color color) => new()
    {
        BgColor=color,BorderColor=new Color("5c6849"),BorderWidthLeft=2,BorderWidthTop=2,BorderWidthRight=2,BorderWidthBottom=2,
        CornerRadiusTopLeft=5,CornerRadiusTopRight=5,CornerRadiusBottomLeft=5,CornerRadiusBottomRight=5
    };
    /// <summary>在 parent 下添加装饰面板；rect 为位置和尺寸，color 为底色，不接收鼠标。</summary>
    private static void AddPanel(Control parent, Rect2 rect, Color color)
    {
        var panel = new Panel { Position = rect.Position, Size = rect.Size, MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride(GodotPropertyNames.PanelStyle, Box(color)); parent.AddChild(panel);
    }
    /// <summary>创建文字标签并返回，便于后续刷新内容。</summary>
    /// <param name="parent">标签挂载的父控件。</param>
    /// <param name="text">初始显示文本。</param>
    /// <param name="rect">基准视口中的位置与尺寸。</param>
    /// <param name="size">字体大小，随视口统一缩放。</param>
    /// <param name="color">文字颜色。</param>
    /// <param name="align">水平对齐方式。</param>
    private static Label AddLabel(Control parent, string text, Rect2 rect, int size, Color color, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new Label { Text = text, Position = rect.Position, Size = rect.Size, HorizontalAlignment = align, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride(GodotPropertyNames.FontSize, size); label.AddThemeColorOverride(GodotPropertyNames.FontColor, color); parent.AddChild(label); return label;
    }
    /// <summary>在 parent 下建立细血条；rect 为布局区域，color 为已填充部分颜色。</summary>
    private static ProgressBar AddBar(Control parent, Rect2 rect, Color color)
    {
        var bar = new ProgressBar { Position = rect.Position, Size = rect.Size, ShowPercentage = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        // Godot 默认主题即使隐藏百分比仍保留字体最小高度；细血条要同时覆盖字体尺寸。
        bar.AddThemeFontSizeOverride(GodotPropertyNames.FontSize, 1);
        var background = Box(new Color(BattleUiColors.BarBackground));
        var fill = Box(color);
        foreach (var style in new[] { background, fill })
        {
            style.CornerRadiusTopLeft = style.CornerRadiusTopRight = 2;
            style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight = 2;
        }
        bar.AddThemeStyleboxOverride(GodotPropertyNames.BackgroundStyle, background); bar.AddThemeStyleboxOverride(GodotPropertyNames.FillStyle, fill);
        parent.AddChild(bar);
        bar.Size = rect.Size; // 应用主题之后再设置，撤销构造时默认主题对 Size 的钳制。
        return bar;
    }
    /// <summary>在 parent 下创建按钮；text 为显示文案，rect 为布局区域，返回值用于绑定点击事件。</summary>
    private static Button AddButton(Control parent, string text, Rect2 rect)
    {
        var button = new Button { Text = text, Position = rect.Position, Size = rect.Size };
        button.AddThemeStyleboxOverride(GodotPropertyNames.NormalStyle, Box(new Color(BattleUiColors.ButtonNormal)));
        button.AddThemeStyleboxOverride(GodotPropertyNames.HoverStyle, Box(new Color(BattleUiColors.ButtonHover)));
        button.AddThemeStyleboxOverride(GodotPropertyNames.PressedStyle, Box(new Color(BattleUiColors.ButtonPressed)));
        parent.AddChild(button); return button;
    }
}
