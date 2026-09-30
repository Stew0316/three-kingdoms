/// <summary>当前 S1 原型使用的界面文本。正式多语言接入后应替换为本地化键。</summary>
public static class BattleTexts
{
    public const string PlayerName = "吕布";
    public const string EnemyName = "魏延";
    public const string ArenaTitle = "演 武 场";
    public const string Victory = "挑战成功";
    public const string Defeat = "胜败乃兵家常事";
    public const string PauseTitle = "稍作休整";
    public const string Resume = "继续";
    public const string Restart = "重新挑战 · R";
    public const string Ready = "就绪";
    public const string Stop = "停止";
    public const string Observe = "观察";
    public const string Chase = "接近";
    public const string BasicAttack = "普攻";
    public const string Dash = "突进";
    public const string Sweep = "横扫";
    public const string Charging = "蓄力";
    public const string Active = "出招";
    public const string Recovering = "收招";
    public const string HurtState = "受击";
    public const string DeadState = "阵亡";
    public const string MoveState = "移动";
    public const string IdleState = "待机";
    public const string Controls = "WASD / 方向键 移动    J / 左键 普攻    K 突进    L 横扫    空格 闪避";
    public const string DebugControls = "红色扇形是敌方预警    |    Esc 暂停    R 重开    F3 状态    F4 骨骼";
    // 以下模板统一约束 HUD 的动态文案，参数顺序由调用处的注释和类型表达。
    public const string BattleClockFormat = "{0} · 对 · {1}   {2:0}s";
    public const string SkillStatusFormat = "K 突进 {0}     L 横扫 {1}     空格 闪避 {2}";
    public const string DebugStateFormat = "{0} {1} / {2}     {3} {4} / {5}";
    public const string FinishedSubtitleFormat = "用时 {0:0.0} 秒 · 按 R 再战一局";
    public const string PausedSubtitle = "按 Esc 继续对战";
    public const string HealthFormat = "{0} / {1}";
    public const string CooldownFormat = "{0:0.0}s";
}

/// <summary>S1 HUD 的固定主题色。后续建立 Theme 资源后可迁移到 UI 资源。</summary>
public static class BattleUiColors
{
    public const string Panel = "1d282b";
    public const string PlayerName = "a7ded2";
    public const string EnemyName = "e1a48c";
    public const string Title = "e0ca93";
    public const string SecondaryText = "a5b2ad";
    public const string PlayerHealthText = "c5d1c8";
    public const string EnemyHealthText = "d4c6b5";
    public const string PlayerHealth = "70b8a7";
    public const string EnemyHealth = "c87960";
    public const string SkillText = "ded0ac";
    public const string DebugText = "f3dfb4";
    public const string ControlText = "d8cfb6";
    public const string HelpText = "9ba9a1";
    public const string OverlayPanel = "202c2e";
    public const string OverlayTitle = "e4cb8f";
    public const string OverlayText = "bdc9bd";
    public const string BarBackground = "111b1e";
    public const string ButtonNormal = "48534a";
    public const string ButtonHover = "65715e";
    public const string ButtonPressed = "343f38";
}

/// <summary>系统字体候选名称，按顺序尝试匹配。</summary>
public static class GameFontNames
{
    public const string MicrosoftYaHeiUi = "Microsoft YaHei UI";
    public const string MicrosoftYaHei = "Microsoft YaHei";
    public const string NotoSansCjkSc = "Noto Sans CJK SC";

    // SystemFont 需要数组；每次创建独立数组，避免共享集合被修改。
    public static string[] CreateChineseFallbacks() =>
    [
        MicrosoftYaHeiUi, MicrosoftYaHei, NotoSansCjkSc
    ];
}
