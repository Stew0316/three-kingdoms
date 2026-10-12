using Godot;

/// <summary>单位独占的当前近战状态；不写回技能资源，换装不清除已生效状态。</summary>
public sealed class CombatStatusRuntime
{
    public float StunRemaining { get; private set; } // 额外受击锁定秒数。
    public float SlowRemaining { get; private set; } // 普通行走减速剩余秒数。
    public float VulnerableRemaining { get; private set; } // 攻击易伤剩余秒数。
    public float EmpowerRemaining { get; private set; } // 自身增伤/吸血剩余秒数。
    private float _slow = 1, _vulnerability, _damage = 1, _lifesteal;
    public float MoveMultiplier => SlowRemaining > 0 ? _slow : 1;
    public float IncomingMultiplier => VulnerableRemaining > 0 ? 1 + _vulnerability : 1;
    public float DamageMultiplier => EmpowerRemaining > 0 ? _damage : 1;
    public float Lifesteal => EmpowerRemaining > 0 ? _lifesteal : 0;
    /// <summary>由单位物理时钟推进，到期后各倍率属性自动返回基础值。</summary>
    public void Tick(float dt)
    {
        StunRemaining = Mathf.Max(0, StunRemaining - dt);
        SlowRemaining = Mathf.Max(0, SlowRemaining - dt);
        VulnerableRemaining = Mathf.Max(0, VulnerableRemaining - dt);
        EmpowerRemaining = Mathf.Max(0, EmpowerRemaining - dt);
    }
    /// <summary>命中后提交控制；当前同类减速/易伤取强度较强、持续时间较长者。</summary>
    public void Apply(DamageInfo info)
    {
        StunRemaining = Mathf.Max(StunRemaining, info.StunSeconds);
        if (info.SlowFactor < 1 && info.StatusDuration > 0)
        { _slow = SlowRemaining > 0 ? Mathf.Min(_slow, info.SlowFactor) : info.SlowFactor; SlowRemaining = Mathf.Max(SlowRemaining, info.StatusDuration); }
        if (info.Vulnerability > 0 && info.StatusDuration > 0)
        { _vulnerability = VulnerableRemaining > 0 ? Mathf.Max(_vulnerability, info.Vulnerability) : info.Vulnerability; VulnerableRemaining = Mathf.Max(VulnerableRemaining, info.StatusDuration); }
    }
    /// <summary>一次性接收施法快照；当前自身强化后施放覆盖前施放，不支持叠层。</summary>
    public void Empower(AttackSpec spec)
    {
        if (spec.StatusDuration <= 0 || (spec.DamageMultiplier <= 1 && spec.Lifesteal <= 0)) return;
        _damage = spec.DamageMultiplier; _lifesteal = spec.Lifesteal;
        EmpowerRemaining = spec.StatusDuration;
    }
}
