using Godot;

/// <summary>马匹缩小采样：多级纹理抑制毛发闪烁，轻微偏向较细一级以保留鞍具和脸部边缘。</summary>
public static class HorseSurfaceMaterial
{
    // 仅作用于马图集。-0.4是在正式1.7屏幕缩放下与8倍超采样对照后的取值，不作屏幕锐化。
    // 顶点颜色单独传入，避免fragment中的COLOR已含纹理后又重复相乘，保留远腿明暗和根部渐隐。
    public static ShaderMaterial Shared { get; } = new()
    {
        Shader = new Shader { Code = @"
shader_type canvas_item;
varying vec4 vertex_tint;
void vertex() { vertex_tint = COLOR; }
void fragment() {
    COLOR = texture(TEXTURE, UV, -0.4) * vertex_tint;
    if (COLOR.a < 0.035) discard;
}" }
    };
}
