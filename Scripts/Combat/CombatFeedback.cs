using Godot;

/// <summary>短时伤害飘字与命中火花；仅呈现已发生的伤害，不执行伤害判定。</summary>
public partial class CombatFeedback : Node2D
{
    // 本次反馈剩余秒数，归零后停止绘制。
    private float _remaining;
    // 最近一次实际提交给反馈组件的伤害数值，用于飘字。
    private int _damage;
    public override void _Ready() => ZIndex = PresentationZIndexes.CombatFeedback;
    /// <summary>显示 damage 伤害值，并重置为 0.6 秒反馈。</summary>
    public void ShowDamage(int damage) { _damage = damage; _remaining = .6f; }
    /// <summary>按 delta 秒衰减寿命，随场景暂停自动停止。</summary>
    public override void _Process(double delta)
    {
        if (_remaining <= 0) return;
        _remaining -= (float)delta;
        QueueRedraw();
    }
    /// <summary>绘制向上漂移的伤害数字，火花只保留在反馈初段。</summary>
    public override void _Draw()
    {
        if (_remaining <= 0) return;
        float alpha = Mathf.Min(1, _remaining * 3);
        DrawString(ThemeDB.FallbackFont, new Vector2(-10, -72 - (1 - _remaining / .6f) * 22),
            $"-{_damage}", HorizontalAlignment.Left, -1, 16, new Color(PresentationPalette.DamageText, alpha));
        if (_remaining < .43f) return;
        for (int i = 0; i < 6; i++)
        {
            Vector2 direction = Vector2.FromAngle(i * Mathf.Tau / 6 + .2f);
            DrawLine(new Vector2(0, -35) + direction * 6, new Vector2(0, -35) + direction * 17,
                new Color(PresentationPalette.HitSpark, alpha), 2, true);
        }
    }
}
