using Godot;

/// <summary>角色表现参数，特效关闭或修改不影响战斗判定。</summary>
[GlobalClass]
public partial class CharacterPresentationConfig : Resource
{
    [Export] public Texture2D PartsAtlas { get; set; } // 4×4 拆件图集，顺序见资源 README。
    [Export] public Color AuraColor { get; set; } = new("ffb34f");
    [Export] public Color ParticleColor { get; set; } = new("ffe5ad");
    [Export] public bool OrbitParticles { get; set; } // true 为环绕叶片，false 为上升火星。
    [Export] public bool Afterimages { get; set; } // 移动时采样当前骨骼姿态。
    [Export] public Color AfterimageColor { get; set; } = new(.2f, .55f, 1f, .42f);
    [Export] public float AfterimageInterval { get; set; } = .075f;
    [Export] public float AfterimageLifetime { get; set; } = .32f;
    [Export] public float AuraRadius { get; set; } = 21;
    [Export] public int ParticleCount { get; set; } = 24;
    [Export] public float MotionStrength { get; set; } = 1;
}
