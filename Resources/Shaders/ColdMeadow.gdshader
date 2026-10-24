shader_type canvas_item;
varying vec2 world_position;
uniform vec2 path_points[15];
bool on_path(vec2 p) {
    bool inside=false;
    for(int i=0,j=14;i<15;j=i++) {
        vec2 a=path_points[i], b=path_points[j];
        if((a.y>p.y)!=(b.y>p.y) && p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x) inside=!inside;
    }
    return inside;
}
float hash(vec2 p) {
    vec3 q=fract(vec3(p.xyx)*.1031);
    q+=dot(q,q.yzx+33.33);
    return fract((q.x+q.y)*q.z);
}
float noise(vec2 p) {
    vec2 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
    return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);
}
void vertex() { world_position=VERTEX; }
void fragment() {
    vec2 p=world_position;
    vec3 grass=texture(TEXTURE,p/340.0).rgb;
    float detail=noise(p*.035)*.4+noise(p*.012)*.6;
    float cold=smoothstep(1090.0,1660.0,p.x)*(1.0-smoothstep(60.0,360.0,p.y));
    cold=max(cold,1.0-smoothstep(-300.0,100.0,p.y));
    float sparse=smoothstep(.12,.85,cold);
    vec3 earth=vec3(.30,.32,.30)*(0.7+detail*.6);
    vec3 base=mix(grass,earth,sparse*.78);
    float snow=smoothstep(.47,.7,detail+cold*.55)*smoothstep(.18,.8,cold);
    base=mix(base,vec3(.74,.79,.79)*(0.92+detail*.12),snow);
    if(!on_path(p)) base*=.68;
    float gate=(1.0-smoothstep(932.0,1000.0,p.x))*step(920.0,p.x)*step(242.0,p.y)*(1.0-step(320.0,p.y));
    base=mix(base,vec3(.69,.69,.57),gate);
    COLOR=vec4(base,1.0);
}
