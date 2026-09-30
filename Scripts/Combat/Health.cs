using Godot;

/// <summary>管理最大/当前生命并发送变化与死亡信号，不负责位移或动画。</summary>
public partial class Health : Node
{
    // 生命刷新通知：current 为当前生命，max 为上限；当前回血接口也复用此信号。
    [Signal] public delegate void DamagedEventHandler(int current, int max);
    // 生命首次从存活降至 0 时发送，通知角色和对局停止行动。
    [Signal] public delegate void DiedEventHandler();

    // 生命上限，在角色场景 Inspector 中配置，生成与重置时作为初始血量。
    [Export] public int MaxHealth { get; set; } = 100;
    // 当前剩余生命，只允许生命组件内部修改。
    public int CurrentHealth { get; private set; }

    /// <summary>角色入树时按上限初始化生命。</summary>
    public override void _Ready()
    {
        CurrentHealth = MaxHealth;
    }

    /// <summary>应用配置中的 maxHealth 上限，并按 refill 决定是否回满当前生命。</summary>
    public void Configure(int maxHealth, bool refill = true)
    {
        MaxHealth = Mathf.Max(1, maxHealth);
        if (refill)
            CurrentHealth = MaxHealth;
        else
            CurrentHealth = Mathf.Min(CurrentHealth, MaxHealth);
    }

    /// <summary>扣除 info.Amount，最低为 0；忽略无效伤害和对尸体的重复命中。</summary>
    public void ApplyDamage(DamageInfo info)
    {
        if (CurrentHealth <= 0 || info.Amount <= 0) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - info.Amount);
        EmitSignal(SignalName.Damaged, CurrentHealth, MaxHealth);

        if (CurrentHealth == 0)
            EmitSignal(SignalName.Died);
    }

    /// <summary>恢复至最大生命并刷新订阅者；不负责重置宿主角色的死亡状态。</summary>
    public void ResetHealth()
    {
        CurrentHealth = MaxHealth;
        EmitSignal(SignalName.Damaged, CurrentHealth, MaxHealth);
    }
}
