using Godot;

/// <summary>演武场静态绘制使用的固定颜色与随机种子。</summary>
public static class ArenaVisualConstants
{
    // 固定种子保证每次启动时石板明暗分布一致，便于截图与回归测试。
    public const ulong StonePatternSeed = 20260928;
    public const string GrassDark = "6fa84f";
    public const string GrassBlade = "4d883e";
    public const string WallShadow = "456747";
    public const string CourtyardEdge = "a88d58";
    public const string Courtyard = "c6b27b";
    public const string TileJoint = "a99768";
    public const string RoofDark = "71443c";
    public const string Roof = "a95545";
    public const string RoofHighlight = "df8b62";
    public const string GateDark = "55382f";
    public const string Gate = "7d563b";
    public const string BushDark = "376c3c";
    public const string Bush = "55964b";
    public const string BushHighlight = "91c968";
    public const string BannerPole = "72533b";
    public const string PlayerBanner = "b94b45";
    public const string EnemyBanner = "3f7961";

    public static readonly Color RingPrimary = new(.92f,.82f,.52f,.72f);
    public static readonly Color RingSecondary = new(.50f,.38f,.20f,.45f);
    public static readonly Color RingCross = new(.57f,.43f,.23f,.42f);
}
