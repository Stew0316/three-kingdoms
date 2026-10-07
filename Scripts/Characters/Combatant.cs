using Godot;

/// <summary>玩家与敌人共用的移动、施法、受击和死亡规则；控制器只提交行动意图。</summary>
public partial class Combatant : CharacterBody2D
{
    // Idle/Move 接收行动，Attack/Dodge 执行动作，Hurt 锁定受击，Dead 永久停止行动。
    public enum State { Idle, Move, Attack, Dodge, Hurt, Dead }
    // 是否为敌方：用于选择阵营碰撞层、默认朝向与技能数值。
    [Export] public bool IsEnemy { get; set; }
    // 角色静态战斗数据，由 .tres 提供；运行时只读，不保存当前生命或冷却。
    [Export] public CombatantConfig Config { get; set; }
    [Export] public CharacterPresentationConfig Presentation { get; set; } // 美术图集与装饰效果。
    // 原始立绘是否朝左，用于计算水平镜像，避免把素材初始方向当作战斗方向。
    [Export] public bool ArtFacesLeft { get; set; }
    // 当前状态，只允许通过 SetState 切换。
    public State CurrentState { get; private set; } = State.Idle;
    // 最后一次有效朝向的单位向量；角色停下后仍保留。
    public Vector2 FacingDirection { get; private set; } = Vector2.Right;
    // 角色生命组件；血量变化和死亡通知由它统一发出。
    public Health Health { get; private set; }
    // 是否已进入不可恢复的死亡状态；重开通过创建新角色恢复。
    public bool IsDead => CurrentState == State.Dead;
    // 整场对战是否结束，胜方也必须停止移动与出招。
    public bool BattleFinished { get; private set; }
    // 当前是否允许接受新的移动/施法请求。
    public bool CanAct => !BattleFinished && CurrentState is State.Idle or State.Move;
    // 最近一次成功启动的动作类型；是否仍执行要结合 CurrentState 判断。
    public CombatAction CurrentAction { get; private set; }
    // 本次动作的数值快照，供命中判定与表现层共同读取。
    public AttackSpec Spec { get; private set; }
    // 本次动作从启动起累计的秒数，暂停和打击停顿时不推进。
    public float ActionTime { get; private set; }
    // 是否处于可提交伤害的生效窗口；前摇和后摇均不能命中。
    public bool IsAttackActive => !BattleFinished && CurrentState == State.Attack
        && ActionTime >= Spec.Prepare && ActionTime < Spec.Prepare + Spec.Active;
    // 供 HUD 与 AI 调试显示的当前动作阶段名称。
    public string Phase => CurrentState is State.Attack or State.Dodge
        ? ActionTime < Spec.Prepare ? BattleTexts.Charging : ActionTime < Spec.Prepare + Spec.Active ? BattleTexts.Active : BattleTexts.Recovering
        : CurrentState switch { State.Hurt => BattleTexts.HurtState, State.Dead => BattleTexts.DeadState, State.Move => BattleTexts.MoveState, _ => BattleTexts.IdleState };

    // 按 CombatAction 枚举索引保存各动作的剩余冷却秒数。
    private readonly float[] _cooldowns = new float[4];
    // 控制器提交的移动意图，长度限制为 1 以避免斜向加速。
    private Vector2 _moveInput;
    // 施法开始时锁定的方向，攻击中不跟随新的转向输入。
    private Vector2 _castDirection;
    // 剩余受击硬直秒数，归零后恢复行动。
    private float _hurtTime;
    // 剩余打击停顿秒数，期间冻结本角色的战斗推进。
    private float _hitStop;
    // 空闲/行走等循环表现的累计秒数，角色停止处理时同步冻结。
    private float _visualTime;
    public float VisualTime => _visualTime; // 表现层共用的动作时钟，受打击停顿和暂停约束。
    public Vector2 CastDirection => _castDirection; // 本次施法锁定方向，供刀光读取。
    public CombatResolver Resolver { get; set; } // 由当前遭遇注入，不跨场景共享。
    public float CounterRemaining { get; private set; } // 反击动画剩余时间，不驱动伤害。
    public float CounterDuration { get; private set; }
    public float CounterRadius { get; private set; }
    public DamageInfo LastDamage { get; private set; } // 最近实际结算结果，用于反馈与验证。
    public CharacterRig Rig { get; private set; } // 二维正式场景与历史三维桥接共用的骨骼表现引用。
    public float WalkDistance { get; private set; } // 主动行走实际路程；碰墙、攻击位移和击退不计入步态。
    public Vector2 WalkVelocity { get; private set; } // 碰撞后的主动行走速度，供表现层判断是否真正迈步。
    public WeaponTrail Trail { get; private set; }

