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
    public const string Skill3 = "skill_3";
    public const string Skill4 = "skill_4";
    public const string Interact = "interact";
    public static readonly string[] SkillSlots = { Skill1, Skill2, Skill3, Skill4 };
    public static readonly string[] ItemSlots = { "item_1", "item_2", "item_3", "item_4" };
    public static readonly CombatAction[] SkillActions = { CombatAction.Dash, CombatAction.Sweep, CombatAction.Skill3, CombatAction.Skill4 };
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
