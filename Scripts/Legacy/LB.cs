using Godot;
using System.Threading.Tasks;

public partial class LB : CharacterBody2D
{
    [Export] int speed = 300;

	public enum State { Idle, Move, Attack, Hurt, Dead }

	[Export] public State CurrentState { get; private set; } = State.Idle;
	[Export] Label stateLabel; // HUD 左上角的 Label，在场景里拖进来

    // 四方向，Down 为默认初始朝向
    public enum FaceDir { Down, Up, Left, Right }
    public FaceDir Facing { get; private set; } = FaceDir.Down;
	private Node2D _visual;

	private Area2D _attackHitbox;
	private bool _isAttacking;

	public override void _Ready()
	{
		_visual = GetNode<Node2D>(NodeNames.Visual);
		_attackHitbox = GetNode<Area2D>(NodeNames.AttackHitbox);
		_attackHitbox.AreaEntered += OnAttackHit; // 命中信号：攻击框检测到受击框时触发
	}

    public override void _PhysicsProcess(double delta)
    {
		if (Input.IsActionJustPressed(InputActions.Attack))
		{
			_ = PerformAttackAsync();
		}

        Vector2 direction = Input.GetVector(InputActions.MoveLeft, InputActions.MoveRight, InputActions.MoveUp, InputActions.MoveDown);

        // 只在有输入时更新：松手后保留最后一个非零方向
        if (direction != Vector2.Zero)
        {
            Facing = DirToFace(direction);
    		UpdateVisualFacing();
        }

		if (CurrentState is State.Idle or State.Move)
        {
            // 以输入为准切换，天然不会一帧内抖动
            SetState(direction != Vector2.Zero ? State.Move : State.Idle);

            Velocity = Velocity.MoveToward(direction * speed, speed * 8f * (float)delta);
            MoveAndSlide();
        }

        UpdateHud();
    }

	private void SetState(State newState)
    {
        if (CurrentState == newState) return;   // 同状态不重复触发
        if (CurrentState == State.Dead) return; // 死亡后锁定（为后面留的守卫）

        CurrentState = newState;
        // 以后：动画切换（AnimationPlayer）、状态信号都从这里发
    }

	private void UpdateHud()
    {
        if (stateLabel != null)
            stateLabel.Text = $"State: {CurrentState}";
    }

	// 攻击框命中受击框：统一走 TakeDamage 入口提交伤害
	private void OnAttackHit(Area2D area)
	{
		if (area is Hurtbox hurtbox)
		{
			hurtbox.TakeDamage(new DamageInfo(this, 12, FacingVector));
		}
	}

    // 斜向输入按分量绝对值取主方向
    private FaceDir DirToFace(Vector2 dir)
    {
        if (Mathf.Abs(dir.X) > Mathf.Abs(dir.Y))
            return dir.X > 0 ? FaceDir.Right : FaceDir.Left;
        return dir.Y > 0 ? FaceDir.Down : FaceDir.Up;
    }

    // 攻击、技能、击退需要向量时用这个转
    public Vector2 FacingVector => Facing switch
    {
        FaceDir.Up    => Vector2.Up,
        FaceDir.Left  => Vector2.Left,
        FaceDir.Right => Vector2.Right,
        _             => Vector2.Down,
    };
	private void UpdateVisualFacing()
	{
		_visual.Rotation = Facing switch
		{
			FaceDir.Up => Mathf.Pi,
			FaceDir.Left => -Mathf.Pi / 2f,
			FaceDir.Right => Mathf.Pi / 2f,
			_ => 0f, // Down
		};
	}

	private async Task PerformAttackAsync()
	{
		// 已经攻击时，忽略新的攻击输入。
		if (_isAttacking)
			return;

		_isAttacking = true;

		// 暂时让攻击盒放在角色前方。
		// 这里的 (32, 64) 是你当前角色碰撞中心附近的位置。
		_attackHitbox.Position = new Vector2(32, 64) + FacingVector * 64f;

		// 0.00 ~ 0.12 秒：前摇，尚不能命中。
		await ToSignal(
			GetTree().CreateTimer(0.12),
			SceneTreeTimer.SignalName.Timeout
		);

		// 0.12 ~ 0.22 秒：命中窗口打开。
		_attackHitbox.Monitoring = true;

		await ToSignal(
			GetTree().CreateTimer(0.10),
			SceneTreeTimer.SignalName.Timeout
		);

		// 0.22 秒后：关闭命中盒。
		_attackHitbox.Monitoring = false;

		// 0.22 ~ 0.35 秒：后摇。
		await ToSignal(
			GetTree().CreateTimer(0.13),
			SceneTreeTimer.SignalName.Timeout
		);

		_isAttacking = false;
	}
}
