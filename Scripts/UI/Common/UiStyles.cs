using Godot;

/// <summary>共用界面样式工厂；每次返回独立资源，避免某个面板改样式影响其它界面。</summary>
public static class UiStyles
{
    public static StyleBoxFlat Panel(Color fill, Color edge, int border = 1, int radius = 5,
        float horizontalMargin = -1, float verticalMargin = -1) => new()
    {
        BgColor = fill, BorderColor = edge,
        BorderWidthBottom = border, BorderWidthTop = border, BorderWidthLeft = border, BorderWidthRight = border,
        CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
        ContentMarginLeft = horizontalMargin, ContentMarginRight = horizontalMargin,
        ContentMarginTop = verticalMargin, ContentMarginBottom = verticalMargin
    };

    public static Theme ChineseTheme(int fontSize) => new()
    {
        DefaultFont = new SystemFont { FontNames = GameFontNames.CreateChineseFallbacks() },
        DefaultFontSize = fontSize
    };
}
