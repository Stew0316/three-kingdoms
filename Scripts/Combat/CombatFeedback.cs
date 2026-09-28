using Godot;

public partial class CombatFeedback : Node2D
{
    private float _remaining;
    private int _damage;
    public void ShowDamage(int damage) { _damage = damage; _remaining = .6f; }
    public override void _Process(double delta)
    {
        if (_remaining <= 0) return;
        _remaining -= (float)delta;
        QueueRedraw();
    }
    public override void _Draw()
    {
        if (_remaining <= 0) return;
        float alpha = Mathf.Min(1, _remaining * 3);
        DrawString(ThemeDB.FallbackFont, new Vector2(-10, -100 - (1 - _remaining / .6f) * 22),
            $"-{_damage}", HorizontalAlignment.Left, -1, 16, new Color(1, .89f, .65f, alpha));
        if (_remaining < .43f) return;
        for (int i = 0; i < 6; i++)
        {
            Vector2 direction = Vector2.FromAngle(i * Mathf.Tau / 6 + .2f);
            DrawLine(new Vector2(0, -35) + direction * 6, new Vector2(0, -35) + direction * 17,
                new Color(1, .94f, .77f, alpha), 2, true);
        }
    }
}
