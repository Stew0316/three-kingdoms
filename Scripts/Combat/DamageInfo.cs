using Godot;

/// <summary>一次命中的只读伤害请求；由命中盒传递到目标角色和生命组件。</summary>
/// <param name="Attacker">伤害来源，用于命中反馈和交战双方的打击停顿。</param>
/// <param name="Amount">请求扣除的生命值，非正值会被生命组件忽略。</param>
/// <param name="KnockbackDirection">击退方向，角色接收后归一化，长度不代表力度。</param>
public readonly record struct DamageInfo(
    Node2D Attacker,
    int Amount,
    Vector2 KnockbackDirection
);
