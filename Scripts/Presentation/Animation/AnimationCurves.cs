using Godot;

/// <summary>表现层共用的连续插值；只计算曲线，不持有动作时钟或修改游戏状态。</summary>
internal static class AnimationCurves
{
    /// <summary>将权重限制在 0～1，起点和终点速度为零。</summary>
    public static float Smooth(float value)
    {
        float weight = Mathf.Clamp(value, 0, 1);
        return weight * weight * (3 - 2 * weight);
    }

    /// <summary>标量 Catmull-Rom 段，马匹躯干和颈部用它连接周期关键姿态。</summary>
    public static float CatmullRom(float a, float b, float c, float d, float weight)
    {
        float squared = weight * weight, cubed = squared * weight;
        return .5f * (2 * b + (-a + c) * weight + (2 * a - 5 * b + 4 * c - d) * squared
            + (-a + 3 * b - 3 * c + d) * cubed);
    }

    /// <summary>二维 Catmull-Rom 段，使用同一曲线规则连接形体轮廓点。</summary>
    public static Vector2 CatmullRom(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float weight)
    {
        float squared = weight * weight, cubed = squared * weight;
        return .5f * (2 * b + (-a + c) * weight + (2 * a - 5 * b + 4 * c - d) * squared
            + (-a + 3 * b - 3 * c + d) * cubed);
    }
}
