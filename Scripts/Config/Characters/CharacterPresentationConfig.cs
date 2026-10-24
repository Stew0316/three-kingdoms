using Godot;

/// <summary>表现组合入口；每个武将引用自己的模型、动作资源，效果不影响规则。</summary>
[GlobalClass]
public partial class CharacterPresentationConfig : Resource
{
    [Export] public CharacterModelConfig Model { get; set; } = new(); // 当前武将独立模型绑定。
    [Export] public CharacterAnimationConfig Animation { get; set; } = new(); // 当前武将独立动作集。
    [Export] public Color AuraColor { get; set; } = new("ffb34f");
    [Export] public Color ParticleColor { get; set; } = new("ffe5ad");
    [Export] public bool OrbitParticles { get; set; } // true 为环绕叶片，false 为上升火星。
    [Export] public bool Afterimages { get; set; } // 移动时采样当前骨骼姿态。
    [Export] public Color AfterimageColor { get; set; } = new(.2f, .55f, 1f, .42f);
    [Export] public float AfterimageInterval { get; set; } = .075f;
    [Export] public float AfterimageLifetime { get; set; } = .32f;
    [Export] public float AuraRadius { get; set; } = 21;
    [Export] public int ParticleCount { get; set; } = 24;
    [Export] public Color EmpowerColor { get; set; } = new("ff3d3d"); // 自身强化的全身气焰主色，吕布金黄、关羽赤红。
    [Export] public bool EmpowerHumanAura { get; set; } // 在人物前后绘制持续上升的全身气焰，并辅以拆件边缘受光。
    [Export] public bool EmpowerWeaponGlow { get; set; } // 强化期间仅武器增加白色辉光。
    [Export] public bool EmpowerBlueTrail { get; set; } // 强化且移动时产生蓝色残影与粒子拖尾。
}
