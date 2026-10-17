using Godot;

/// <summary>武将基础属性，与可交换的主动及被动技能分离。</summary>
[GlobalClass]
public partial class CombatantConfig : Resource
{
    // 角色进入场景时使用的生命上限。
    [Export] public int MaxHealth { get; set; } = 100;
    // 普通移动状态下的速度，单位为场景像素/秒。
    [Export] public float MoveSpeed { get; set; } = 100f;
    // 受到非致命伤害后的不可行动时间，单位为秒。
    [Export] public float HurtDuration { get; set; } = .2f;
    // 受到伤害时的初始击退速度。
    [Export] public float KnockbackSpeed { get; set; } = 160f;
    // 击退速度每秒向零衰减的数值。
    [Export] public float KnockbackDeceleration { get; set; } = 850f;
    // 命中后攻击者与受击者各自冻结的时间，单位为秒。
    [Export] public float HitStopDuration { get; set; } = .035f;
}
