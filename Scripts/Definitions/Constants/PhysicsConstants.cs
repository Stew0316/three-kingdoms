/// <summary>project.godot 物理层的位掩码。层号从 1 开始，位掩码从最低位开始。</summary>
public static class PhysicsLayers
{
    public const uint None = 0u;
    public const uint WorldBody = 1u << 0;
    public const uint PlayerHurtbox = 1u << 1;
    public const uint PlayerBody = 1u << 2;
    public const uint EnemyHurtbox = 1u << 3;
    public const uint EnemyBody = 1u << 4;

    // 玩家身体与世界和敌方身体发生阻挡。
    public const uint PlayerBodyMask = WorldBody | EnemyBody;
    // 敌方身体与世界和玩家身体发生阻挡。
    public const uint EnemyBodyMask = WorldBody | PlayerBody;
}

/// <summary>节点处理顺序的固定优先级；数值越小越早执行。</summary>
public static class ProcessPriorities
{
    // 控制器先提交意图，角色随后在同一物理帧执行。
    public const int Controller = -10;
}
