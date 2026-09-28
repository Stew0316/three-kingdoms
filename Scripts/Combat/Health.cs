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
        if (CurrentHealth <= 0 || info.Amount <= 0) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - info.Amount);
        EmitSignal(SignalName.Damaged, CurrentHealth, MaxHealth);

        if (CurrentHealth == 0)
            EmitSignal(SignalName.Died);
    }

    public void ResetHealth()
    {
        CurrentHealth = MaxHealth;
        EmitSignal(SignalName.Damaged, CurrentHealth, MaxHealth);
    }
}
