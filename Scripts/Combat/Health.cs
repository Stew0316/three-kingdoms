using Godot;

public partial class Health : Node
{
    [Signal] public delegate void DamagedEventHandler(int current, int max);
    [Signal] public delegate void DiedEventHandler();

    [Export] public int MaxHealth { get; set; } = 100;
    public int CurrentHealth { get; private set; }

    public override void _Ready()
    {
        CurrentHealth = MaxHealth;
    }

    public void ApplyDamage(DamageInfo info)
    {
        if (CurrentHealth <= 0) return; // 已死不再扣血

        CurrentHealth = Mathf.Max(0, CurrentHealth - info.Amount);
        EmitSignal(SignalName.Damaged, CurrentHealth, MaxHealth);

        if (CurrentHealth == 0)
            EmitSignal(SignalName.Died);
    }

    public void ResetHealth()
    {
        CurrentHealth = MaxHealth;
        // 最小实现不发信号；以后 HUD 需要同步时再发 Healed 信号
    }
}