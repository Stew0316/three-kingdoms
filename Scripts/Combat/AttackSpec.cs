// 一份配置同时驱动时间、范围提示和伤害判定。时间单位为秒，距离为世界像素。
public enum CombatAction { Basic, Dash, Sweep, Dodge }

public readonly record struct AttackSpec(
    float Prepare, float Active, float Recover, float Cooldown,
    float Range, float HalfAngle, int Damage, float Speed)
{
    public float Duration => Prepare + Active + Recover;

    public static AttackSpec For(CombatAction action, bool enemy) => action switch
    {
        CombatAction.Dash => new(enemy ? .48f : .18f, .22f, .32f,
            enemy ? 4f : 3f, 40f, .72f, enemy ? 14 : 18, 360f),
        CombatAction.Sweep => new(enemy ? .65f : .32f, .14f, .40f,
            enemy ? 6f : 5f, 76f, 1.75f, enemy ? 18 : 24, 0f),
        CombatAction.Dodge => new(0f, .18f, .10f, 1.2f, 0f, 0f, 0, 330f),
        _ => new(enemy ? .40f : .14f, .10f, enemy ? .38f : .23f,
            enemy ? .95f : .48f, 48f, .95f, enemy ? 10 : 12, 0f)
    };
}
