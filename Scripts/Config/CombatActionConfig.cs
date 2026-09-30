using Godot;

/// <summary>可由 Godot Inspector 编辑的一种战斗动作配置。</summary>
[GlobalClass]
public partial class CombatActionConfig : Resource
{
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
        Prepare, Active, Recover, Cooldown, Range, HalfAngle, Damage, Speed);
}
