using Godot;

/// <summary>强化技的表现控制器：Shader 保持人形/武器轮廓，GPUParticles2D 仅补充短促气流，不参与命中或数值。</summary>
public partial class EmpowerEffects2D : Node2D
{
    private Combatant _actor;
    private GpuParticles2D _particles;

    public override void _Ready()
    {
        _actor = GetParent<Combatant>();
        _particles = new GpuParticles2D
        {
            Name = "奋起气流粒子", Amount = 22, Lifetime = .55f, Emitting = false,
            VisibilityRect = new Rect2(-110, -140, 220, 210), ZIndex = 2
        };
        var material = new ParticleProcessMaterial
        {
            ParticleFlagDisableZ = true, Direction = Vector3.Up, Spread = 38,
            InitialVelocityMin = 26, InitialVelocityMax = 52, Gravity = Vector3.Zero,
            ScaleMin = .55f, ScaleMax = 1.15f
        };
        _particles.ProcessMaterial = material;
        _particles.Texture = MakeParticleTexture();
        AddChild(_particles);
    }

    public override void _Process(double delta)
    {
        if (_actor?.Presentation == null || _actor.Rig == null) return;
        var cfg = _actor.Presentation;
        float strength = Mathf.Clamp(_actor.Status.EmpowerRemaining / .18f, 0, 1);
        bool active = strength > 0;
        foreach (Sprite2D part in _actor.Rig.DrawOrderedParts)
            SetShader(part.Material as ShaderMaterial, cfg, strength, part == _actor.Rig.WeaponPart);
        SetShader(_actor.Rig.OffhandWeapon?.Material as ShaderMaterial, cfg, strength, true);
        bool trailing = cfg.EmpowerBlueTrail && active && _actor.WalkVelocity.LengthSquared() > 900;
        _particles.Emitting = active || trailing;
        var particleMaterial = _particles.ProcessMaterial as ParticleProcessMaterial;
        if (particleMaterial != null)
        {
            Color color = trailing ? new Color("4ebcff") : cfg.EmpowerColor;
            particleMaterial.Color = new Color(color, trailing ? .70f : .48f);
            particleMaterial.Direction = trailing ? new Vector3(-_actor.WalkVelocity.Normalized().X, -_actor.WalkVelocity.Normalized().Y, 0) : Vector3.Up;
        }
    }

    private static void SetShader(ShaderMaterial material, CharacterPresentationConfig cfg, float strength, bool weapon)
    {
        if (material == null) return;
        material.SetShaderParameter("empower_color", cfg.EmpowerColor);
        material.SetShaderParameter("empower_strength", cfg.EmpowerHumanAura ? strength : 0f);
        material.SetShaderParameter("weapon_glow", cfg.EmpowerWeaponGlow && weapon ? strength : 0f);
    }

    private static GradientTexture2D MakeParticleTexture()
    {
        var gradient = new Gradient();
        // 使用 Gradient 自己的逐点 API，避免不同 Godot C# 绑定对 Packed*Array 的命名差异。
        gradient.SetOffset(0, 0f);
        gradient.SetColor(0, Colors.White);
        gradient.SetOffset(1, 1f);
        gradient.SetColor(1, new Color(1, 1, 1, 0));
        return new GradientTexture2D { Gradient = gradient, Width = 16, Height = 16, Fill = GradientTexture2D.FillEnum.Radial };
    }
}
