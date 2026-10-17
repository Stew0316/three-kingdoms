using Godot;
using System.Collections.Generic;

/// <summary>16 关节业务骨架的静态约定；仅为旧资源提供默认绑定，正式武将由模型配置覆盖。</summary>
internal static class CharacterRigDefaults
{
    public const int JointCount = 16;

    // 业务序号保持稳定，不能使用 Skeleton2D 的树序索引替换。
    public static IReadOnlyList<string> JointNames { get; } = System.Array.AsReadOnly(new[]
    {
        "腰", "胸", "头", "远上臂", "远前臂", "远手", "近上臂", "近前臂", "近手",
        "远大腿", "远小腿", "近大腿", "近小腿", "披风", "武器", "冠翎"
    });

    /// <summary>返回独立父链，参考姿态可解除本实例武器与近手的父子关系。</summary>
    public static int[] CreateParents() => new[] { -1, 0, 1, 1, 3, 4, 1, 6, 7, 0, 9, 0, 11, 1, 8, 2 };

    /// <summary>返回独立初始关节坐标，供体型校准和模型绑定覆盖。</summary>
    public static Vector2[] CreateJointPositions() => new Vector2[]
    {
        new(0, -27), new(0, -15), new(1, -13), new(-9, -7), new(0, 14), new(0, 14),
        new(9, -6), new(0, 12), new(0, 12), new(-7, 3), new(0, 12), new(7, 3),
        new(0, 12), new(-5, -10), new(0, 1), new(0, -12)
    };

    // 图集格序与业务骨骼顺序不同；采样和绑定都使用同一映射。
    public static IReadOnlyList<int> AtlasTiles { get; } = System.Array.AsReadOnly(new[]
        { 2, 1, 0, 4, 5, 12, 6, 7, 13, 8, 9, 10, 11, 3, 14, 15 });

    /// <summary>返回独立拆件矩形，允许模型和比例校准覆盖显示范围。</summary>
    public static Rect2[] CreatePartRectangles() => new Rect2[]
    {
        new(-11, -3, 22, 17), new(-11, -13, 22, 23), new(-7, -16, 14, 19),
        new(-5, -3, 10, 16), new(-4, -3, 8, 16), new(-4, -3, 8, 8),
        new(-6, -3, 12, 16), new(-4, -3, 8, 16), new(-4, -3, 8, 8),
        new(-5, -3, 10, 17), new(-5, -2, 11, 16), new(-5, -3, 10, 17),
        new(-5, -2, 11, 16), new(-16, -2, 26, 42), new(-6, -54, 12, 87), new(-7, -27, 19, 30)
    };

    /// <summary>返回从后至前的独立绘制顺序，模型可调整遮挡而不改变父链。</summary>
    public static int[] CreateDrawOrder() => new[] { 13, 9, 10, 11, 12, 0, 1, 3, 4, 2, 15, 6, 7, 14, 5, 8 };
}
