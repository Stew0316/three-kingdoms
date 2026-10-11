// 一份配置同时驱动时间、范围提示和伤害判定。时间单位为秒，距离为世界像素。
/// <summary>四个输入槽位，名称保留既有按键含义；槽内技能可以互换。</summary>
public enum CombatAction { Basic, Dash, Sweep, Dodge }

/// <summary>技能向表现层请求的动作语义；与输入槽位及武将身份分开。</summary>
public enum CombatMotion { Basic, Dash, Sweep, Dodge }

/// <summary>不可变动作参数；玩家和敌人使用同一套执行逻辑。</summary>
/// <param name="Prepare">前摇秒数，只预警不造成伤害。</param>
/// <param name="Active">生效窗口秒数，期间允许命中或位移。</param>
/// <param name="Recover">后摇秒数，结束后恢复行动。</param>
/// <param name="Cooldown">从动作启动开始计算的冷却秒数。</param>
/// <param name="Range">命中扇形半径，单位为世界像素。</param>
/// <param name="HalfAngle">扇形半角，单位为弧度，总开角为此值的两倍。</param>
/// <param name="Damage">单次命中伤害；闪避为 0。</param>
/// <param name="Speed">生效窗口内的位移速度，单位为世界像素/秒。</param>
public readonly record struct AttackSpec(
    float Prepare, float Active, float Recover, float Cooldown,
    float Range, float HalfAngle, int Damage, float Speed)
{
    // 一次动作占用角色的总秒数；冷却独立计时，不计入动作总时长。
    public float Duration => Prepare + Active + Recover;
}
