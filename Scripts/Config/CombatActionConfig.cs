using Godot;

/// <summary>可由 Godot Inspector 编辑的一种战斗动作配置。</summary>
[GlobalClass]
public partial class CombatActionConfig : Resource
{
    [Export] public string SkillId { get; set; } = ""; // 稳定技能标识，用于独立冷却和装备校验。
    [Export] public string DisplayName { get; set; } = "技能"; // HUD 跟随实际装备刷新。
    [Export] public Texture2D Icon { get; set; } // 可选技能图标，属于技能而非武将。
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = ""; // 技能说明，随装备进入HUD。
    [Export] public int HitCount { get; set; } = 1; // 每个目标最多命中的段数。
    [Export] public float HitInterval { get; set; } = .18f; // 多段命中间隔秒数。
    [Export] public float StunSeconds { get; set; } // 命中后额外控制时长。
    [Export] public float SlowFactor { get; set; } = 1; // 移速倍率，1不减速。
    [Export] public float Vulnerability { get; set; } // 目标承受伤害增加比例。
    [Export] public float StatusDuration { get; set; } // 减速、易伤或自身强化持续秒数。
    [Export] public int SelfHeal { get; set; } // 生效时自身治疗量，仅触发一次。
    [Export] public int SelfCost { get; set; } // 生效时生命消耗，最多扣到1。
    [Export] public float DamageMultiplier { get; set; } = 1; // 生效时给予自身的伤害强化倍率。
    [Export] public float Lifesteal { get; set; } // 自身强化期间按实际攻击伤害回复的比例。
    [Export] public CombatMotion Motion { get; set; } // 语义动作请求，由当前武将的动画资源解释。
    // 动作进入有效判定前的准备时间，单位为秒。
    [Export] public float Prepare { get; set; }
    // 动作可造成伤害或位移的有效时间，单位为秒。
    [Export] public float Active { get; set; }
    // 有效阶段结束后恢复行动前的时间，单位为秒。
    [Export] public float Recover { get; set; }
    // 再次使用同一动作前需要等待的时间，单位为秒。
    [Export] public float Cooldown { get; set; }
    // 命中扇形的半径，单位为场景像素。
    [Export] public float Range { get; set; }
    // 命中扇形相对朝向的半角，单位为弧度。
    [Export] public float HalfAngle { get; set; }
    // 动作每次成功命中造成的基础伤害。
    [Export] public int Damage { get; set; }
    // 有效阶段沿施法方向移动的速度，0 表示不产生动作位移。
    [Export] public float Speed { get; set; }

    /// <summary>创建本次施法使用的不可变参数快照，避免运行时修改共享 Resource。</summary>
    public AttackSpec ToSpec() => new(
        Prepare, Active, Recover, Cooldown, Range, HalfAngle, Damage, Speed)
    { HitCount = HitCount, HitInterval = HitInterval, StunSeconds = StunSeconds, SlowFactor = SlowFactor,
        Vulnerability = Vulnerability, StatusDuration = StatusDuration, SelfHeal = SelfHeal, SelfCost = SelfCost,
        DamageMultiplier = DamageMultiplier, Lifesteal = Lifesteal };
}
