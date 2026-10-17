using Godot;

/// <summary>固定两节骨长的局部角度求解，供角色手臂、腿部和回退动作共用。</summary>
internal static class TwoBoneIk2D
{
    /// <summary>Root 是朝下为零轴的根骨角，Joint 是第二节相对第一节的弯曲角。</summary>
    public readonly record struct Angles(float Root, float Joint);

    /// <summary>
    /// 根据根部到目标的局部向量计算关节角，不修改骨骼节点。
    /// bend 选择弯曲方向；minimumDistance 保留旧资源的最短距离约定。
    /// </summary>
    public static Angles Solve(Vector2 offset, float firstLength, float secondLength,
        float bend, float minimumDistance = -1)
    {
        float minimum = minimumDistance >= 0 ? minimumDistance : Mathf.Abs(firstLength - secondLength) + .01f;
        float distance = Mathf.Clamp(offset.Length(), minimum, firstLength + secondLength - .01f);
        float joint = bend * Mathf.Acos(Mathf.Clamp((distance * distance - firstLength * firstLength - secondLength * secondLength)
            / (2 * firstLength * secondLength), -1, 1));
        float root = offset.Angle() - Mathf.Pi / 2
            - Mathf.Atan2(secondLength * Mathf.Sin(joint), firstLength + secondLength * Mathf.Cos(joint));
        return new(root, joint);
    }
}