    public void PlayCounterSpin(float radius, float duration)
    {
        CounterRadius = Mathf.Max(1, radius);
        CounterDuration = Mathf.Max(.05f, duration);
        CounterRemaining = CounterDuration;
    }
    // 静态图资源及镜像/颜色入口；接入骨骼后由 Rig 读取，原 Sprite 隐藏。
    private Sprite2D _visual;
    // 主动攻击区域，负责候选目标筛选和单次施法去重。
    private Hitbox _hitbox;
    // 被动受击区域，接收对方攻击并转交本角色。
    private Hurtbox _hurtbox;
    // 伤害数字和命中火花的显示组件。
    private CombatFeedback _feedback;

    /// <summary>绑定角色组件、阵营碰撞层及死亡信号。</summary>
    public override void _Ready()
    {
        if (Config == null)
        {
            GD.PushError($"{Name} 缺少 CombatantConfig，角色无法正常行动。");
            SetPhysicsProcess(false);
            return;
        }
        MotionMode = MotionModeEnum.Floating;
        CollisionLayer = IsEnemy ? PhysicsLayers.EnemyBody : PhysicsLayers.PlayerBody;
        CollisionMask = IsEnemy ? PhysicsLayers.EnemyBodyMask : PhysicsLayers.PlayerBodyMask;
        Health = GetNode<Health>(NodeNames.Health);
        Rig = GetNode<CharacterRig>(NodeNames.Rig);
        Trail = GetNode<WeaponTrail>(NodeNames.WeaponTrail);
        Health.Configure(Config.MaxHealth);
        _visual = GetNode<Sprite2D>(NodeNames.Visual);
        _hitbox = GetNode<Hitbox>(NodeNames.AttackHitbox);
        _hurtbox = GetNode<Hurtbox>(NodeNames.Hurtbox);
        _hurtbox.CollisionLayer = IsEnemy ? PhysicsLayers.EnemyHurtbox : PhysicsLayers.PlayerHurtbox;
        _hurtbox.CollisionMask = PhysicsLayers.None;
        _feedback = GetNode<CombatFeedback>(NodeNames.Feedback);
        FacingDirection = IsEnemy ? Vector2.Left : Vector2.Right;
        Health.Died += Die;
    }

    /// <summary>查询 action 对应动作的剩余冷却秒数；0 表示冷却结束。</summary>
    public float Cooldown(CombatAction action) => _cooldowns[(int)action];

    /// <summary>接收移动意图 input；零向量表示停止，不会清除最后朝向。</summary>
    public void SetMoveInput(Vector2 input)
    {
        _moveInput = input.LimitLength();
        if (CanAct && !input.IsZeroApprox()) Face(input);
    }

    /// <summary>以 direction 更新朝向；攻击、受击等锁定状态下忽略转向。</summary>
    public void Face(Vector2 direction)
    {
        if (CanAct && !direction.IsZeroApprox()) FacingDirection = direction.Normalized();
    }

    /// <summary>尝试启动 action；成功则锁定方向并消耗冷却，不满足状态或冷却条件时返回 false。</summary>
    public bool TryAction(CombatAction action)
    {
        if (!CanAct || Cooldown(action) > 0) return false;
        CurrentAction = action;
        CombatActionConfig actionConfig = Config.GetAction(action);
        if (actionConfig == null) return false;
        Spec = actionConfig.ToSpec();
        ActionTime = 0;
        _castDirection = FacingDirection;
        _cooldowns[(int)action] = Spec.Cooldown;
        Velocity = Vector2.Zero;
        _hitbox.Begin(Spec);
        SetState(action == CombatAction.Dodge ? State.Dodge : State.Attack);
        return true;
    }

    /// <summary>按固定物理步推进动作与碰撞；delta 是本步经过的秒数。</summary>
    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        WalkVelocity = Vector2.Zero;
        if (_hitStop > 0) { _hitStop -= dt; return; }
        CounterRemaining = Mathf.Max(0, CounterRemaining - dt);
        for (int i = 0; i < _cooldowns.Length; i++) _cooldowns[i] = Mathf.Max(0, _cooldowns[i] - dt);
        if (IsDead || BattleFinished) { Velocity = Vector2.Zero; QueueRedraw(); return; }

