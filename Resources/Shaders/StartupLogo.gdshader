shader_type canvas_item;
render_mode unshaded;
void fragment() {
    vec4 ink=texture(TEXTURE,UV);
    float visible=smoothstep(0.28,0.62,max(ink.r,max(ink.g,ink.b)));
    COLOR=vec4(ink.rgb,ink.a*visible);
}
