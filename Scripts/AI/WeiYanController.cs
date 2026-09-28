using Godot;

public partial class WeiYanController : Node
{
    public Combatant Target { get; set; }
    public string Decision { get; private set; } = "观察";
    private Combatant _actor;
    private float _nextDecision = 1.1f;
    private int _attackCount;

    public override void _Ready() { _actor = GetParent<Combatant>(); ProcessPhysicsPriority = -10; }

    public override void _PhysicsProcess(double delta)
    {
        if (_actor.IsDead || _actor.BattleFinished || Target == null || !IsInstanceValid(Target) || Target.IsDead)
        {
            _actor.SetMoveInput(Vector2.Zero);
            Decision = "停止";
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
            Act(CombatAction.Dash, "突进");
        else if (distance < 68 && _attackCount > 0 && _actor.Cooldown(CombatAction.Sweep) <= 0)
            Act(CombatAction.Sweep, "横扫");
        else if (distance <= 44 && _actor.Cooldown(CombatAction.Basic) <= 0)
            Act(CombatAction.Basic, "普攻");
        else if (distance > 42) { _actor.SetMoveInput(offset.Normalized()); Decision = "接近"; }
        else { Decision = "观察"; }
    }

    private void Act(CombatAction action, string label)
    {
        if (_actor.TryAction(action)) { Decision = label; _attackCount++; }
    }
}
