using Godot;

/// <summary>旧测试场景使用的受击木桩；保留用于独立验证生命组件。</summary>
public partial class Dummy : CharacterBody2D
{
    // 木桩受击与死亡时切换颜色的占位图形。
    private ColorRect _rect;
    // 木桩复用的生命组件。
    private Health _health;

    public override void _Ready()
    {
        _health = GetNode<Health>(NodeNames.Health);
        _rect = GetNode<ColorRect>(NodeNames.ColorRect);

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
        if (Input.IsActionJustPressed(InputActions.RestoreHealth))
        {
            _health.ResetHealth();
            _rect.Color = Colors.White; // 颜色一并归位
            GD.Print($"木桩血量已恢复 {_health.CurrentHealth}/{_health.MaxHealth}");
        }
    }
}
