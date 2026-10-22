shader_type canvas_item;
render_mode unshaded, blend_add;

// 日光权重：0 月夜、1 白天；与表盘中的日月图案共用过渡。
uniform float daylight : hint_range(0.0, 1.0) = 1.0;
// 独立表现相位，弧度；由 C# 推进，暂停时停止，不改变全局时辰。
uniform float drift_phase = 0.0;
// 绘制区域宽度，逻辑像素；表圈半径保持 22 像素。
uniform float extent = 96.0;

float seed_value(float index) {
    return fract(sin(index * 127.1 + 71.7) * 43758.5453);
}

void fragment() {
    vec2 point = (UV - vec2(0.5)) * extent;
    float radius = length(point);
    float angle = atan(point.y, point.x);
    // 收窄后的淡柔影只做衬底；主体是各自运动、短而柔软的光丝。
    float alpha = exp(-0.5 * pow((radius - 23.0) / 3.6, 2.0)) * 0.055;

    // 小光圈位于细光丝根部，单独保持完整、柔和且较窄。
    alpha += exp(-0.5 * pow((radius - 23.1) / 0.75, 2.0)) * 0.14;
    for (int index = 0; index < 40; index++) {
        float id = float(index);
        float seed = seed_value(id);
        float phase = seed * 6.2831853;
        // 整数频率保证表现相位绕回时连续；各缕速度不同，避免整齐摆动。
        float pace = 2.0 + mod(id, 3.0);
        float motion = drift_phase * pace + phase;
        float length_pixels = 3.6 + seed_value(id + 31.0) * 4.0 + sin(motion) * 0.45;
        // 每缕根部停留在铜圈附近，末梢逐渐弯曲；不整圈同步转动或像旗面起伏。
        float along = clamp((radius - 22.3) / length_pixels, 0.0, 1.0);
        // 根部保持细密、略不均匀的间距，减少相邻光丝挤成大块亮斑。
        float root = id * 6.2831853 / 40.0 + (seed - 0.5) * 0.10;
        root += sin(motion) * 0.045;
        float bend = sin(motion + along * 2.3) * 0.23
                   + sin(drift_phase * (pace + 1.0) - phase + along * 5.0) * 0.09;
        float direction = root + bend * along + (seed - 0.5) * along * along * 0.28;
        float separation = atan(sin(angle - direction), cos(angle - direction)) * radius;
        // 亚像素细芯外包柔光，越接近末梢越细；根部和末梢都渐隐，避免硬射线。
        float thickness = mix(0.46, 0.16, along);
        float core = exp(-0.5 * pow(separation / thickness, 2.0));
        float blur = exp(-0.5 * pow(separation / (thickness + 0.7), 2.0));
        float tips = smoothstep(22.0, 23.3, radius)
                   * (1.0 - smoothstep(0.40, 1.0, along))
                   * (1.0 - smoothstep(22.3 + length_pixels - 0.5, 22.3 + length_pixels, radius));
        // 多组错相淡入淡出，让亮度和长度不均匀，但避免快速闪烁。
        float life = 0.40 + 0.60 * pow(0.5 + 0.5 * sin(drift_phase * 2.0 + phase), 2.0);
        alpha += (core * 0.22 + blur * 0.065) * tips * life;
    }

    // 限制在表圈外侧，远端完全透明，保留约为初版一半的扩散宽度。
    alpha *= smoothstep(21.6, 22.9, radius) * (1.0 - smoothstep(31.0, 35.0, radius));
    vec3 tint = mix(vec3(0.34, 0.64, 1.0), vec3(1.0, 0.74, 0.32), daylight);
    COLOR = vec4(tint, min(alpha, 0.42));
}
