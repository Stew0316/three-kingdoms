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
uniform vec4 exclude_region = vec4(0.0);
varying vec4 vertex_tint;
uniform vec4 art_tint : source_color = vec4(1.0);
uniform bool cloth_enabled = false;
uniform bool cloth_cape = false;
uniform vec4 cloth_region = vec4(0.0, 0.0, 1.0, 1.0);
uniform vec2 cloth_root = vec2(0.0);
uniform vec2 cloth_vertical_scale = vec2(1.0); // 根部高度以上、以下的纵向比例；默认保持原形。
uniform vec4 cloth_arc = vec4(0.0); // 原图中的弧线中心与椭圆半径；只用于已标定的柔性附件。
uniform float cloth_arc_width_scale = 1.0;
uniform float cloth_amplitude = 0.0;
uniform float cloth_weight = 0.0;
uniform float cloth_time = 0.0;
uniform float cloth_seed = 0.0;
uniform float cloth_rate = 1.0;
uniform float cloth_flex = 1.0;
uniform vec2 cloth_flow = vec2(-1.0, 0.0);
varying vec2 art_uv;
uniform bool shaft_stretch_enabled = false;
uniform vec4 weapon_region = vec4(0.0, 0.0, 1.0, 1.0);
uniform float weapon_source_split = 0.5;
uniform float weapon_display_split = 0.5;
void vertex() {
    vertex_tint = COLOR;
    art_uv = UV;
    if (cloth_enabled) {
        // 扩展绘制四边形但反向映射原图，给自由端留空间；静止时图像尺寸不变。
        vec2 root_uv = (cloth_region.xy + cloth_root) * TEXTURE_PIXEL_SIZE;
        VERTEX = cloth_root + (VERTEX - cloth_root) * 1.4;
        art_uv = root_uv + (UV - root_uv) * 1.4;
        // 先按最终上下长度扩展绘制边界；UV传递显示坐标，由片元反求原图，分界连续。
        if (cloth_vertical_scale != vec2(1.0)) {
            float vertex_y = VERTEX.y - cloth_root.y;
            VERTEX.y = cloth_root.y + vertex_y * (vertex_y < 0.0 ? cloth_vertical_scale.x : cloth_vertical_scale.y);
            float uv_y = art_uv.y - root_uv.y;
            art_uv.y = root_uv.y + uv_y * (uv_y < 0.0 ? cloth_vertical_scale.x : cloth_vertical_scale.y);
        }
    }
}
void fragment() {
    vec2 sample_uv = art_uv;
    if (shaft_stretch_enabled) {
        // 只重新分配刀头与直杆的纵向长度，连接处连续；X 不变，原图与身体骨骼不受影响。
        vec2 local = (art_uv / TEXTURE_PIXEL_SIZE - weapon_region.xy) / weapon_region.zw;
        local.y = local.y < weapon_display_split
            ? local.y * weapon_source_split / weapon_display_split
            : weapon_source_split + (local.y - weapon_display_split) * (1.0 - weapon_source_split) / (1.0 - weapon_display_split);
        sample_uv = (weapon_region.xy + local * weapon_region.zw) * TEXTURE_PIXEL_SIZE;
    }
    if (cloth_enabled) {
        vec2 point = art_uv / TEXTURE_PIXEL_SIZE - cloth_region.xy;
        if (cloth_vertical_scale != vec2(1.0)) {
            float offset_y = point.y - cloth_root.y;
            point.y = cloth_root.y + offset_y / (offset_y < 0.0 ? cloth_vertical_scale.x : cloth_vertical_scale.y);
        }
        vec2 relative = (point - cloth_root) / cloth_region.zw;
        // 披风整条领口固定，冠翎的冠座固定；越靠自由端越柔软。
        float distance_to_root = cloth_cape ? max(relative.y, 0.0) : length(relative);
        float freedom = pow(smoothstep(0.12, 0.9, distance_to_root), cloth_flex);
        float phase = cloth_time * cloth_rate * 6.2 - distance_to_root * 7.5 + cloth_seed;
        // 非整倍数波叠加形成连续不规则摆动，避免逐帧随机抖动。
        float wave = sin(phase) * .48 + sin(phase * 1.73 + relative.x * 5.0) * .22
            + sin(cloth_time * cloth_rate * 2.13 + relative.y * 3.1 + cloth_seed) * .12;
        vec2 cross_flow = vec2(-cloth_flow.y, cloth_flow.x);
        vec2 displacement = (cloth_flow * .55 + cross_flow * wave) * cloth_amplitude * cloth_weight * freedom;
        point -= displacement;
        if (cloth_arc.z > 0.0 && cloth_arc.w > 0.0 && cloth_arc_width_scale < 1.0) {
            // 沿椭圆径向收细羽带，保留拱形路径；挂点附近平滑恢复，根部与下垂尾梢不改。
            float influence = 1.0 - smoothstep(cloth_root.y - 36.0, cloth_root.y - 6.0, point.y);
            if (influence > 0.0) {
                float width = mix(1.0, cloth_arc_width_scale, influence);
                vec2 radial = (point + cloth_region.xy - cloth_arc.xy) / cloth_arc.zw;
                float radius = length(radial);
                float source_radius = 1.0 + (radius - 1.0) / width;
                if (source_radius < 0.0) discard;
                point = cloth_arc.xy + radial / max(radius, .001) * source_radius * cloth_arc.zw - cloth_region.xy;
            }
        }
        if (point.x < .5 || point.y < .5 || point.x > cloth_region.z - .5 || point.y > cloth_region.w - .5) discard;
        sample_uv = (cloth_region.xy + point) * TEXTURE_PIXEL_SIZE;
    }
    // 图集UV对应原图坐标；屏幕坐标向下为正，顺时针凸多边形内部叉积非负。
    vec2 pixel = sample_uv / TEXTURE_PIXEL_SIZE;
    if (exclude_region.z > 0.0 && pixel.x >= exclude_region.x && pixel.y >= exclude_region.y
        && pixel.x < exclude_region.x + exclude_region.z && pixel.y < exclude_region.y + exclude_region.w) discard;
    for (int i = 0; i < clip_count; i++) {
        vec2 a = clip_polygon[i];
        vec2 b = clip_polygon[(i + 1) % clip_count];
        vec2 edge = b - a;
        vec2 offset = pixel - a;
        if (edge.x * offset.y - edge.y * offset.x < 0.0) { discard; }
    }
    vec4 tex = texture(TEXTURE, sample_uv) * art_tint;
    float edge_alpha = min(min(texture(TEXTURE,sample_uv+vec2(TEXTURE_PIXEL_SIZE.x,0.0)).a,
        texture(TEXTURE,sample_uv-vec2(TEXTURE_PIXEL_SIZE.x,0.0)).a),
        min(texture(TEXTURE,sample_uv+vec2(0.0,TEXTURE_PIXEL_SIZE.y)).a,
        texture(TEXTURE,sample_uv-vec2(0.0,TEXTURE_PIXEL_SIZE.y)).a));
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
