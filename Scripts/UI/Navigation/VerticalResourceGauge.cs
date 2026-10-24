using Godot;

/// <summary>独立透明铁铜器物量表：生命由剑身红芯显示，蓝葫芦保留尚未启用的内力语义。</summary>
public partial class VerticalResourceGauge : Control
{
    private bool _isMana;
    private float _ratio = 1;
    private TextureRect _art;
    private ShaderMaterial _fillMaterial;

    /// <summary>为 true 时使用酒葫芦；内力尚未启用，不显示虚构数值或动态水位。</summary>
    public bool IsMana
    {
        get => _isMana;
        set { if (_isMana == value) return; _isMana = value; RefreshArtwork(); }
    }

    /// <summary>真实生命比例限制在 0–1；仅剑身红芯按高度退色，铁刃和铜饰保持完整。</summary>
    public float Ratio
    {
        get => _ratio;
        set
        {
            _ratio = Mathf.Clamp(value, 0, 1);
            _fillMaterial?.SetShaderParameter("ratio", _ratio);
        }
    }

    public string ValueText { get; set; } = ""; // 数值由父界面提供，长文字只出现在悬浮提示中。

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        CustomMinimumSize = new Vector2(32, 54);
        _fillMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Resources/Shaders/ResourceGauge.gdshader") };
        _art = new TextureRect { Name = "GaugeArtwork", ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale, TextureFilter = TextureFilterEnum.Linear,
            MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_art);
        _art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        RefreshArtwork();
    }

    private void RefreshArtwork()
    {
        if (_art == null) return;
        var texture = GD.Load<Texture2D>(IsMana
            ? "res://Source/UI/Icons/Gauges/ManaGourdV1.png"
            : "res://Source/UI/Icons/Gauges/HealthSwordV1.png");
        // 剑原图两侧留白较多，运行时 UV 收窄取景；保留原始 PNG 与 Alpha，不改写源素材。
        _art.Texture = texture;
        _art.Material = _fillMaterial;
        _fillMaterial.SetShaderParameter("is_mana", IsMana);
        _fillMaterial.SetShaderParameter("ratio", Ratio);
    }
}
