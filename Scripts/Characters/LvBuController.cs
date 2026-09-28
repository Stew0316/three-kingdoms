using Godot;

public partial class LvBuController : Node
{
    private Combatant _actor;
    public override void _Ready() { _actor = GetParent<Combatant>(); ProcessPhysicsPriority = -10; }
    public override void _PhysicsProcess(double delta)
    {
        if (!_actor.CanAct) return;
        _actor.SetMoveInput(Input.GetVector("move_left", "move_right", "move_up", "move_down"));
        if (Input.IsActionJustPressed("dodge")) _actor.TryAction(CombatAction.Dodge);
        else if (Input.IsActionJustPressed("skill_1")) _actor.TryAction(CombatAction.Dash);
        else if (Input.IsActionJustPressed("skill_2")) _actor.TryAction(CombatAction.Sweep);
        else if (Input.IsActionJustPressed("attack")) _actor.TryAction(CombatAction.Basic);
    }
}
