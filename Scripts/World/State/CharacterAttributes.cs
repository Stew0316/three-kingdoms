using Godot;
using System;

/// <summary>按稳定人物 ID 唯一持有的冒险属性；场景角色和菜单引用同一个对象。</summary>
public sealed class CharacterAttributes
{
    public string Id { get; }
    public CombatantConfig Config { get; }
    public float MoveRate { get; internal set; } = 1;
    public float DamageMultiplier { get; internal set; } = 1;
    public int CurrentHealth { get; internal set; }
    public float HealthRatio => (float)CurrentHealth / Config.MaxHealth;

    internal CharacterAttributes(string id, CombatantConfig definition, float ratio)
    {
        Id = id;
        // 唯一实例副本与只读内容资源分离，所有冒险场景共享此副本。
        Config = (CombatantConfig)definition.Duplicate();
        CurrentHealth = Math.Max(1, Mathf.RoundToInt(Config.MaxHealth * ratio));
    }

    internal void RestoreRatio(float ratio) =>
        CurrentHealth = Math.Max(1, Mathf.RoundToInt(Config.MaxHealth * Mathf.Clamp(ratio, .01f, 1)));
}
