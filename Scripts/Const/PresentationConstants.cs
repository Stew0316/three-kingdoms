using Godot;

/// <summary>角色语义动作对应的 AnimationPlayer 动画名称。</summary>
public static class CharacterAnimations
{
    public const string Idle = "待机";
    public const string Move = "行走";
    public const string Attack = "挥击";
    public const string Dash = "突进";
    public const string Hurt = "受击";
    public const string Dead = "倒地";
}

/// <summary>Skeleton2D 中固定的骨骼节点名称。</summary>
public static class CharacterBoneNames
{
    public const int Count = 8;
    public const string Hips = "Hips";
    public const string Chest = "Chest";
    public const string Head = "Head";
    public const string LeftArm = "LeftArm";
    public const string RightArm = "RightArm";
    public const string LeftLeg = "LeftLeg";
    public const string RightLeg = "RightLeg";
    public const string Cape = "Cape";

    // 返回新数组，避免外部修改全局骨骼顺序。
    public static string[] CreateOrderedNames() =>
    [
        Hips, Chest, Head, LeftArm, RightArm, LeftLeg, RightLeg, Cape
    ];
}

/// <summary>CanvasItem 的固定绘制层级。</summary>
public static class PresentationZIndexes
{
    public const int WeaponTrail = 3;
    public const int BoneOverlay = 4;
    public const int CombatFeedback = 10;
}

/// <summary>表现层复用的颜色字符串；使用 RRGGBB 格式。</summary>
public static class PresentationColors
{
    public const string PlayerTeam = "80cfc4";
    public const string EnemyTeam = "da7965";
    public const string PlayerTrail = "ffc66b";
    public const string EnemyTrail = "ff8050";
    public const string BoneDebug = "66f5db";
}
/// <summary>需要保留浮点分量或透明度的固定表现色；Color 不是编译期常量，因此使用只读字段。</summary>
public static class PresentationPalette
{
    public static readonly Color PlayerAttack = new(.95f, .80f, .42f);
    public static readonly Color EnemyAttack = new(1f, .30f, .20f);
    public static readonly Color DeathTint = new(.45f, .43f, .40f, .65f);
    public static readonly Color HurtFlash = new(1.7f, 1.7f, 1.7f);
    public static readonly Color GroundShadow = new(0f, 0f, 0f, .28f);
    public static readonly Color DamageText = new(1f, .89f, .65f);
    public static readonly Color HitSpark = new(1f, .94f, .77f);
}
