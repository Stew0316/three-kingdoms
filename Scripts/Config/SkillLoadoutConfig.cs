using Godot;

/// <summary>默认技能装备表。槽位只是输入位置，技能可以来自任意武将；不保存运行时冷却。</summary>
[GlobalClass]
public partial class SkillLoadoutConfig : Resource
{
    [Export] public CombatActionConfig Basic { get; set; }
    [Export] public CombatActionConfig Dash { get; set; }
    [Export] public CombatActionConfig Sweep { get; set; }
    [Export] public CombatActionConfig Dodge { get; set; }
    [Export] public Godot.Collections.Array<PassiveConfig> Passives { get; set; } = new();

    public CombatActionConfig GetSkill(CombatAction slot) => slot switch
    {
        CombatAction.Dash => Dash, CombatAction.Sweep => Sweep,
        CombatAction.Dodge => Dodge, _ => Basic
    };
}
