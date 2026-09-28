using Godot;
using System.Collections.Generic;

public partial class Hitbox : Area2D
{
    private readonly HashSet<ulong> _hitTargets = new();
    private Combatant _owner;
    private CircleShape2D _shape;

    public override void _Ready()
    {
        _owner = GetParent<Combatant>();
        // 每个实例独占形状，吕布横扫不会修改魏延的攻击范围。
        _shape = new CircleShape2D { Radius = 48 };
        GetNode<CollisionShape2D>("CollisionShape2D").Shape = _shape;
        CollisionLayer = 0;
        CollisionMask = _owner.IsEnemy ? 2u : 8u;
    }

    public void Begin(AttackSpec spec)
    {
        _hitTargets.Clear();
        _shape.Radius = Mathf.Max(spec.Range, 1f);
    }

    public void Clear() => _hitTargets.Clear();

    // Area2D 只负责候选重叠；生效窗口、扇形方向和每次施法去重由战斗规则控制。
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
