shader_type canvas_item;
render_mode unshaded;

// 全局昼夜比例 [0,1)；不用 TIME，暂停时天空与日月同时停止。
uniform float cycle : hint_range(0.0, 1.0) = 0.25;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}
float noise(vec2 p) {
    vec2 cell = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(cell), hash(cell + vec2(1.0, 0.0)), f.x),
        mix(hash(cell + vec2(0.0, 1.0)), hash(cell + vec2(1.0)), f.x), f.y);
}
void fragment() {
    vec2 p = (UV - 0.5) * 2.0;
    float radius = length(p);
    float angle = cycle * TAU;
    float altitude = sin(angle);
    float day = smoothstep(-0.18, 0.18, altitude);
    float dusk = 1.0 - smoothstep(0.0, 0.42, abs(altitude));
    // 球面压缩与暗边表现内嵌深度；云层随全局时间缓慢回转，周期首尾连续。
    vec2 sphere = p / (1.2 + sqrt(max(0.0, 1.0 - dot(p, p))) * 0.4);
    vec2 drift = vec2(cos(angle), sin(angle)) * 0.65;
    float cloud = noise(sphere * 4.0 + drift) * 0.60
                + noise(sphere * 9.0 + drift) * 0.27
                + noise(sphere * 19.0 + drift) * 0.13;
    vec3 sky = mix(vec3(0.014, 0.025, 0.095), vec3(0.09, 0.32, 0.57), day);
    sky += mix(vec3(0.12, 0.10, 0.24), vec3(0.33, 0.42, 0.43), day)
         * smoothstep(0.35, 0.82, cloud);
    sky += vec3(0.38, 0.16, 0.13) * dusk * smoothstep(-0.5, 0.9, p.y) * 0.6;
    vec2 stars = mat2(vec2(cos(angle), sin(angle)), vec2(-sin(angle), cos(angle))) * sphere;
    for(int i=0; i<38; i++) {
        float id = float(i);
        vec2 star = vec2(hash(vec2(id, 2.0)), hash(vec2(id, 7.0))) * 1.7 - 0.85;
        float size = mix(0.004, 0.013, hash(vec2(id, 11.0)));
        float dot_light = 1.0 - smoothstep(size, size + 0.014, length(stars - star));
        sky += vec3(0.72, 0.85, 1.0) * dot_light * (1.0 - day * 0.94);
    }
    // 日月相隔半周，从左升、经上方、向右落；下半周藏于地平线后。
    vec2 sun = vec2(-cos(angle) * 0.96, 0.27 - altitude * 0.87);
    vec2 moon = vec2(cos(angle) * 0.96, 0.27 + altitude * 0.87);
    float sun_visible = smoothstep(-0.12, 0.10, altitude);
    float moon_visible = 1.0 - smoothstep(-0.10, 0.12, altitude);
    float sd = length(p - sun);
    sky += vec3(1.0, 0.61, 0.12) * exp(-sd * sd * 20.0) * sun_visible * 0.75;
    float sun_disc = 1.0 - smoothstep(0.14, 0.16, sd);
    sky = mix(sky, vec3(1.0, 0.94, 0.60), sun_disc * sun_visible);
    vec2 m = p - moon;
    float moon_disc = 1.0 - smoothstep(0.17, 0.19, length(m));
    float cutout = smoothstep(0.155, 0.175, length(m - vec2(0.085, -0.045)));
    sky += vec3(0.19, 0.40, 0.75) * exp(-dot(m,m) * 15.0) * moon_visible * 0.38;
    sky = mix(sky, vec3(0.84, 0.94, 1.0), moon_disc * cutout * moon_visible);
    sky *= 1.0 - smoothstep(0.72, 1.05, radius) * 0.48;
    float glass = exp(-pow((length(p - vec2(-0.08, 0.04)) - 0.90) / 0.018, 2.0));
    sky += vec3(0.52, 0.70, 0.92) * glass * smoothstep(0.1, 0.8, -p.y) * 0.18;
    COLOR = vec4(sky, 1.0 - smoothstep(0.98, 1.0, radius));
}
