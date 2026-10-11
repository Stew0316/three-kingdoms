using Godot;
using System.Collections.Generic;

/// <summary>单位独占的技能槽和冷却。共享 Resource 始终只读，卸下技能仍保留其剩余冷却。</summary>
public sealed class SkillLoadoutRuntime
{
    private readonly CombatActionConfig[] _slots = new CombatActionConfig[4];
    private readonly List<PassiveConfig> _passives = new();
    private readonly Dictionary<string, float> _cooldowns = new();
    private readonly List<string> _cooldownKeys = new();
    public IReadOnlyList<PassiveConfig> Passives => _passives;

    public SkillLoadoutRuntime(SkillLoadoutConfig config)
    {
        if (config == null) return;
        for (int index = 0; index < _slots.Length; index++) _slots[index] = config.GetSkill((CombatAction)index);
        _passives.AddRange(config.Passives);
    }

    public CombatActionConfig GetSkill(CombatAction slot) => _slots[(int)slot];
    public float Cooldown(CombatAction slot)
    {
        var skill = GetSkill(slot);
        return skill != null && _cooldowns.TryGetValue(skill.SkillId, out float value) ? value : 0;
    }

    /// <summary>替换单个输入槽；同单位禁止重复技能，避免同技能占多槽绕开状态管理。</summary>
    public bool Equip(CombatAction slot, CombatActionConfig skill)
    {
        if (skill == null || string.IsNullOrWhiteSpace(skill.SkillId)) return false;
        for (int index = 0; index < _slots.Length; index++)
            if (index != (int)slot && _slots[index]?.SkillId == skill.SkillId) return false;
        _slots[(int)slot] = skill;
        return true;
    }

    public bool EquipPassive(int index, PassiveConfig passive)
    {
        if (index < 0 || index >= _passives.Count || passive == null || string.IsNullOrWhiteSpace(passive.SkillId)) return false;
        for (int other = 0; other < _passives.Count; other++)
            if (other != index && _passives[other].SkillId == passive.SkillId) return false;
        _passives[index] = passive;
        return true;
    }

    public void StartCooldown(CombatActionConfig skill, float duration)
    {
        if (!_cooldowns.ContainsKey(skill.SkillId)) _cooldownKeys.Add(skill.SkillId);
        _cooldowns[skill.SkillId] = duration;
    }

    public void Tick(float elapsed)
    {
        foreach (string id in _cooldownKeys) _cooldowns[id] = Mathf.Max(0, _cooldowns[id] - elapsed);
    }
}
