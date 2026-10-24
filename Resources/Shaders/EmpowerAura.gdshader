shader_type canvas_item;
render_mode unshaded;

// phase 只接受角色 VisualTime；不使用 TIME，以便菜单暂停与打击停顿能冻结气焰。
uniform vec4 aura_color : source_color = vec4(1.0, 0.75, 0.08, 1.0);
uniform float phase = 0.0;
uniform float strength = 0.0;
uniform float burst = 0.0;
uniform bool front_layer = false;

float hash21(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}
float noise21(vec2 p) {
    vec2 i = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash21(i), hash21(i + vec2(1.0, 0.0)), f.x),
        mix(hash21(i + vec2(0.0, 1.0)), hash21(i + vec2(1.0, 1.0)), f.x), f.y);
}
void fragment() {
    float h = 1.0 - UV.y;
    float x = (UV.x - 0.5) * 2.0;
    // 向上平移噪声与细流，侧焰连贯地经过腿、腰、肩，焰尖在头顶以上散开。
    vec2 flow = vec2(x * 5.0, UV.y * 6.0 + phase * 3.6);
    float broad = noise21(flow);
    float fine = noise21(flow * 2.7 + vec2(6.2, phase * 1.3));
    float warp = sin(h * 16.0 - phase * 7.0) * .035 + (broad - .5) * .12;
    float side = abs(x + warp);
    float taper = 1.0 - smoothstep(.56, 1.02, h);
    float width = (.30 + .36 * sin(h * 2.8)) * taper + .07;
    width += (broad - .5) * .27 + (fine - .5) * .07 + burst * .10;
    float envelope = 1.0 - smoothstep(width - .09, width + .10, side);
    float base = smoothstep(.015, .11, h);
    float top = 1.0 - smoothstep(.85 + (broad - .5) * .14, 1.0, h);
    float rim = exp(-pow((side - width + .045) * 24.0, 2.0));
    float plume = smoothstep(.38, .76, broad * .65 + fine * .35);
    float streak = pow(max(0.0, sin(x * 43.0 + broad * 7.0 + h * 4.0)), 14.0);
    float stream = streak * smoothstep(.20, .60, fine) * envelope;
    float glow = exp(-pow(side / max(.10, width + .12), 4.0)) * .12;
    // 交错长焰舌从周身底部向上冲，越过肩头后拉尖消散，避免形成光滑的蛋形描边。
    float tongues = 0.0;
    for (int i = 0; i < 10; i++) {
        float seed = float(i);
        float rise = fract(phase * (.68 + mod(seed, 3.0) * .09) + seed * .618);
        float tip = .28 + rise * .75;
        float tail = tip - .30;
        float along = clamp((h - tail) / .30, 0.0, 1.0);
        float lane = (mod(seed, 2.0) < 1.0 ? -1.0 : 1.0) * (.34 + mod(seed, 5.0) * .052);
        lane *= 1.0 - smoothstep(.65, 1.0, h) * .70;
        lane += sin(h * 15.0 - phase * 7.0 + seed * 2.1) * .055;
        float thickness = mix(.065, .004, along);
        float flame = (1.0 - smoothstep(thickness * .30, thickness, abs(x - lane)));
        flame *= smoothstep(tail, tail + .09, h) * (1.0 - smoothstep(tip - .015, tip, h));
        tongues += flame * sin(rise * 3.14159265);
    }
    float density = envelope * (.12 + plume * .32) + rim * .42 + stream * .38 + glow + tongues * .75;
    if (front_layer) {
        // 身前薄焰只显侧缘与穿行细流，让胸甲、脸和武器仍然可读。
        float clear_body = smoothstep(.16, .42, side);
        density = (rim * .27 + envelope * plume * .18 + tongues * .42) * clear_body + stream * .19;
    }
    vec3 hot = mix(aura_color.rgb, vec3(1.0, .95, .66), .30);
    vec3 color = mix(aura_color.rgb, hot, clamp(rim * .50 + stream + tongues * .5, 0.0, 1.0));
    COLOR = vec4(color, clamp(density * base * top * strength, 0.0, .92));
}
