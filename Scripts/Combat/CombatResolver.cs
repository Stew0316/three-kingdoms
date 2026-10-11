using Godot;
using System;
using System.Collections.Generic;

/// <summary>遭遇独占的伤害队列。主攻击及派生伤害统一结算，禁止反伤/反击递归触发。</summary>
public sealed class CombatResolver
{
    private readonly List<Combatant> _units = new(); // 当前遭遇目标集合，按单位去重。
    private readonly Queue<(Combatant Target, DamageInfo Info)> _pending = new();
    private readonly RandomNumberGenerator _random = new();
    private bool _draining;
    public bool PassivesEnabled { get; set; } = true; // 验证旧规则时隔离新增概率。
    public Func<float> RollOverride { get; set; } // 测试注入确定随机数；正常游戏为 null。
    public event Action Settled; // 整条伤害链结束后才允许胜负结算。

    public CombatResolver() => _random.Randomize();
    public void Register(Combatant unit) { if (!_units.Contains(unit)) _units.Add(unit); }
    private bool Roll(float chance) => chance > 0 && (chance >= 1 || (RollOverride?.Invoke() ?? _random.Randf()) < chance);

    /// <summary>提交请求并迭代排空。回调追加请求只入队，避免同步递归。</summary>
    public void Submit(Combatant target, DamageInfo info)
    {
        _pending.Enqueue((target, info));
        if (_draining) return;
        _draining = true;
        try
        {
            int budget = 32;
            while (_pending.Count > 0 && budget-- > 0)
            {
                var request = _pending.Dequeue();
                Resolve(request.Target, request.Info);
            }
            if (_pending.Count > 0) GD.PushWarning("伤害派生队列达到 32 次上限，已清理剩余请求。");
            _pending.Clear();
        }
        finally { _draining = false; }
        Settled?.Invoke();
    }

    private void Resolve(Combatant target, DamageInfo info)
    {
        if (!GodotObject.IsInstanceValid(target) || target.IsDead || target.BattleFinished || info.Amount <= 0) return;
        var source = info.Attacker as Combatant;
        bool attack = info.Kind == DamageKind.Attack && source != null && GodotObject.IsInstanceValid(source)
            && source.IsEnemy != target.IsEnemy;
        if (attack && PassivesEnabled)
            foreach (var passive in source.Skills.Passives)
                if (passive.Effect == PassiveConfig.EffectKind.Critical && Roll(passive.Chance))
                {
                    info = info with { Amount = Mathf.RoundToInt(info.Amount * Mathf.Max(1, passive.CriticalMultiplier)), IsCritical = true };
                    break; // 同一伤害包最多暴击一次。
                }
        target.ApplyResolvedDamage(info);
        if (!attack || !PassivesEnabled) return;
        foreach (var passive in target.Skills.Passives)
        {
            if (passive.Effect == PassiveConfig.EffectKind.Reflect && !source.IsDead && Roll(passive.Chance))
                _pending.Enqueue((source, new DamageInfo(target, passive.Damage, -info.KnockbackDirection, DamageKind.Reflected)));
            if (passive.Effect != PassiveConfig.EffectKind.CounterSpin || target.IsDead || !Roll(passive.Chance)) continue;
            target.PlayCounterSpin(passive.Radius, passive.VisualDuration);
            foreach (var unit in _units)
            {
                if (!GodotObject.IsInstanceValid(unit) || unit.IsDead || unit.IsEnemy == target.IsEnemy) continue;
                Vector2 offset = unit.GlobalPosition - target.GlobalPosition;
                if (offset.LengthSquared() <= passive.Radius * passive.Radius)
                    _pending.Enqueue((unit, new DamageInfo(target, passive.Damage, offset.Normalized(), DamageKind.Counter)));
            }
        }
    }
}
