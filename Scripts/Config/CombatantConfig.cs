using Godot;

/// <summary>一个角色原型的可调战斗数据；运行时按只读配置使用。</summary>
[GlobalClass]
public partial class CombatantConfig : Resource
{
    // 可组合的被动定义；每个单位的随机状态在运行时结算器中保存。
    [Export] public Godot.Collections.Array<PassiveConfig> Passives { get; set; } = new();
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
    // 普攻的可编辑数值配置。
    [Export] public CombatActionConfig Basic { get; set; }
    // 突进攻击的可编辑数值配置。
    [Export] public CombatActionConfig Dash { get; set; }
    // 横扫攻击的可编辑数值配置。
    [Export] public CombatActionConfig Sweep { get; set; }
    // 闪避位移的可编辑数值配置。
    [Export] public CombatActionConfig Dodge { get; set; }

    /// <summary>获取 action 对应的共享动作配置；调用方应立即转换为运行时快照。</summary>
    public CombatActionConfig GetAction(CombatAction action) => action switch
    {
        CombatAction.Dash => Dash,
        CombatAction.Sweep => Sweep,
        CombatAction.Dodge => Dodge,
        _ => Basic
    };
}
