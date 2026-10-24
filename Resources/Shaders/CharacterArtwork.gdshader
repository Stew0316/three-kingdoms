shader_type canvas_item;
render_mode blend_mix; // 人物接受 CanvasModulate 与局部灯光；技能气焰使用独立不受光材质。
uniform float color_steps = 7.0;
uniform float saturation = 1.15;
uniform vec4 outline_color : source_color = vec4(0.10,0.08,0.07,0.92);
uniform bool cartoon_enabled = true;
uniform vec4 empower_color : source_color = vec4(1.0,0.2,0.2,1.0);
uniform float empower_strength = 0.0;
uniform float weapon_glow = 0.0;
uniform int clip_count = 0;
uniform vec2 clip_polygon[16];
varying vec4 vertex_tint;
void vertex() { vertex_tint = COLOR; }
void fragment() {
    // 图集UV对应原图坐标；屏幕坐标向下为正，顺时针凸多边形内部叉积非负。
    vec2 pixel = UV / TEXTURE_PIXEL_SIZE;
    for (int i = 0; i < clip_count; i++) {
        vec2 a = clip_polygon[i];
        vec2 b = clip_polygon[(i + 1) % clip_count];
        vec2 edge = b - a;
        vec2 offset = pixel - a;
        if (edge.x * offset.y - edge.y * offset.x < 0.0) { discard; }
    }
    vec4 tex = texture(TEXTURE, UV);
    float edge_alpha = min(min(texture(TEXTURE,UV+vec2(TEXTURE_PIXEL_SIZE.x,0.0)).a,
        texture(TEXTURE,UV-vec2(TEXTURE_PIXEL_SIZE.x,0.0)).a),
        min(texture(TEXTURE,UV+vec2(0.0,TEXTURE_PIXEL_SIZE.y)).a,
        texture(TEXTURE,UV-vec2(0.0,TEXTURE_PIXEL_SIZE.y)).a));
    float luminance = dot(tex.rgb, vec3(0.299,0.587,0.114));
    vec3 saturated = mix(vec3(luminance),tex.rgb,saturation);
    vec3 posterized = floor(clamp(saturated,0.0,1.0)*color_steps+0.5)/color_steps;
    float inner_edge = tex.a>0.08 ? 1.0-smoothstep(0.05,0.42,edge_alpha) : 0.0;
    vec3 color = mix(posterized,outline_color.rgb,inner_edge*outline_color.a);
    // 在每个拆件边缘叠加同一强化色，组合起来仍沿人体与武器轮廓，而非画成圆形光圈。
    color = cartoon_enabled ? color : tex.rgb;
    float aura = inner_edge * empower_strength;
    color = mix(color, empower_color.rgb, aura);
    color += empower_color.rgb * weapon_glow * (0.28 + inner_edge * .72);
    // fragment 的输入 COLOR 已乘过纹理；使用顶点颜色避免贴图二次相乘而发黑。
    COLOR = vec4(color,tex.a)*vertex_tint;
}
