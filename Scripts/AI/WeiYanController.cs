using Godot;

/// <summary>距离型战斗控制器；魏延默认装配此控制器，按当前装备技能的语义决策，不依赖武将身份。</summary>
public partial class WeiYanController : Node
{
    // 本场追击目标，由 Arena 在双方初始化后绑定。
    public Combatant Target { get; set; }
    // 最近的决策或动作阶段，供 HUD 调试显示。
    public string Decision { get; private set; } = BattleTexts.Observe;
    // 执行 AI 指令的父级角色。
    private Combatant _actor;
    // 距下次决策的剩余秒数；开场先观察 1.1 秒，之后每 0.1 秒决策。
    private float _nextDecision = 1.1f;
    // 已成功启动的攻击次数，保证首个近身动作不会直接选择横扫。
    private int _attackCount;

    /// <summary>绑定角色并先于角色物理更新提交 AI 意图。</summary>
    public override void _Ready() { _actor = GetParent<Combatant>(); ProcessPhysicsPriority = ProcessPriorities.Controller; }

    /// <summary>按 delta 秒推进决策间隔；忙碌、死亡或目标失效时不启动新动作。</summary>
    public override void _PhysicsProcess(double delta)
    {
        if (_actor.IsDead || _actor.BattleFinished || Target == null || !IsInstanceValid(Target) || Target.IsDead)
        {
            _actor.SetMoveInput(Vector2.Zero);
            Decision = BattleTexts.Stop;
            return;
        }
        if (!_actor.CanAct) { Decision = _actor.Phase; return; }
        _nextDecision -= (float)delta;
        if (_nextDecision > 0) return;
        _nextDecision = .1f;
        Vector2 offset = Target.GlobalPosition - _actor.GlobalPosition;
        float distance = offset.Length();
        _actor.Face(offset);
        _actor.SetMoveInput(Vector2.Zero);

        if (TryMotion(CombatMotion.Dash, distance) || TryMotion(CombatMotion.Sweep, distance)
            || TryMotion(CombatMotion.Basic, distance)) return;
        float approachRange = 42;
        foreach (CombatAction slot in System.Enum.GetValues<CombatAction>())
        {
            var skill = _actor.GetSkill(slot);
            if (skill != null && skill.Motion != CombatMotion.Dodge)
                approachRange = Mathf.Min(approachRange, Mathf.Max(8, skill.Range - 6));
        }
        if (distance > approachRange) { _actor.SetMoveInput(offset.Normalized()); Decision = BattleTexts.Chase; }
        else { Decision = BattleTexts.Observe; }
    }

    /// <summary>从实际装备查找指定动作语义；只有施法成功才更新技能名称和攻击计数。</summary>
    private bool TryMotion(CombatMotion motion, float distance)
    {
        foreach (CombatAction slot in System.Enum.GetValues<CombatAction>())
        {
            var skill = _actor.GetSkill(slot);
            if (skill == null || skill.Motion != motion || _actor.Cooldown(slot) > 0) continue;
            bool inRange = motion switch
            {
                CombatMotion.Dash => distance > skill.Range + 42 && distance < skill.Range + skill.Speed * skill.Active + 36,
                CombatMotion.Sweep => _attackCount > 0 && distance < skill.Range - 8,
                _ => distance <= skill.Range - 4
            };
            if (inRange && _actor.TryAction(slot)) { Decision = skill.DisplayName; _attackCount++; return true; }
        }
        return false;
    }
}
