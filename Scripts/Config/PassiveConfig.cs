using Godot;

/// <summary>只读被动定义；概率用 0～1，暴击倍率为最终总伤害倍率。</summary>
[GlobalClass]
public partial class PassiveConfig : Resource
{
    [Export] public string SkillId { get; set; } = ""; // 被动的稳定标识，与武将外观无关。
    public enum EffectKind { Reflect, Critical, CounterSpin }
    [Export] public string DisplayName { get; set; } = ""; // HUD 与触发反馈名称。
    [Export] public EffectKind Effect { get; set; } // 通用触发处理类型，不依赖武将名字。
    [Export(PropertyHint.Range, "0,1,0.01")] public float Chance { get; set; } = 1;
    [Export] public int Damage { get; set; } // 反伤/反击的固定伤害。
    [Export] public float CriticalMultiplier { get; set; } = 1; // 1.8 表示 180% 总伤害。
    [Export] public float Radius { get; set; } = 68; // 周身反击半径，战斗平面单位。
    [Export] public float VisualDuration { get; set; } = .48f; // 仅影响反击旋转表现。
}
