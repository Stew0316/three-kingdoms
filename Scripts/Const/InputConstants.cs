using Godot;

/// <summary>project.godot Input Map 中注册的动作名称。</summary>
public static class InputActions
{
    public const string MoveLeft = "move_left";
    public const string MoveRight = "move_right";
    public const string MoveUp = "move_up";
    public const string MoveDown = "move_down";
    public const string Attack = "attack";
    public const string Skill1 = "skill_1";
    public const string Skill2 = "skill_2";
    public const string Dodge = "dodge";
    public const string Pause = "pause_game";
    public const string Restart = "restart_battle";
    // 旧木桩调试场景使用的回血动作；保留到旧场景迁移完成。
    public const string RestoreHealth = "health";
}

/// <summary>尚未进入 Input Map、只用于开发调试的固定按键。</summary>
public static class DebugKeys
{
    public const Key ToggleState = Key.F3;
    public const Key ToggleBones = Key.F4;
}
