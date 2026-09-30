using Godot;
using System.Collections.Generic;

/// <summary>攻击候选重叠区域；根据动作范围、方向和已命中集合决定是否提交伤害。</summary>
public partial class Hitbox : Area2D
{
    // 本次施法已命中的目标实例 ID；按角色去重，避免多个受击框重复扣血。
    private readonly HashSet<ulong> _hitTargets = new();
    // 发起攻击的宿主角色，提供阵营和生效窗口。
    private Combatant _owner;
    // 当前实例独占的圆形候选区域，半径随动作配置更新。
    private CircleShape2D _shape;

    /// <summary>绑定宿主并配置只检测对方受击层的候选区域。</summary>
    public override void _Ready()
    {
        _owner = GetParent<Combatant>();
        // 每个实例独占形状，吕布横扫不会修改魏延的攻击范围。
        _shape = new CircleShape2D { Radius = 48 };
        GetNode<CollisionShape2D>(NodeNames.CollisionShape).Shape = _shape;
        CollisionLayer = PhysicsLayers.None;
        CollisionMask = _owner.IsEnemy ? PhysicsLayers.PlayerHurtbox : PhysicsLayers.EnemyHurtbox;
    }

    /// <summary>开始一次新施法，清空命中记录并按 spec 设置候选半径。</summary>
    public void Begin(AttackSpec spec)
    {
        _hitTargets.Clear();
        _shape.Radius = Mathf.Max(spec.Range, 1f);
    }

    /// <summary>清除本次命中记录；是否允许继续攻击由宿主状态决定。</summary>
    public void Clear() => _hitTargets.Clear();

    // Area2D 只负责候选重叠；生效窗口、扇形方向和每次施法去重由战斗规则控制。
    /// <summary>使用 spec 和施法锁定的单位向量 direction，结算当前重叠目标。</summary>
    public void Resolve(AttackSpec spec, Vector2 direction)
    {
        if (!_owner.IsAttackActive || spec.Damage <= 0) return;
        foreach (Area2D area in GetOverlappingAreas())
        {
            if (area is not Hurtbox hurtbox || hurtbox.Target is not Combatant target
                || target == _owner || target.IsEnemy == _owner.IsEnemy || target.IsDead)
                continue;
            Vector2 offset = target.GlobalPosition - _owner.GlobalPosition;
            // 用受击区域的中心裁定扇形边界，预警与判定共享同一半径和角度。
            if (offset.Length() > spec.Range || (offset.LengthSquared() > .01f
                && direction.Dot(offset.Normalized()) < Mathf.Cos(spec.HalfAngle))) continue;
            if (!_hitTargets.Add(target.GetInstanceId())) continue;
            hurtbox.TakeDamage(new DamageInfo(_owner, spec.Damage,
                offset.IsZeroApprox() ? direction : offset.Normalized()));
        }
    }
}
