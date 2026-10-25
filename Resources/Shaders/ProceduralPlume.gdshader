shader_type canvas_item;
render_mode blend_mix;
uniform float motion_time = 0.0;
uniform float motion_weight = 0.0;
uniform float rate = 1.0;
uniform float amplitude = 6.0;
uniform float phase = 0.0;
uniform vec2 flow = vec2(-1.0, 0.0);
varying vec4 tint;
float brush_noise(vec2 p) {
    vec2 grid = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    vec2 h = vec2(127.1, 311.7);
    float a = fract(sin(dot(grid,h))*43758.5453);
    float b = fract(sin(dot(grid+vec2(1,0),h))*43758.5453);
    float c = fract(sin(dot(grid+vec2(0,1),h))*43758.5453);
    float d = fract(sin(dot(grid+vec2(1,1),h))*43758.5453);
    return mix(mix(a,b,f.x),mix(c,d,f.x),f.y);
}
void vertex() {
    tint = COLOR;
    float along = UV.x;
    // 上段保留拱形承托，下段和回卷尾梢明显柔软；根部严格固定。
    float free_end = .07 * smoothstep(0.0, .5, along)
        + .93 * pow(smoothstep(.38, 1.0, along), 1.4);
    float wave = motion_time * rate * 5.8 - along * 7.0 + phase;
    vec2 across_flow = vec2(-flow.y, flow.x);
    // 波沿长度传播，根部位移严格为零；每条网格独立频率、幅度与相位。
    VERTEX += motion_weight * free_end * (flow * (2.5 + sin(wave * .61) * 1.2)
        + across_flow * amplitude * (sin(wave) + .28 * sin(wave * 1.71 + along * 3.0)));
}
void fragment() {
    float across = abs(UV.y);
    float vanes = UV.x * 110.0 + across * 14.0 + sin(UV.x * 19.0) * .8;
    float branches = .5 + .5 * sin(vanes * 6.283185);
    float grain = sin(UV.x * 389.0 + UV.y * 37.0) * sin(UV.y * 83.0 - UV.x * 71.0);
    float brush = brush_noise(vec2(UV.x * 55.0 + across * 5.0, UV.y * 7.0));
    float light = .36 + .24 * (1.0 - across) + .12 * sin(UV.x * 12.0 - UV.y * 1.7)
        + .10 * branches + .05 * grain + .16 * brush;
    vec3 base = mix(vec3(.20,.018,.012), vec3(.76,.105,.065), light);
    base *= 1.0 - .5 * smoothstep(.78,1.0,across);
    base += vec3(.10,.023,.012) * exp(-pow((UV.y + .17) * 9.0, 2.0));
    float alpha = 1.0 - smoothstep(1.0 - max(fwidth(UV.y), .012), 1.0, across);
    COLOR = vec4(base, alpha) * tint;
}