        if (CurrentState == State.Hurt)
        {
            _hurtTime -= dt;
            Velocity = Velocity.MoveToward(Vector2.Zero, Config.KnockbackDeceleration * dt);
            MoveAndSlide();
            if (_hurtTime <= 0) SetState(State.Idle);
        }
        else if (CurrentState is State.Attack or State.Dodge)
        {
            ActionTime += dt;
            // 位移与命中只在生效窗口执行，前摇用于预警，后摇提供反击空档。
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
            Velocity = _moveInput * Config.MoveSpeed;
            SetState(_moveInput.IsZeroApprox() ? State.Idle : State.Move);
            Vector2 beforeMove = GlobalPosition;
            MoveAndSlide();
            Vector2 travel = GlobalPosition - beforeMove;
            WalkVelocity = travel / Mathf.Max(dt, .001f);
            // 墙体安全边距会产生亚像素往返修正；忽略低于 1 像素/秒的抖动，避免顶墙累积假步长。
            if (WalkVelocity.LengthSquared() > 1) WalkDistance += travel.Length();
        }
        UpdateVisual(dt);
        QueueRedraw();
    }

    /// <summary>处理 info 中的伤害及击退方向；死亡或结算后不再接受伤害。</summary>
    public void ReceiveDamage(DamageInfo info)
    {
        if (Resolver != null) Resolver.Submit(this, info);
        else ApplyResolvedDamage(info);
    }

    /// <summary>仅供结算器提交已计算的结果；派生伤害不附带硬直和打击停顿。</summary>
    internal void ApplyResolvedDamage(DamageInfo info)
    {
        if (IsDead || BattleFinished || info.Amount <= 0) return;
        LastDamage = info;
        // ApplyDamage 可能同步触发 Die 和整场结算，后续必须再次检查 IsDead。
        Health.ApplyDamage(info);
        _feedback.ShowDamage(info.Amount, info.IsCritical, info.Kind);
        if (info.Kind is DamageKind.Reflected or DamageKind.Counter) return;
        _hitStop = Config.HitStopDuration;
        if (info.Attacker is Combatant attacker) attacker._hitStop = attacker.Config.HitStopDuration;
        if (IsDead) return;
        _hitbox.Clear();
        SetState(State.Hurt);
        _hurtTime = Config.HurtDuration;
        Velocity = info.KnockbackDirection.Normalized() * Config.KnockbackSpeed;
    }

    /// <summary>锁定死亡状态、取消命中并关闭身体/受击能力，保留尸体表现。</summary>
    private void Die()
    {
        SetState(State.Dead);
        Velocity = Vector2.Zero;
        _hitbox.Clear();
        CollisionLayer = PhysicsLayers.None;
        CollisionMask = PhysicsLayers.None;
        _hurtbox.SetDeferred(Area2D.PropertyName.Monitorable, false);
        _visual.Modulate = PresentationPalette.DeathTint;
        // 不翻转 Sprite2D；骨骼层改用纵向压缩和下沉表现倒地。
        _visual.Rotation = 0;
        _visual.Position = new Vector2(0, -22);
        QueueRedraw();
    }

    /// <summary>停止本角色参与对战；死亡角色保留死亡状态，存活角色回到待机。</summary>
    public void FinishBattle()
    {
        BattleFinished = true;
        CounterRemaining = 0;
        _moveInput = Vector2.Zero;
        Velocity = Vector2.Zero;
        _hitbox.Clear();
        if (!IsDead) { SetState(State.Idle); _visual.Modulate = Colors.White; _visual.Rotation = 0; }
        QueueRedraw();
    }

    /// <summary>集中切换 state；禁止从死亡状态退出，也避免重复设置相同状态。</summary>
    private void SetState(State state)
    {
        if (IsDead || CurrentState == state) return;
        CurrentState = state;
    }

    /// <summary>按 dt 秒更新表现时钟与原图镜像、色彩，供骨骼表现读取。</summary>
    private void UpdateVisual(float dt)
    {
        _visualTime += dt;
        // 单张 PNG 仅水平镜像，不旋转人物来表示上/下朝向。精确方向由脚底箭头表达。
        if (Mathf.Abs(FacingDirection.X) > .15f)
            _visual.FlipH = (FacingDirection.X < 0) != ArtFacesLeft;
        float bounce = CurrentState == State.Move ? Mathf.Sin(_visualTime * 17f) * 1.6f : 0;
        _visual.Position = new Vector2(0, -34 + bounce);
        _visual.Rotation = CurrentState == State.Attack
            ? Mathf.Sin(Mathf.Clamp(ActionTime / Spec.Duration, 0, 1) * Mathf.Pi) * .07f * _castDirection.X : 0;
        _visual.Modulate = CurrentState == State.Hurt
            ? PresentationPalette.HurtFlash : Colors.White;
    }

    /// <summary>角色节点不再直接绘制纸片式地面效果；光环、阴影、粒子和预警统一由 GroundEffects 表现层绘制。</summary>
    public override void _Draw()
    {
        // 保留空绘制入口，便于以后添加碰撞/导航调试显示；正式表现不与规则节点耦合。
    }
}
