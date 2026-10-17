using Godot;
using System.Linq;

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
    private Label _playerEffects, _enemyEffects; // 展示仍在生效的控制与强化，时间来自本单位运行时状态。
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
    private Control _roster;
    private bool _pausedBeforeRoster;

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
        var header = new Control { Visible = !_arena.IsGroupPractice, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(header);
        AddPanel(header, new Rect2(16, 10, 608, 53), new Color(BattleUiColors.Panel));
        AddLabel(header, configuredPlayer.Ui.DisplayName, new Rect2(29, 16, 110, 22), 18, configuredPlayer.Ui.NameColor);
        AddLabel(header, _arena.IsSoloPractice ? "单人练习" : configuredEnemy.Ui.DisplayName, new Rect2(501, 16, 110, 22), 18, configuredEnemy.Ui.NameColor, HorizontalAlignment.Right);
        AddLabel(header, _arena.IsSoloPractice ? configuredPlayer.Ui.DisplayName + "步态练习" : BattleTexts.ArenaTitle, new Rect2(230, 15, 180, 23), 18, new Color(BattleUiColors.Title), HorizontalAlignment.Center);
        _clock = AddLabel(header, string.Empty, new Rect2(268, 41, 104, 18), 10, new Color(BattleUiColors.SecondaryText), HorizontalAlignment.Center);
        _playerHealth = AddLabel(header, string.Empty, new Rect2(100, 21, 119, 18), 10, new Color(BattleUiColors.PlayerHealthText), HorizontalAlignment.Right);
        _enemyHealth = AddLabel(header, string.Empty, new Rect2(421, 21, 99, 18), 10, new Color(BattleUiColors.EnemyHealthText));
        _playerBar = AddBar(header, new Rect2(29, 45, 190, 6), configuredPlayer.Ui.HealthColor);
        _enemyBar = AddBar(header, new Rect2(421, 45, 190, 6), configuredEnemy.Ui.HealthColor);
        _enemyBar.Visible = _enemyHealth.Visible = !_arena.IsSoloPractice;
        if (_arena.IsGroupPractice) BuildGroupHeader(root);
        AddPanel(root, new Rect2(16, 66, 529, 20), new Color(BattleUiColors.Panel));
        _skills = AddLabel(root, string.Empty, new Rect2(20, 67, 520, 17), 11, new Color(BattleUiColors.SkillText), HorizontalAlignment.Center);
        _state = AddLabel(root, string.Empty, new Rect2(26, 100, 550, 18), 10, new Color(BattleUiColors.DebugText));
        _playerEffects = AddLabel(root, string.Empty, new Rect2(29, 87, 260, 16), 10, configuredPlayer.Ui.DetailColor);
        _enemyEffects = AddLabel(root, string.Empty, new Rect2(326, 87, 285, 16), 10, configuredEnemy.Ui.DetailColor, HorizontalAlignment.Right);
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
        var choose = AddButton(root, "选武将", new Rect2(552, 66, 72, 23));
        choose.Pressed += () => { _pausedBeforeRoster = GetTree().Paused; GetTree().Paused = true; _roster.Show(); };
        BuildRoster(root);
        if (AdventureSession.Instance?.InCityTrial == true)
        {
            choose.Hide();
            var back = AddButton(root, "返回槐安", new Rect2(552, 66, 72, 23));
            back.Pressed += () => AdventureSession.Instance.ReturnToCity(GetTree(), _arena.Player);
            var resultBack = AddButton(_overlay, "返回城池 / 交任务", new Rect2(230, 270, 180, 28));
            resultBack.Pressed += () => AdventureSession.Instance.ReturnToCity(GetTree(), _arena.Player);
        }
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
                PassiveConfig.EffectKind.EvasionCritical => $"闪避{passive.EvasionChance:P0} · 暴击{passive.Chance:P0}×{passive.CriticalMultiplier:0.0}",
                PassiveConfig.EffectKind.Lifesteal => $"回复{passive.LifestealRatio:P0}",
                _ => $"{passive.Chance:P0}×{passive.CriticalMultiplier:0.0}"
            }));
        return string.Join(" · ",parts);
    }

    /// <summary>处理尚未被其他控件消费的 input；重开前先消费事件，避免访问已离树的 HUD。</summary>
    public override void _UnhandledInput(InputEvent input)
    {
        if (_roster?.Visible == true)
        {
            if (input.IsActionPressed(InputActions.Pause)) { _roster.Hide(); GetTree().Paused = _pausedBeforeRoster; }
            GetViewport().SetInputAsHandled();
            return;
        }
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
            foreach (var actor in _arena.Fighters)
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
        _enemyPassives.Text = _arena.IsSoloPractice ? "对手已停用 · 自由移动观察步态" : _arena.IsGroupPractice
            ? $"四将同场 · 剩余对手 {_arena.Enemies.Count(actor => !actor.IsDead)}/3 · {_arena.BattleSeconds:0}s"
            : DescribePassives(configuredEnemy);
        _playerPassives.TooltipText = _playerPassives.Text;
        _enemyPassives.TooltipText = _arena.IsGroupPractice
            ? string.Join("\n", _arena.Enemies.Select(actor => actor.Ui.DisplayName + "：" + DescribePassives(actor))) : _enemyPassives.Text;
        _playerEffects.Text = DescribeEffects(configuredPlayer);
        _enemyEffects.Text = _arena.IsSoloPractice ? "" : _arena.IsGroupPractice
            ? string.Join(" · ", _arena.Enemies.Where(actor => DescribeEffects(actor) != "").Select(actor => actor.Ui.DisplayName + " " + DescribeEffects(actor)))
            : DescribeEffects(configuredEnemy);
        _enemyEffects.ClipText = true;
        _controls.Text = $"WASD / 方向键 移动    J / 左键 {SkillName(CombatAction.Basic)}    K {SkillName(CombatAction.Dash)}    L {SkillName(CombatAction.Sweep)}    空格 {SkillName(CombatAction.Dodge)}";
        _state.Visible = _showDebug;
        _state.Text = _arena.IsSoloPractice ? $"{configuredPlayer.Ui.DisplayName}：{_arena.Player.Phase}　实际速度 {_arena.Player.WalkVelocity.Length():0}　步态 {_arena.Player.Rig.WalkPhase:0.00}" : string.Format(BattleTexts.DebugStateFormat,
            configuredPlayer.Ui.DisplayName, _arena.Player.CurrentState, _arena.Player.Phase,
            configuredEnemy.Ui.DisplayName, _arena.Enemy.CurrentState,
            _arena.Enemy.GetNode<WeiYanController>(NodeNames.Controller).Decision);
        bool paused = GetTree().Paused;
        _overlay.Visible = (_arena.Finished || paused) && !_roster.Visible;
        if (!_overlay.Visible) return;
        _result.Text = _arena.Finished ? _arena.Result : BattleTexts.PauseTitle;
        _subtitle.Text = _arena.Finished
            ? string.Format(BattleTexts.FinishedSubtitleFormat, _arena.BattleSeconds)
            : BattleTexts.PausedSubtitle;
        _resume.Visible = !_arena.Finished;
    }

    /// <summary>四张独立生命卡共用样式，每张订阅对应单位，不将三名对手的血量合并。</summary>
    private void BuildGroupHeader(Control root)
    {
        for (int index = 0; index < _arena.Fighters.Count; index++)
        {
            var actor = _arena.Fighters[index]; float x = 16 + index * 154;
            AddPanel(root, new Rect2(x, 10, 146, 53), new Color(BattleUiColors.Panel));
            AddLabel(root, actor.Ui.DisplayName + (actor.IsEnemy ? "" : " · 你"), new Rect2(x + 9, 14, 128, 22), 15, actor.Ui.NameColor);
            var text = AddLabel(root, "", new Rect2(x + 9, 34, 128, 15), 10, actor.Ui.DetailColor);
            var bar = AddBar(root, new Rect2(x + 9, 53, 128, 5), actor.Ui.HealthColor);
            void Update(int current, int max) { text.Text = current == 0 ? "已击败" : $"{current} / {max}"; bar.MaxValue = max; bar.Value = current; }
            actor.Health.Damaged += Update; Update(actor.Health.CurrentHealth, actor.Health.MaxHealth);
        }
    }

    private string SkillName(CombatAction slot) => _arena.Player.GetSkill(slot)?.DisplayName ?? "空槽";
    /// <summary>选择玩家和对手后重建整场，身份资料、模型、动画与技能同时来自新武将资源。</summary>
    private void BuildRoster(Control root)
    {
        _roster = new Control { Name = "武将选择", Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _roster.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_roster);
        var dim = new ColorRect { Color = new Color(0, 0, 0, .7f) }; _roster.AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddPanel(_roster, new Rect2(90, 73, 460, 230), new Color(BattleUiColors.OverlayPanel));
        AddLabel(_roster, "选择武将", new Rect2(110, 86, 420, 28), 22, new Color(BattleUiColors.OverlayTitle), HorizontalAlignment.Center);
        AddLabel(_roster, "我方", new Rect2(114, 128, 50, 22), 14, new Color(BattleUiColors.OverlayText));
        AddLabel(_roster, _arena.IsGroupPractice ? "首位对手" : "对手", new Rect2(340, 128, 150, 22), 14, new Color(BattleUiColors.OverlayText));
        OptionButton Pick(int x, int selected)
        {
            var select = new OptionButton { Position = new Vector2(x, 152), Size = new Vector2(190, 30) };
            foreach (string name in HeroCatalog.Names) select.AddItem(name);
            select.Select(selected); _roster.AddChild(select); return select;
        }
        var player = Pick(114, HeroCatalog.PlayerIndex); var enemy = Pick(336, HeroCatalog.EnemyIndex);
        var detail = AddLabel(_roster, "", new Rect2(110, 190, 420, 43), 11, new Color(BattleUiColors.OverlayText), HorizontalAlignment.Center);
        detail.MouseFilter = Control.MouseFilterEnum.Pass; // 允许悬停阅读完整技能效果。
        void Describe()
        {
            var loadout = HeroCatalog.LoadDefaultLoadout(player.Selected);
            detail.Text = $"主动：{loadout.Dash.DisplayName} / {loadout.Sweep.DisplayName}\n被动：{loadout.Passives[0].DisplayName}（普攻与空格闪避为基础动作）";
            detail.TooltipText = loadout.Dash.Description + "\n" + loadout.Sweep.Description + "\n" + loadout.Passives[0].Description;
        }
        player.ItemSelected += _ => { if (_arena.IsGroupPractice && enemy.Selected == player.Selected) enemy.Select((player.Selected + 1) % HeroCatalog.Ids.Length); Describe(); };
        enemy.ItemSelected += _ => { if (_arena.IsGroupPractice && enemy.Selected == player.Selected) enemy.Select((player.Selected + 1) % HeroCatalog.Ids.Length); };
        Describe();
        if (_arena.IsGroupPractice)
            AddLabel(_roster, "其余两将自动加入 · 吕布1200，其余1000生命", new Rect2(110, 230, 420, 17), 10, new Color(BattleUiColors.OverlayText), HorizontalAlignment.Center);
        var start = AddButton(_roster, "开始对战", new Rect2(336, 251, 130, 30));
        start.Pressed += () => { HeroCatalog.PlayerIndex = player.Selected; HeroCatalog.EnemyIndex = enemy.Selected; Restart(); };
        var cancel = AddButton(_roster, "返回", new Rect2(174, 251, 130, 30));
        cancel.Pressed += () => { _roster.Hide(); GetTree().Paused = _pausedBeforeRoster; };
    }
    private string SkillText(CombatAction slot) => $"{SkillName(slot)} {ReadyText(slot)}";

    private static string DescribeEffects(Combatant actor)
    {
        var status = actor.Status;
        var effects = new System.Collections.Generic.List<string>();
        if (status.StunRemaining > 0) effects.Add($"眩晕 {status.StunRemaining:0.0}s");
        if (status.SlowRemaining > 0) effects.Add($"减速 {status.SlowRemaining:0.0}s");
        if (status.VulnerableRemaining > 0) effects.Add($"易伤 {status.VulnerableRemaining:0.0}s");
        if (status.EmpowerRemaining > 0) effects.Add($"战意 {status.EmpowerRemaining:0.0}s");
        return string.Join(" · ", effects);
    }

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
    private static StyleBoxFlat Box(Color color) => UiStyles.Panel(color, new Color("5c6849"), border: 2);
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
