using Godot;
using System.Collections.Generic;

/// <summary>固定镜头三维演武场。唯一物理世界仍为 XY 平面，表现只做 XY→XZ 坐标映射。</summary>
public partial class Hd2DStage : Node3D
{
    public const float WorldScale = .02f;
    private Arena _arena;
    private readonly List<FighterView> _views = new();
    private readonly List<Ghost> _ghosts = new();
    private readonly Dictionary<uint,StandardMaterial3D> _materials=new(); // 同色环境物件共享材质。
    private sealed class FighterView
    {
        public Combatant Actor;
        public SubViewport Portrait;
        public Sprite3D Body;
        public MeshInstance3D Ground;
        public Node3D Particles;
        public float LastGhost;
    }
    private sealed class Ghost { public Sprite3D Sprite; public StandardMaterial3D Material; public float Remaining; public float Lifetime; public Color Tint; }
    public static Vector3 ToWorld(Vector2 point) => new((point.X-320)*WorldScale,0,(point.Y-210)*WorldScale);

    public override void _Ready()
    {
        _arena=GetParent<Arena>();
        _arena.GetNode<Node2D>(NodeNames.Fighters).Visible=false;
        BuildEnvironment();
        foreach(var actor in new[]{_arena.Player,_arena.Enemy}) BuildFighter(actor);
    }
    private StandardMaterial3D Material(Color color, bool glow=false)
    {
        uint key=color.ToRgba32()^(glow ? 1u : 0u);
        if(_materials.TryGetValue(key,out var material)) return material;
        material=new StandardMaterial3D { AlbedoColor=color,Roughness=.86f,EmissionEnabled=glow,Emission=color,EmissionEnergyMultiplier=glow ? 1.8f : 0 };
        _materials[key]=material; return material;
    }
    private MeshInstance3D Box(Vector3 position,Vector3 size,Color color)
    {
        var mesh=new MeshInstance3D { Position=position, Mesh=new BoxMesh { Size=size }, MaterialOverride=Material(color) };
        AddChild(mesh); return mesh;
    }
    private void BuildEnvironment()
    {
        var camera=new Camera3D { Name="固定斜俯视镜头", Projection=Camera3D.ProjectionType.Orthogonal,
            Size=14.3f, KeepAspect=Camera3D.KeepAspectEnum.Width, Position=new(0,10,12), Current=true };
        AddChild(camera); camera.LookAt(new Vector3(0,0,-.35f));
        var env=new Godot.Environment { BackgroundMode=Godot.Environment.BGMode.Color, BackgroundColor=new("17282f"),
            AmbientLightSource=Godot.Environment.AmbientSource.Color, AmbientLightColor=new("afc9d3"), AmbientLightEnergy=.38f,
            TonemapMode=Godot.Environment.ToneMapper.Filmic, GlowEnabled=true, GlowIntensity=.65f,
            FogEnabled=true, FogLightColor=new("253941"), FogDensity=.005f, FogSkyAffect=0 };
        AddChild(new WorldEnvironment { Environment=env });
        var sun=new DirectionalLight3D { RotationDegrees=new(-53,-30,0), LightColor=new("ffdc9e"), LightEnergy=1.65f, ShadowEnabled=true };
        AddChild(sun);
        Box(new(0,-.3f,0),new(12.6f,.6f,5.1f),new("343d3e"));
        Box(new(0,-.72f,0),new(200,.15f,200),new("18292c"));
        var stoneShader=new Shader { Code="""
            shader_type spatial;
            uniform vec4 stone_color : source_color = vec4(0.3,0.35,0.35,1.0);
            varying vec3 world_position;
            float noise(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
            void vertex() { world_position=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; }
            void fragment() {
                float grain=noise(floor(world_position.xz*150.0));
                float patch=noise(floor(world_position.xz*13.0));
                float edge=smoothstep(0.0,0.045,min(min(UV.x,1.0-UV.x),min(UV.y,1.0-UV.y)));
                ALBEDO=stone_color.rgb*(0.79+grain*0.13+patch*0.1)*mix(0.64,1.0,edge);
                ROUGHNESS=0.93;
            }
            """ };
        using var rng=new RandomNumberGenerator { Seed=930 };
        var tiles=new MultiMesh { TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,UseColors=true,
            Mesh=new BoxMesh { Size=new(.485f,.055f,.385f) },InstanceCount=264 };
        AddChild(new MultiMeshInstance3D { Multimesh=tiles,MaterialOverride=new ShaderMaterial { Shader=stoneShader } });
        for(int z=0;z<11;z++) for(int x=0;x<24;x++)
        {
            float v=rng.RandfRange(-.035f,.035f);
            int index=z*24+x;
            tiles.SetInstanceTransform(index,new Transform3D(Basis.Identity,new(-5.75f+x*.5f,.012f,-2.05f+z*.4f)));
            tiles.SetInstanceColor(index,new Color(.32f+v,.37f+v,.37f+v));
        }
        // 边墙、柱、飞檐和旗帜构成真实遮挡与阴影；舞台之外不参与战斗碰撞。
        Box(new(0,.34f,-2.43f),new(12.4f,.7f,.25f),new("465254"));
        Box(new(0,.72f,-2.43f),new(12.7f,.12f,.42f),new("7f867c"));
        foreach(float x in new[]{-6.08f,6.08f})
        {
            Box(new(x,.22f,0),new(.24f,.44f,4.8f),new("465254"));
            for(int z=0;z<4;z++)
            {
                var p=new Vector3(x,.6f,-2.2f+z*1.45f);
                Box(p,new(.38f,1.2f,.38f),new("586364"));
                Box(p+new Vector3(0,.67f,0),new(.51f,.16f,.51f),new("9a9580"));
            }
        }
        foreach(float x in new[]{-4.7f,4.7f})
        {
            Box(new(x,1.5f,-2.6f),new(.13f,3,.13f),new("3a2e26"));
            Box(new(x+.36f,2.32f,-2.6f),new(.72f,.95f,.035f),new(x<0 ? "702d30" : "284c42"));
            Box(new(x+.36f,2.83f,-2.6f),new(.92f,.06f,.08f),new("b09b6b"));
        }
        Box(new(0,1.8f,-3.25f),new(4.3f,.22f,1.2f),new("302f2b"));
        Box(new(0,2.08f,-3.25f),new(4.7f,.18f,1.4f),new("445353"));
        foreach(float x in new[]{-1.8f,1.8f}) Box(new(x,.95f,-3.25f),new(.24f,1.7f,.24f),new("573b30"));
        foreach(float x in new[]{-5.3f,5.3f})
        {
            Box(new(x,.38f,-1.75f),new(.32f,.76f,.32f),new("332e29"));
            AddChild(new OmniLight3D { Position=new(x,1,-1.75f), LightColor=new("ff9f4c"),LightEnergy=2,OmniRange=3 });
            Box(new(x,.87f,-1.75f),new(.2f,.18f,.2f),new("ffc477")).MaterialOverride=Material(new("ffae54"),true);
        }
        // 内外双圈嵌在石台表面，提供场地尺度参照。
        foreach(float r in new[]{1.5f,1.56f})
        {
            var ring=new MeshInstance3D { Position=new(0,.07f,0),Mesh=new TorusMesh { InnerRadius=r,OuterRadius=r+.02f,Rings=64,RingSegments=6 },MaterialOverride=Material(new("9b9270")) };
            AddChild(ring);
        }
        // 台阶和后排立柱加强纵深，均处于可战斗平台之外。
        for(int i=0;i<3;i++) Box(new(0,-.22f-i*.15f,2.7f+i*.3f),new(3.8f+i*.3f,.18f,.4f),new("4b5757"));
        for(int i=0;i<9;i++)
        {
            float x=-8+i*2;
            Box(new(x,1.4f,-5),new(.35f,3,.35f),new("233338"));
            Box(new(x,2.95f,-5),new(2.15f,.2f,.6f),new("2e4145"));
        }
    }
    private SubViewport Viewport(string name,int size)
    {
        var vp=new SubViewport { Name=name,Size=new(size,size),TransparentBg=true,Disable3D=true,
            RenderTargetUpdateMode=SubViewport.UpdateMode.Always,RenderTargetClearMode=SubViewport.ClearMode.Always,OwnWorld3D=false };
        vp.World2D=new World2D(); AddChild(vp); return vp;
    }
    private void BuildFighter(Combatant actor)
    {
        var portrait=Viewport(actor.Name+"骨骼视口",512);
        var canvas=new Node2D { Position=new(256,384),Scale=new(4,4) }; portrait.AddChild(canvas);
        actor.Rig.Reparent(canvas,false);
        actor.GetNode<CombatFeedback>(NodeNames.Feedback).Reparent(canvas,false);
        var body=new Sprite3D { Name=actor.Name+"二维人物",Texture=portrait.GetTexture(),PixelSize=.005f,
            Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,Shaded=false,NoDepthTest=false,AlphaCut=SpriteBase3D.AlphaCutMode.Discard,AlphaScissorThreshold=.08f };
        AddChild(body);
        var groundVp=Viewport(actor.Name+"地面视口",512);
        var groundCanvas=new Node2D { Position=new(256,256),Scale=new(2,2) }; groundVp.AddChild(groundCanvas);
        groundCanvas.AddChild(new GroundEffects { Actor=actor });
        actor.Trail.Reparent(groundCanvas,false);
        var ground=new MeshInstance3D { Mesh=new PlaneMesh { Size=new(5.12f,5.12f) },
            MaterialOverride=new StandardMaterial3D { AlbedoTexture=groundVp.GetTexture(),Transparency=BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,CullMode=BaseMaterial3D.CullModeEnum.Disabled,NoDepthTest=false } };
        AddChild(ground);
        var particles=new Node3D(); AddChild(particles);
        var cfg=actor.Presentation;
        for(int i=0;i<(cfg?.ParticleCount ?? 24);i++)
        {
            var m=new MeshInstance3D { Mesh=new QuadMesh { Size=new(.025f,.075f) },
                MaterialOverride=new StandardMaterial3D { AlbedoColor=cfg?.ParticleColor ?? Colors.White,
                    ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,BillboardMode=BaseMaterial3D.BillboardModeEnum.Enabled,
                    Transparency=BaseMaterial3D.TransparencyEnum.Alpha,EmissionEnabled=true,Emission=cfg?.ParticleColor ?? Colors.White,EmissionEnergyMultiplier=2 } };
            particles.AddChild(m);
        }
        _views.Add(new FighterView { Actor=actor,Portrait=portrait,Body=body,Ground=ground,Particles=particles });
    }
    public override void _Process(double delta)
    {
        foreach(var view in _views)
        {
            var actor=view.Actor; var cfg=actor.Presentation; Vector3 p=ToWorld(actor.Position);
            // 相机 up 为 (0,.777,-.63)，纹理中心到脚底为 .64 世界单位。
            view.Body.Position=p+new Vector3(0,.55f,-.40f);
            view.Ground.Position=p+new Vector3(0,.064f,0);
            view.Particles.Position=p; view.Particles.Visible=!actor.IsDead;
            float t=actor.VisualTime;
            for(int i=0;i<view.Particles.GetChildCount();i++)
            {
                var particle=view.Particles.GetChild<MeshInstance3D>(i);
                float phase=(t*.65f+i*.618f)%1,angle=i*2.4f+t*(cfg?.OrbitParticles==true ? 1.7f : .25f);
                float r=(cfg?.AuraRadius ?? 21)*WorldScale;
                particle.Position=new(Mathf.Cos(angle)*r,.06f+phase*(cfg?.OrbitParticles==true ? .22f : .65f),Mathf.Sin(angle)*r);
                particle.Scale=Vector3.One*Mathf.Sin(phase*Mathf.Pi)*(cfg?.OrbitParticles==true ? 1.5f : 1);
                particle.RotationDegrees=new(0,0,cfg?.OrbitParticles==true ? angle*50 : 0);
            }
            if(cfg?.Afterimages==true && !actor.IsDead && !actor.BattleFinished && actor.Velocity.Length()>30 && t-view.LastGhost>=cfg.AfterimageInterval)
            {
                view.LastGhost=t;
                // 每次复制当前已渲染姿态，残影停留世界原位，不继续播放角色动作。
                if(DisplayServer.GetName()!="headless")
                {
                    using var snapshot=view.Portrait.GetTexture().GetImage();
                    var texture=ImageTexture.CreateFromImage(snapshot);
                    var material=new StandardMaterial3D { AlbedoTexture=texture,AlbedoColor=cfg.AfterimageColor,
                        ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,Transparency=BaseMaterial3D.TransparencyEnum.Alpha,BlendMode=BaseMaterial3D.BlendModeEnum.Add,
                        BillboardMode=BaseMaterial3D.BillboardModeEnum.Enabled,EmissionEnabled=true,Emission=new Color(.05f,.35f,1),EmissionEnergyMultiplier=.65f };
                    var ghost=new Sprite3D { Texture=texture,PixelSize=.005f,MaterialOverride=material,
                        Position=view.Body.Position,Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,Modulate=cfg.AfterimageColor,NoDepthTest=false };
                    AddChild(ghost); _ghosts.Add(new Ghost { Sprite=ghost,Material=material,Remaining=cfg.AfterimageLifetime,Lifetime=cfg.AfterimageLifetime,Tint=cfg.AfterimageColor });
                }
            }
        }
        for(int i=_ghosts.Count-1;i>=0;i--)
        {
            var ghost=_ghosts[i]; ghost.Remaining-=(float)delta;
            if(ghost.Remaining<=0 || _arena.Finished) { ghost.Sprite.QueueFree(); _ghosts.RemoveAt(i); }
            else ghost.Material.AlbedoColor=new Color(ghost.Tint,ghost.Tint.A*ghost.Remaining/ghost.Lifetime);
        }
    }
}
