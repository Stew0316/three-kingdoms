shader_type canvas_item;
render_mode unshaded;
uniform float ratio : hint_range(0.0, 1.0) = 1.0;
uniform bool is_mana = false;

void fragment() {
    vec2 sample_uv = UV;
    if (!is_mana) { sample_uv.x = mix(.27, .73, UV.x); }
    vec4 tex = texture(TEXTURE, sample_uv);
    if (!is_mana) {
        // 只选择红色剑芯，铜边与兽面不参与血量裁切；水位由实际生命比例决定。
        float crimson = smoothstep(.12, .32, tex.r - max(tex.g, tex.b));
        crimson *= 1.0 - smoothstep(.30, .55, tex.g / max(tex.r, .001));
        crimson *= step(.04, sample_uv.y) * (1.0 - step(.65, sample_uv.y));
        float level = mix(.65, .04, ratio);
        float empty = ratio <= 0.0 ? 1.0 : 1.0 - smoothstep(level - .003, level + .003, sample_uv.y);
        float luma = dot(tex.rgb, vec3(.299, .587, .114));
        tex.rgb = mix(tex.rgb, vec3(luma * .22 + .035), crimson * empty);
    }
    COLOR = tex;
}
