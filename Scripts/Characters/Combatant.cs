using Godot;

public partial class Combatant : CharacterBody2D
{
    public enum State { Idle, Move, Attack, Dodge, Hurt, Dead }
    [Export] public bool IsEnemy { get; set; }
    [Export] public float MoveSpeed { get; set; } = 145f;
    [Export] public bool ArtFacesLeft { get; set; }
    public State CurrentState { get; private set; } = State.Idle;
    public Vector2 FacingDirection { get; private set; } = Vector2.Right;
    public Health Health { get; private set; }
    public bool IsDead => CurrentState == State.Dead;
    public bool BattleFinished { get; private set; }
    public bool CanAct => !BattleFinished && CurrentState is State.Idle or State.Move;
    public CombatAction CurrentAction { get; private set; }
    public AttackSpec Spec { get; private set; }
    public float ActionTime { get; private set; }
    public bool IsAttackActive => !BattleFinished && CurrentState == State.Attack
        && ActionTime >= Spec.Prepare && ActionTime < Spec.Prepare + Spec.Active;
    public string Phase => CurrentState is State.Attack or State.Dodge
        ? ActionTime < Spec.Prepare ? "蓄力" : ActionTime < Spec.Prepare + Spec.Active ? "出招" : "收招"
        : CurrentState switch { State.Hurt => "受击", State.Dead => "阵亡", State.Move => "移动", _ => "待机" };

    private readonly float[] _cooldowns = new float[4];
    private Vector2 _moveInput;
    private Vector2 _castDirection;
    private float _hurtTime;
    private float _hitStop;
    private float _visualTime;
    private Sprite2D _visual;
    private Hitbox _hitbox;
    private Hurtbox _hurtbox;
    private CombatFeedback _feedback;

    public override void _Ready()
    {
        MotionMode = MotionModeEnum.Floating;
        CollisionLayer = IsEnemy ? 16u : 4u;
        CollisionMask = IsEnemy ? 5u : 17u;
        Health = GetNode<Health>("Health");
        _visual = GetNode<Sprite2D>("Visual");
        _hitbox = GetNode<Hitbox>("AttackHitbox");
        _hurtbox = GetNode<Hurtbox>("Hurtbox");
        _hurtbox.CollisionLayer = IsEnemy ? 8u : 2u;
        _hurtbox.CollisionMask = 0;
        _feedback = GetNode<CombatFeedback>("Feedback");
        FacingDirection = IsEnemy ? Vector2.Left : Vector2.Right;
        Health.Died += Die;
    }

    public float Cooldown(CombatAction action) => _cooldowns[(int)action];

    public void SetMoveInput(Vector2 input)
    {
        _moveInput = input.LimitLength();
        if (CanAct && !input.IsZeroApprox()) Face(input);
    }

    public void Face(Vector2 direction)
    {
        if (CanAct && !direction.IsZeroApprox()) FacingDirection = direction.Normalized();
    }

    public bool TryAction(CombatAction action)
    {
        if (!CanAct || Cooldown(action) > 0) return false;
        CurrentAction = action;
        Spec = AttackSpec.For(action, IsEnemy);
        ActionTime = 0;
        _castDirection = FacingDirection;
        _cooldowns[(int)action] = Spec.Cooldown;
        Velocity = Vector2.Zero;
        _hitbox.Begin(Spec);
        SetState(action == CombatAction.Dodge ? State.Dodge : State.Attack);
        return true;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (_hitStop > 0) { _hitStop -= dt; return; }
        for (int i = 0; i < _cooldowns.Length; i++) _cooldowns[i] = Mathf.Max(0, _cooldowns[i] - dt);
        if (IsDead || BattleFinished) { Velocity = Vector2.Zero; QueueRedraw(); return; }

        if (CurrentState == State.Hurt)
        {
            _hurtTime -= dt;
            Velocity = Velocity.MoveToward(Vector2.Zero, 850f * dt);
            MoveAndSlide();
            if (_hurtTime <= 0) SetState(State.Idle);
        }
        else if (CurrentState is State.Attack or State.Dodge)
        {
            ActionTime += dt;
            bool active = ActionTime >= Spec.Prepare && ActionTime < Spec.Prepare + Spec.Active;
            Velocity = active ? _castDirection * Spec.Speed : Vector2.Zero;
            MoveAndSlide();
            if (active) _hitbox.Resolve(Spec, _castDirection);
            if (ActionTime >= Spec.Duration)
            {
                _hitbox.Clear();
                SetState(State.Idle);
            }
        }
        else
        {
            Velocity = _moveInput * MoveSpeed;
            SetState(_moveInput.IsZeroApprox() ? State.Idle : State.Move);
            MoveAndSlide();
        }
        UpdateVisual(dt);
        QueueRedraw();
    }

