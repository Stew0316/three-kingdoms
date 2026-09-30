using Godot;

/// <summary>魏延的最小距离决策器，使用与玩家相同的移动和施法入口。</summary>
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

        if (distance > 82 && distance < 155 && _actor.Cooldown(CombatAction.Dash) <= 0)
            Act(CombatAction.Dash, BattleTexts.Dash);
        else if (distance < 68 && _attackCount > 0 && _actor.Cooldown(CombatAction.Sweep) <= 0)
            Act(CombatAction.Sweep, BattleTexts.Sweep);
        else if (distance <= 44 && _actor.Cooldown(CombatAction.Basic) <= 0)
            Act(CombatAction.Basic, BattleTexts.BasicAttack);
        else if (distance > 42) { _actor.SetMoveInput(offset.Normalized()); Decision = BattleTexts.Chase; }
        else { Decision = BattleTexts.Observe; }
    }

    /// <summary>尝试 action 动作；只有启动成功才更新 label 调试文案和攻击计数。</summary>
    private void Act(CombatAction action, string label)
    {
        if (_actor.TryAction(action)) { Decision = label; _attackCount++; }
    }
}
