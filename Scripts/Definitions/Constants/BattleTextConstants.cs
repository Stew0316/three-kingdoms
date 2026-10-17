/// <summary>当前 S1 原型使用的界面文本。正式多语言接入后应替换为本地化键。</summary>
public static class BattleTexts
{
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
    public const string DebugControls = "红色扇形是敌方预警    |    Esc 暂停    R 重开    F3 状态    F4 骨骼";
    // 以下模板统一约束 HUD 的动态文案，参数顺序由调用处的注释和类型表达。
    public const string BattleClockFormat = "{0} · 对 · {1}   {2:0}s";
    public const string DebugStateFormat = "{0} {1} / {2}     {3} {4} / {5}";
    public const string FinishedSubtitleFormat = "用时 {0:0.0} 秒 · 按 R 再战一局";
    public const string PausedSubtitle = "按 Esc 继续对战";
    public const string HealthFormat = "{0} / {1}";
    public const string CooldownFormat = "{0:0.0}s";
}

/// <summary>S1 HUD 的固定主题色。后续建立 Theme 资源后可迁移到 UI 资源。</summary>
public static class BattleUiColors
{
    public const string Panel = "f8edca";
    public const string PlayerName = "9d4037";
    public const string EnemyName = "34634d";
    public const string Title = "594936";
    public const string SecondaryText = "776853";
    public const string PlayerHealthText = "493e31";
    public const string EnemyHealthText = "493e31";
    public const string PlayerHealth = "df6753";
    public const string EnemyHealth = "62a068";
    public const string SkillText = "4e493d";
    public const string DebugText = "49392c";
    public const string ControlText = "fff3d5";
    public const string HelpText = "e1d6b8";
    public const string OverlayPanel = "f7e8bd";
    public const string OverlayTitle = "65432d";
    public const string OverlayText = "665746";
    public const string BarBackground = "5b5a45";
    public const string ButtonNormal = "77985f";
    public const string ButtonHover = "8eb26f";
    public const string ButtonPressed = "5c7f49";
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
