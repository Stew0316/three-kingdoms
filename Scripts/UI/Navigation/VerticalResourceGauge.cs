using Godot;
using System.Collections.Generic;

/// <summary>
/// 无底板的竖向资源器物：血量使用剑尖朝上的剑刃，未启用的内力使用暗色酒葫芦。
/// 以 32×54 逻辑像素绘制；改变控件尺寸时保持器物比例并居中。
/// </summary>
public partial class VerticalResourceGauge : Control
{
    private bool _isMana;
    private float _ratio = 1;

    /// <summary>为 true 时绘制酒葫芦；当前游戏尚无内力数值，因此不绘制虚构水位。</summary>
    public bool IsMana
    {
        get => _isMana;
        set { if (_isMana == value) return; _isMana = value; QueueRedraw(); }
    }

    /// <summary>真实剩余血量比例，限制在 0–1；红芯从剑格向剑尖填充。</summary>
    public float Ratio
    {
        get => _ratio;
        set
        {
            float next = Mathf.Clamp(value, 0, 1);
            if (_ratio == next) return;
            _ratio = next;
            QueueRedraw();
        }
    }

    /// <summary>供父界面组织提示文字的真实数值；器物上不叠加长文本。</summary>
    public string ValueText { get; set; } = "";

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        CustomMinimumSize = new Vector2(32, 54);
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        float scale = Mathf.Min(Size.X / 32, Size.Y / 54);
        if (scale <= 0) return;
        DrawSetTransform((Size - new Vector2(32, 54) * scale) / 2, 0, Vector2.One * scale);
        if (IsMana) DrawGourd(); else DrawSword();
    }

    private void DrawSword()
    {
        // 刃缘、剑脊和铜剑格都属于器物自身，不在外侧增加矩形底色或框线。
        Vector2[] blade = { new(16, 1.5f), new(22, 10), new(21.1f, 39.5f),
            new(10.9f, 39.5f), new(10, 10) };
        Shape(blade, new Color("3f4748"), new Color("171c1d"), 1.1f);
        DrawColoredPolygon(new Vector2[] { new(16, 2.4f), new(16, 39), new(11.5f, 39), new(10.8f, 10.2f) },
            new Color("89948b"));
        DrawColoredPolygon(new Vector2[] { new(16, 2.4f), new(21.2f, 10.2f), new(20.5f, 39), new(16, 39) },
            new Color("4e5b5b"));
        DrawPolyline(new Vector2[] { new(15.6f, 3), new(11.1f, 10.3f), new(11.7f, 37.8f) },
            new Color("d2d0ae"), .65f, true);
        DrawLine(new(21, 10.8f), new(20.2f, 37.8f), new Color("a1aaa0"), .55f, true);

        Vector2[] core = { new(16, 5.3f), new(19.6f, 11), new(19, 38.3f),
            new(13, 38.3f), new(12.4f, 11) };
        DrawColoredPolygon(core, new Color("293333"));
        if (Ratio > 0)
        {
            float level = Mathf.Lerp(38.3f, 5.3f, Ratio);
            Vector2[] filled = ClipBelow(core, level);
            if (filled.Length >= 3)
            {
                DrawColoredPolygon(filled, new Color("a93e32"));
                Vector2[] lit = ClipBelow(new Vector2[] { new(16, 5.3f), new(16.2f, 38.3f),
                    new(13, 38.3f), new(12.4f, 11) }, level);
                if (lit.Length >= 3) DrawColoredPolygon(lit, new Color("d2674b"));
                // 只沿实际裁切出的水位补一条短高光，半血和低血都能直观看出高度。
                if (Ratio < .99f)
                {
                    float left = 32, right = 0;
                    foreach (Vector2 point in filled)
                        if (Mathf.Abs(point.Y - level) < .01f)
                        { left = Mathf.Min(left, point.X); right = Mathf.Max(right, point.X); }
                    if (right > left) DrawLine(new(left, level), new(right, level), new Color("eeac79"), .6f, true);
                }
            }
        }

        Vector2[] guard = { new(5, 37.8f), new(11.3f, 39), new(20.7f, 39), new(27, 37.8f),
            new(25.8f, 41.4f), new(20.5f, 42.5f), new(11.5f, 42.5f), new(6.2f, 41.4f) };
        Shape(guard, new Color("886c3e"), new Color("302c23"), .8f);
        DrawPolyline(new Vector2[] { new(6.1f, 38.5f), new(11.5f, 39.9f), new(20.5f, 39.9f), new(25.9f, 38.5f) },
            new Color("c1a572"), .75f, true);
        Shape(new Vector2[] { new(13.6f, 42.3f), new(18.4f, 42.3f), new(18, 49.4f), new(14, 49.4f) },
            new Color("45362a"), new Color("24251f"), .7f);
        for (int i = 0; i < 3; i++)
            DrawLine(new(14.3f, 43.5f + i * 1.8f), new(17.8f, 45 + i * 1.8f), new Color("9c8354"), .7f, true);
        DrawCircle(new(16, 51), 2.5f, new Color("8d7245"), true, -1, true);
        DrawArc(new(16, 51), 2.5f, 0, Mathf.Tau, 20, new Color("292820"), .7f, true);
        DrawArc(new(16, 51), 1.5f, Mathf.Pi, Mathf.Tau, 12, new Color("c1a572"), .65f, true);
    }

    private void DrawGourd()
    {
        // 上肚较小、腰部收紧、下肚饱满；绳结实际绕过细腰，木塞与颈口连接。
        var body = new List<Vector2> { new(13.2f, 7), new(18.8f, 7), new(19, 10.5f) };
        Curve(body, new(25, 13), new(25.2f, 20.2f), new(20.1f, 24.5f));
        Curve(body, new(18.3f, 26.1f), new(18.8f, 28), new(22.6f, 30.4f));
        Curve(body, new(31.2f, 36), new(28.5f, 50.8f), new(16, 51.8f));
        Curve(body, new(3.5f, 50.8f), new(.8f, 36), new(9.4f, 30.4f));
        Curve(body, new(13.2f, 28), new(13.7f, 26.1f), new(11.9f, 24.5f));
        Curve(body, new(6.8f, 20.2f), new(7, 13), new(13, 10.5f));
        Shape(body.ToArray(), new Color("785735"), new Color("30281f"), 1);

        // 暗蓝内胆没有亮蓝满格，也没有动态水位；短横明确表示该资源尚未启用。
        var inner = new List<Vector2> { new(14.2f, 10), new(17.8f, 10), new(17.8f, 12) };
        Curve(inner, new(23, 14), new(22.8f, 20), new(18.7f, 23.3f));
        Curve(inner, new(16.5f, 25.3f), new(17.2f, 29.3f), new(21.3f, 32));
        Curve(inner, new(28.6f, 37.1f), new(25.4f, 48.7f), new(16, 49.5f));
        Curve(inner, new(6.6f, 48.7f), new(3.4f, 37.1f), new(10.7f, 32));
        Curve(inner, new(14.8f, 29.3f), new(15.5f, 25.3f), new(13.3f, 23.3f));
        Curve(inner, new(9.2f, 20), new(9, 14), new(14.2f, 12));
        DrawColoredPolygon(inner.ToArray(), new Color("20333c"));
        DrawArc(new(16, 40), 8.2f, 1.12f, 2.24f, 18, new Color("354b51"), 1.3f, true);
        DrawPolyline(new Vector2[] { new(10.4f, 15), new(9.6f, 17.5f), new(10, 20.1f) },
            new Color("b19561"), .8f, true);
        DrawPolyline(new Vector2[] { new(7.7f, 34.5f), new(5.8f, 38), new(6.1f, 42.5f) },
            new Color("b19561"), .8f, true);
        DrawLine(new(13.5f, 39.7f), new(18.5f, 39.7f), new Color("8b9c99"), 1.15f, true);

        Shape(new Vector2[] { new(13.1f, 3.4f), new(18.8f, 3.4f), new(18.2f, 8.2f), new(13.8f, 8.2f) },
            new Color("a38450"), new Color("443725"), .65f);
        DrawLine(new(13.3f, 4), new(18.5f, 4), new Color("d2b57a"), .7f, true);
        DrawLine(new(14.6f, 4.9f), new(14.9f, 7.7f), new Color("715332"), .55f, true);
        DrawPolyline(new Vector2[] { new(12.8f, 9), new(15.9f, 9.7f), new(19.2f, 9) },
            new Color("b99b63"), 1.2f, true);
        DrawPolyline(new Vector2[] { new(11.8f, 26), new(15.8f, 27), new(20.3f, 26) },
            new Color("30291f"), 2.1f, true);
        DrawPolyline(new Vector2[] { new(11.8f, 25.7f), new(15.8f, 26.6f), new(20.3f, 25.7f) },
            new Color("b28d58"), .75f, true);
        DrawCircle(new(20.5f, 26.2f), 1.15f, new Color("927047"), true, -1, true);
        DrawPolyline(new Vector2[] { new(21.1f, 26.6f), new(23.9f, 28.7f), new(24.4f, 32.2f) },
            new Color("ad8c57"), .85f, true);
        DrawPolyline(new Vector2[] { new(20.6f, 27), new(21.2f, 30.2f), new(22.4f, 31.4f) },
            new Color("695134"), 1, true);
    }

    private void Shape(Vector2[] points, Color fill, Color edge, float edgeWidth)
    {
        DrawColoredPolygon(points, fill);
        var closed = new Vector2[points.Length + 1];
        points.CopyTo(closed, 0);
        closed[^1] = points[0];
        DrawPolyline(closed, edge, edgeWidth, true);
    }

    /// <summary>按水平血量面裁切轮廓，保留面以下部分；零血量由调用方直接跳过填色。</summary>
    private static Vector2[] ClipBelow(Vector2[] polygon, float level)
    {
        var result = new List<Vector2>();
        Vector2 previous = polygon[^1];
        foreach (Vector2 current in polygon)
        {
            bool wasInside = previous.Y >= level, inside = current.Y >= level;
            if (wasInside != inside)
                result.Add(previous.Lerp(current, (level - previous.Y) / (current.Y - previous.Y)));
            if (inside) result.Add(current);
            previous = current;
        }
        return result.ToArray();
    }

    /// <summary>把器物的三次曲线细分为小段，保留圆润肚形并避免把葫芦画成折线药瓶。</summary>
    private static void Curve(List<Vector2> points, Vector2 first, Vector2 second, Vector2 end)
    {
        Vector2 start = points[^1];
        for (int i = 1; i <= 10; i++)
        {
            float t = i / 10f, inverse = 1 - t;
            points.Add(inverse * inverse * inverse * start + 3 * inverse * inverse * t * first
                + 3 * inverse * t * t * second + t * t * t * end);
        }
    }
}
