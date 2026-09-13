using Godot;

public partial class Dummy : CharacterBody2D
{
    private ColorRect _rect;
    private Health _health;

    public override void _Ready()
    {
        _health = GetNode<Health>("Health");
        _rect = GetNode<ColorRect>("ColorRect");

        _health.Damaged += (cur, max) =>
        {
            GD.Print($"木桩血量 {cur}/{max}");
            _rect.Color = Colors.Blue; // 受击反馈：变蓝
        };
        _health.Died += () =>
        {
            GD.Print("木桩死亡");
            _rect.Color = Colors.White; // 死亡反馈：变回白色
        };
    }

    public override void _PhysicsProcess(double delta)
    {
        // H 键：木桩回满血
        if (Input.IsActionJustPressed("health"))
        {
            _health.ResetHealth();
            _rect.Color = Colors.White; // 颜色一并归位
            GD.Print($"木桩血量已恢复 {_health.CurrentHealth}/{_health.MaxHealth}");
        }
    }
}