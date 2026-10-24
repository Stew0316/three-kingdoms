using Godot;

/// <summary>强化气焰在人物前后包裹全身；只采样状态与表现时钟，不参与伤害和移动。</summary>
public partial class EmpowerEffects2D : Node2D
{
    private Combatant _actor;
    private ColorRect _back, _front;
    private ShaderMaterial _backMaterial, _frontMaterial;
    private GpuParticles2D _trailParticles; // 非全身气焰的武将继续保留原武器辉光/蓝色拖尾粒子。
    private float _startedAt;
    private bool _wasActive;
    public bool AuraVisible => _front?.Visible == true;
    public float SampleTime { get; private set; } // 暂停/打击停顿时随单位时钟冻结，供视觉回归读取。

    public override void _Ready()
    {
        _actor = GetParent<Combatant>();
        var shader = GD.Load<Shader>("res://Resources/Shaders/EmpowerAura.gdshader");
        _backMaterial = new ShaderMaterial { Shader = shader };
        _frontMaterial = new ShaderMaterial { Shader = shader };
        _frontMaterial.SetShaderParameter("front_layer", true);
        _back = Layer("AuraBack", _backMaterial);
        _front = Layer("AuraFront", _frontMaterial);
        // 和人物保持同一 Z 层，以兄弟顺序包住骨架；负 Z 会被地图盖住，高 Z 会穿过别的角色。
        AddChild(_front);
        var gradient = new Gradient();
        gradient.SetColor(0, Colors.White); gradient.SetColor(1, new Color(1, 1, 1, 0));
        _trailParticles = new GpuParticles2D
        {
            Name = "WeaponTrailParticles", Amount = 22, Lifetime = .55f, Emitting = false,
            Material = new CanvasItemMaterial { LightMode=CanvasItemMaterial.LightModeEnum.Unshaded },
            VisibilityRect = new Rect2(-110, -140, 220, 210),
            Texture = new GradientTexture2D { Gradient = gradient, Width = 16, Height = 16,
                Fill = GradientTexture2D.FillEnum.Radial },
            ProcessMaterial = new ParticleProcessMaterial { ParticleFlagDisableZ = true, Direction = Vector3.Up,
                Spread = 38, InitialVelocityMin = 26, InitialVelocityMax = 52, Gravity = Vector3.Zero,
                ScaleMin = .55f, ScaleMax = 1.15f }
        };
        AddChild(_trailParticles);
        CallDeferred(MethodName.PlaceBackLayer);
    }

    // 子节点先于 Combatant 就绪，延迟到父节点装配完毕后再插入背层。
    private void PlaceBackLayer()
    {
        _actor.AddChild(_back);
        _actor.MoveChild(_back, _actor.Rig.GetIndex());
    }

    private static ColorRect Layer(string name, ShaderMaterial material) => new()
    {
        Name = name, Material = material, Visible = false,
        MouseFilter = Control.MouseFilterEnum.Ignore
    };

    public override void _Process(double delta)
    {
        if (_actor?.Presentation == null || _actor.Rig == null) return;
        var cfg = _actor.Presentation;
        bool active = _actor.Status.EmpowerRemaining > 0 && !_actor.IsDead;
        if (active && !_wasActive) _startedAt = _actor.VisualTime;
        _wasActive = active;
        float elapsed = Mathf.Max(0, _actor.VisualTime - SampleTime);
        SampleTime = _actor.VisualTime;
        float strength = active ? Mathf.Clamp(_actor.Status.EmpowerRemaining / .22f, 0, 1) : 0;
        strength *= Mathf.SmoothStep(0, .10f, SampleTime - _startedAt);
        foreach (Sprite2D part in _actor.Rig.DrawOrderedParts)
            SetPartShader(part.Material as ShaderMaterial, cfg, strength, part == _actor.Rig.WeaponPart);
        SetPartShader(_actor.Rig.OffhandWeapon?.Material as ShaderMaterial, cfg, strength, true);
        bool trailing = cfg.EmpowerBlueTrail && _actor.WalkVelocity.LengthSquared() > 900;
        _trailParticles.Emitting = active && !cfg.EmpowerHumanAura;
        _trailParticles.SpeedScale = delta > 0 ? elapsed / (float)delta : 0;
        var particles = (ParticleProcessMaterial)_trailParticles.ProcessMaterial;
        particles.Color = new Color(trailing ? new Color("4ebcff") : cfg.EmpowerColor, trailing ? .70f : .48f);
        particles.Direction = trailing ? new Vector3(-_actor.WalkVelocity.Normalized().X, -_actor.WalkVelocity.Normalized().Y, 0) : Vector3.Up;
        _back.Visible = _front.Visible = active && cfg.EmpowerHumanAura;
        if (!_front.Visible) return;

        // 头骨为业务关节 2；从实际姿态/缩放取得头顶与脚点，不按固定世界半径画在脚底。
        Vector2 head = _actor.ToLocal(_actor.Rig.Joints[2].GlobalPosition);
        float rigScale = Mathf.Abs(_actor.Rig.Scale.Y);
        float height = Mathf.Max(24, -head.Y + 14 * rigScale);
        float width = height * 1.04f;
        Vector2 position = new(head.X * .30f - width * .5f, -height * 1.40f);
        Vector2 size = new(width, height * 1.44f);
        _back.Position = _front.Position = position;
        _back.Size = _front.Size = size;
        // 爆发先向外张开再收回持续升腾，持续时间仍由规则层唯一管理。
        float burst = Mathf.Exp(-Mathf.Max(0, SampleTime - _startedAt) * 6);
        foreach (var material in new[] { _backMaterial, _frontMaterial })
        {
            material.SetShaderParameter("aura_color", cfg.EmpowerColor);
            material.SetShaderParameter("phase", SampleTime);
            material.SetShaderParameter("strength", strength);
            material.SetShaderParameter("burst", burst);
        }
    }

    private static void SetPartShader(ShaderMaterial material, CharacterPresentationConfig cfg, float strength, bool weapon)
    {
        if (material == null) return;
        material.SetShaderParameter("empower_color", cfg.EmpowerColor);
        material.SetShaderParameter("empower_strength", cfg.EmpowerHumanAura ? strength * .65f : 0f);
        material.SetShaderParameter("weapon_glow", cfg.EmpowerWeaponGlow && weapon ? strength : 0f);
    }
}
