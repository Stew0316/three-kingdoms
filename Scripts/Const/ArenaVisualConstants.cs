using Godot;

/// <summary>演武场静态绘制使用的固定颜色与随机种子。</summary>
public static class ArenaVisualConstants
{
    // 固定种子保证每次启动时石板明暗分布一致，便于截图与回归测试。
    public const ulong StonePatternSeed = 20260928;
    public const string Background = "172023";
    public const string OuterFloor = "343a35";
    public const string InnerFloor = "786d53";
    public const string Border = "464c46";
    public const string BorderHighlight = "9a8c65";
    public const string SideWall = "4b5149";
    public const string BannerPole = "aa9666";
    public const string Banner = "713f35";

    public static readonly Color RingPrimary = new(.78f, .70f, .48f, .23f);
    public static readonly Color RingSecondary = new(.78f, .70f, .48f, .18f);
    public static readonly Color RingCross = new(.8f, .72f, .5f, .25f);
}
