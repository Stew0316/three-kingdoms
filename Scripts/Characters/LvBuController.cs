using Godot;

/// <summary>将玩家输入转换为公共角色命令，不直接操纵生命、碰撞或特效。</summary>
public partial class LvBuController : Node
{
    // 接收玩家意图的父级角色；角色状态决定命令是否可以执行。
    private Combatant _actor;
    /// <summary>绑定宿主；物理优先级 -10 使输入先于角色物理步读取。</summary>
    public override void _Ready() { _actor = GetParent<Combatant>(); ProcessPhysicsPriority = ProcessPriorities.Controller; }
    /// <summary>每物理帧读取输入；delta 是引擎传入的步长，本控制器不单独计时。</summary>
    public override void _PhysicsProcess(double delta)
    {
        if (!_actor.CanAct) return;
        _actor.SetMoveInput(Input.GetVector(InputActions.MoveLeft, InputActions.MoveRight, InputActions.MoveUp, InputActions.MoveDown));
        if (_actor.SuppressActionsUntilReleased)
        {
            // 暂停期间的确认点击可能仍处于按下状态，必须经过释放后才接受下一次出招。
            if (!Input.IsActionPressed(InputActions.Attack) && !Input.IsActionPressed(InputActions.Dodge)
                && !Input.IsActionPressed(InputActions.Skill1) && !Input.IsActionPressed(InputActions.Skill2)
                && !Input.IsActionPressed(InputActions.Skill3) && !Input.IsActionPressed(InputActions.Skill4))
                _actor.SuppressActionsUntilReleased=false;
            return;
        }
        // 同帧多键只执行一种动作，优先级为闪避、突进、横扫、普攻。
        if (Input.IsActionJustPressed(InputActions.Dodge)) _actor.TryAction(CombatAction.Dodge);
        else if (Input.IsActionJustPressed(InputActions.Skill1)) _actor.TryAction(CombatAction.Dash);
        else if (Input.IsActionJustPressed(InputActions.Skill2)) _actor.TryAction(CombatAction.Sweep);
        else if (Input.IsActionJustPressed(InputActions.Skill3)) _actor.TryAction(CombatAction.Skill3);
        else if (Input.IsActionJustPressed(InputActions.Skill4)) _actor.TryAction(CombatAction.Skill4);
        // 点击物品等 UI 时不把同一鼠标按下作为普通攻击，键盘攻击仍按映射处理。
        else if (Input.IsActionJustPressed(InputActions.Attack)
            && !(Input.IsMouseButtonPressed(MouseButton.Left) && GetViewport().GuiGetHoveredControl()!=null)) _actor.TryAction(CombatAction.Basic);
    }
}
