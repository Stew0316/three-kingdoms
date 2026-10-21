/// <summary>四个输入槽位；序号属于既有资源契约，槽内技能可以互换。</summary>
public enum CombatAction { Basic = 0, Dash = 1, Sweep = 2, Dodge = 3, Skill3 = 4, Skill4 = 5 }

/// <summary>技能请求的动作语义，与输入槽位和武将身份分开。</summary>
public enum CombatMotion { Basic = 0, Dash = 1, Sweep = 2, Dodge = 3 }

/// <summary>只有 Attack 参与暴击与受击被动；派生伤害不会互相触发。</summary>
public enum DamageKind { Direct = 0, Attack = 1, Reflected = 2, Counter = 3 }
