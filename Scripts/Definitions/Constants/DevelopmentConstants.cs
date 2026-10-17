/// <summary>开发工具和自动验证使用的命令行参数。</summary>
public static class DevelopmentArguments
{
    public const string CombatTest = "--combat-test";
    public const string CaptureArena = "--capture-arena";
    public const string CaptureAttack = "--capture-attack";
    public const string CaptureSlash = "--capture-slash";
    public const string CaptureBones = "--capture-bones";
}

/// <summary>自动截图输出位置，均位于不会提交的 Build 目录。</summary>
public static class PreviewPaths
{
    public const string Arena = "res://Build/S1-arena-preview.png";
    public const string Attack = "res://Build/S1-attack-preview.png";
    public const string Slash = "res://Build/S1-slash-preview.png";
    public const string Bones = "res://Build/S1-bones-preview.png";
}