    public void ReceiveDamage(DamageInfo info)
    {
        if (IsDead || BattleFinished || info.Amount <= 0) return;
        Health.ApplyDamage(info);
        _feedback.ShowDamage(info.Amount);
        _hitStop = .035f;
        if (info.Attacker is Combatant attacker) attacker._hitStop = .035f;
        if (IsDead) return;
        _hitbox.Clear();
        SetState(State.Hurt);
        _hurtTime = .20f;
        Velocity = info.KnockbackDirection.Normalized() * 160f;
    }

    private void Die()
    {
        SetState(State.Dead);
        Velocity = Vector2.Zero;
        _hitbox.Clear();
        CollisionLayer = 0;
        CollisionMask = 0;
        _hurtbox.SetDeferred(Area2D.PropertyName.Monitorable, false);
        _visual.Modulate = new Color(.45f, .43f, .40f, .65f);
        _visual.Rotation = IsEnemy ? -.95f : .95f;
        _visual.Position = new Vector2(0, -22);
        QueueRedraw();
    }

    public void FinishBattle()
    {
        BattleFinished = true;
        _moveInput = Vector2.Zero;
        Velocity = Vector2.Zero;
        _hitbox.Clear();
        if (!IsDead) { SetState(State.Idle); _visual.Modulate = Colors.White; _visual.Rotation = 0; }
        QueueRedraw();
    }

    private void SetState(State state)
    {
        if (IsDead || CurrentState == state) return;
        CurrentState = state;
    }

    private void UpdateVisual(float dt)
    {
        _visualTime += dt;
        // 单张 PNG 仅水平镜像，不旋转人物来表示上/下朝向。精确方向由脚底箭头表达。
        if (Mathf.Abs(FacingDirection.X) > .15f)
            _visual.FlipH = (FacingDirection.X < 0) != ArtFacesLeft;
        float bounce = CurrentState == State.Move ? Mathf.Sin(_visualTime * 17f) * 1.6f : 0;
        _visual.Position = new Vector2(0, -51 + bounce);
        _visual.Rotation = CurrentState == State.Attack
            ? Mathf.Sin(Mathf.Clamp(ActionTime / Spec.Duration, 0, 1) * Mathf.Pi) * .07f * _castDirection.X : 0;
        _visual.Modulate = CurrentState == State.Hurt
            ? new Color(1.7f, 1.7f, 1.7f) : Colors.White;
    }

    public override void _Draw()
    {
        DrawSetTransform(Vector2.Zero, 0, new Vector2(1, .4f));
        DrawCircle(Vector2.Zero, 19, new Color(0, 0, 0, .28f));
        DrawSetTransform(Vector2.Zero);
        if (IsDead) return;
        Color teamColor = IsEnemy ? new Color("da7965") : new Color("80cfc4");
        DrawArc(Vector2.Zero, 15, 0, Mathf.Tau, 40, teamColor, 1.3f, true);
        Vector2 tip = FacingDirection * 23;
        Vector2 side = FacingDirection.Orthogonal() * 3;
        DrawColoredPolygon(new[] { tip, FacingDirection * 17 + side, FacingDirection * 17 - side }, teamColor);
        if (BattleFinished || CurrentState != State.Attack || ActionTime >= Spec.Prepare + Spec.Active) return;
        Color color = IsEnemy ? new Color(1, .30f, .20f) : new Color(.95f, .80f, .42f);
        bool active = IsAttackActive;
        float angle = _castDirection.Angle();
        const int segments = 36;
        var points = new Vector2[segments + 2];
        points[0] = Vector2.Zero;
        for (int i = 0; i <= segments; i++)
            points[i + 1] = Vector2.FromAngle(angle - Spec.HalfAngle + 2 * Spec.HalfAngle * i / segments) * Spec.Range;
        DrawColoredPolygon(points, new Color(color, active ? .30f : .12f));
        DrawArc(Vector2.Zero, Spec.Range, angle - Spec.HalfAngle, angle + Spec.HalfAngle, segments, color, active ? 3 : 1, true);
        DrawLine(Vector2.Zero, points[1], color, 1, true);
        DrawLine(Vector2.Zero, points[^1], color, 1, true);
        if (!active && Spec.Prepare > 0)
            DrawArc(Vector2.Zero, Spec.Range * ActionTime / Spec.Prepare,
                angle - Spec.HalfAngle, angle + Spec.HalfAngle, segments, new Color(color, .55f), 1, true);
        if (CurrentAction == CombatAction.Dash)
        {
            Vector2 end = _castDirection * Spec.Speed * Spec.Active;
            DrawLine(Vector2.Zero, end, new Color(color, .8f), 2, true);
            DrawCircle(end, 3, color);
        }
    }
}
