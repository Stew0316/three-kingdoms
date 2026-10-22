using Godot;

/// <summary>图集采样、材质与平级图层绑定；只处理角色外观，不参与动作或技能规则。</summary>
public partial class CharacterRig
{
    /// <summary>读取每格 alpha 包围盒建立 AtlasTexture，原图保持不变。</summary>
    private void BuildParts(Texture2D atlas)
    {
        using var image = atlas.GetImage();
        if (image.IsCompressed()) image.Decompress();
        // 即使关闭卡通滤镜也保持统一 Shader：强化时的人形描边与武器辉光不能依赖贴图替换。
        _cartoonMaterial=BuildCartoonMaterial(Actor.Presentation.Model);
        int cellW = image.GetWidth()/4, cellH = image.GetHeight()/4;
        var tiles = CharacterRigDefaults.AtlasTiles;
        Rect2[] rectangles = CharacterRigDefaults.CreatePartRectangles();
        if (Actor.Presentation.Model.CalibratedLegProportions)
        {
            // 只在绑定时校准体型；裙甲止于大腿中段，让膝关节和大腿连接可读。
            rectangles[0] = new Rect2(-10, -3, 20, 15);
            rectangles[1] = new Rect2(-11, -13, 22, 28); // 胸甲下缘覆盖腰带，转腰时不能露出背景裂缝。
            rectangles[2] = new Rect2(-6, -14, 12, 17);
            rectangles[9] = rectangles[11] = new Rect2(-4.5f, -2, 9, _thighLength + 3);
            rectangles[10] = rectangles[12] = new Rect2(-4.5f, -2, 10, _lowerLegLength + 2);
        }
        if (Actor.Presentation.Animation.ReferencePoseSet)
        {
            rectangles[0] = new Rect2(-10, -3, 20, 17);
            rectangles[1] = new Rect2(-10, -12, 20, 25);
            rectangles[2] = new Rect2(-5.5f, -10, 11, 14);
            rectangles[3] = rectangles[6] = new Rect2(-5, -3, 10, 16);
            rectangles[4] = rectangles[7] = new Rect2(-3.5f, -2, 7, 15);
            rectangles[5] = rectangles[8] = new Rect2(-2.5f, -2, 5, 6);
            rectangles[13] = new Rect2(-19, -2, 28, 49);
            rectangles[14] = new Rect2(-5, -42, 10, 86);
        }
        // 骨骼只负责变换；部件以兄弟节点顺序完成角色内部遮挡，全部使用 Z=0 参与整个人物的 YSort。
        // 持长兵器时远前臂也跨在胸前，不能与披风一起藏在躯干后面。
        int[] drawOrder = CharacterRigDefaults.CreateDrawOrder();
        if (Actor.Presentation.Model.PartRects.Count == CharacterRigDefaults.JointCount)
            for (int index = 0; index < CharacterRigDefaults.JointCount; index++) rectangles[index] = Actor.Presentation.Model.PartRects[index];
        if (Actor.Presentation.Model.DrawOrder.Count == CharacterRigDefaults.JointCount)
            for (int index = 0; index < CharacterRigDefaults.JointCount; index++) drawOrder[index] = Actor.Presentation.Model.DrawOrder[index];
        for(int b=0;b<CharacterRigDefaults.JointCount;b++)
        {
            var crop=Actor.Presentation.Model.AtlasRegions.Count==CharacterRigDefaults.JointCount ? Actor.Presentation.Model.AtlasRegions[tiles[b]]
                : new Rect2(tiles[b]%4*cellW,tiles[b]/4*cellH,cellW,cellH);
            int x0=Mathf.Clamp((int)crop.Position.X,0,image.GetWidth()-1), y0=Mathf.Clamp((int)crop.Position.Y,0,image.GetHeight()-1);
            int width=Mathf.Min((int)crop.Size.X,image.GetWidth()-x0),height=Mathf.Min((int)crop.Size.Y,image.GetHeight()-y0);
            int left=width,top=height,right=0,bottom=0;
            for(int y=2;y<height-2;y+=2) for(int x=2;x<width-2;x+=2)
                if(image.GetPixel(x0+x,y0+y).A>.08f) { left=Mathf.Min(left,x); top=Mathf.Min(top,y); right=Mathf.Max(right,x); bottom=Mathf.Max(bottom,y); }
            if(right<=left || bottom<=top) continue;
            var region=new Rect2(x0+left-1,y0+top-1,right-left+3,bottom-top+3); var rect=rectangles[b];
            if(b==15) rect=Actor.Presentation.Model.CrestRect;
            var sprite=new Sprite2D { Name="拆件", Texture=new AtlasTexture { Atlas=atlas,Region=region },
                Centered=false, Position=rect.Position, Scale=rect.Size/region.Size, TextureFilter=TextureFilterEnum.Linear,
                Material=_cartoonMaterial };
            if (b == 2 && Actor.Presentation.Model.HeadTexture is Texture2D headTexture)
            {
                // 头部可以独立换画风，仍复用该武将的头骨、显示尺寸及冠饰挂点；不改身体图集。
                using var headImage = headTexture.GetImage();
                if (headImage.IsCompressed()) headImage.Decompress();
                Rect2 headRegion = Actor.Presentation.Model.HeadRegion;
                if (!headRegion.HasArea()) headRegion = headImage.GetUsedRect();
                if (headRegion.HasArea())
                {
                    sprite.Texture = new AtlasTexture { Atlas = headTexture, Region = headRegion };
                    sprite.Scale = rect.Size / headRegion.Size;
                }
            }
            if (b is >= 3 and <= 8 && Actor.Presentation.Model.ArmAtlasAnchors.Length == 12)
                BindArmArtwork(b, sprite, region);
            // 旧 v2 图集双靴相向时只翻近侧；原生同向的 v3 图集关闭该配置。
            sprite.FlipH = (b == 12 && Actor.Presentation.Model.AlignBootsForward)
                || (b == 10 && Actor.Presentation.Model.FlipFarBootHorizontal);
            sprite.FlipV = b == 15 && Actor.Presentation.Model.FlipCrestVertical;
            sprite.Visible = b != 15 || Actor.Presentation.Model.CrestVisible;
            if (Actor.Presentation.Animation.ReferencePoseSet && Actor.Presentation.Model.ReferenceAccessories != null)
                BindReferenceAccessory(b, sprite);
            // 在最终武器纹理上限制采样，不重新计算包围盒，避免裁掉杂片后武器缩放或握点漂移。
            if (b == 14 && Actor.Presentation.Model.WeaponAtlasClipPolygon.Length is >= 3 and <= 16)
            {
                var material = BuildCartoonMaterial(Actor.Presentation.Model);
                var polygon = new Vector2[16];
                Actor.Presentation.Model.WeaponAtlasClipPolygon.CopyTo(polygon, 0);
                material.SetShaderParameter("clip_polygon", polygon);
                material.SetShaderParameter("clip_count", Actor.Presentation.Model.WeaponAtlasClipPolygon.Length);
                sprite.Material = material;
            }
            sprite.Name=_bones[b].Name+"图层";
            _partOffsets[b]=sprite.Transform;
            _partByBone[b]=sprite;
            _parts.Add(sprite);
        }
        foreach(int boneIndex in drawOrder)
            if(_partByBone[boneIndex] is Sprite2D part)
            {
                _drawLayers.AddChild(part); _drawOrderedParts.Add(part);
                if (Actor.Presentation.Animation.ReferencePoseSet && boneIndex is 10 or 12)
                    BuildReferenceFoot(boneIndex, part);
            }
        BuildOffhandWeapon();
        SyncParts();
    }

    /// <summary>把原图斜置的肩肘腕轴线对齐骨链；手掌以握点为原点，避免重复护臂和肩关节错位。</summary>
    private void BindArmArtwork(int bone, Sprite2D sprite, Rect2 region)
    {
        var model = Actor.Presentation.Model;
        int index = (bone - 3) * 2;
        Vector2 start = model.ArmAtlasAnchors[index], end = model.ArmAtlasAnchors[index + 1];
        Vector2 axis = end - start;
        if (axis.LengthSquared() < 1) return;
        bool palm = bone is 5 or 8;
        float length = palm ? model.PalmInset : _bones[bone + 1].Position.Length();
        if (bone is 4 or 7) length -= model.PalmInset;
        float scale = length / axis.Length();
        float rotation = Mathf.Pi / 2 - axis.Angle();
        sprite.Rotation = rotation;
        sprite.Scale = Vector2.One * scale;
        sprite.Position = -( (palm ? end : start) - region.Position).Rotated(rotation) * scale;
    }

    /// <summary>将骨链的最终变换同步到平级绘制层，骨骼父子关系不再决定部件遮挡顺序。</summary>
    private void SyncParts()
    {
        Transform2D inverse=_drawLayers.GlobalTransform.AffineInverse();
        for(int i=0;i<_partByBone.Length;i++)
            if(_partByBone[i]!=null)
            {
                Transform2D offset = _partOffsets[i];
                _partByBone[i].Transform=inverse*_bones[i].GlobalTransform*offset;
            }
        if (Actor.Presentation.Animation.ReferencePoseSet) SyncReferenceFeet(inverse);
        SyncOffhandWeapon(inverse);
    }

    /// <summary>压缩写实贴图色阶并在透明边缘内侧加深轮廓，降低卡牌立绘感。</summary>
    private static ShaderMaterial BuildCartoonMaterial(CharacterModelConfig config)
    {
        var shader=new Shader { Code="""
            shader_type canvas_item;
            render_mode unshaded;
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
                float aura = inner_edge * empower_strength;
                color = mix(color, empower_color.rgb, aura);
                color += empower_color.rgb * weapon_glow * (0.28 + inner_edge * .72);
                // fragment 的输入 COLOR 已乘过纹理；使用顶点颜色避免贴图二次相乘而发黑。
                COLOR = vec4(cartoon_enabled ? color : tex.rgb,tex.a)*vertex_tint;
            }
            """ };
        var material=new ShaderMaterial { Shader=shader };
        material.SetShaderParameter("cartoon_enabled",config.CartoonFilter);
        material.SetShaderParameter("color_steps",config.CartoonColorSteps);
        material.SetShaderParameter("saturation",config.CartoonSaturation);
        material.SetShaderParameter("outline_color",config.CartoonOutlineColor);
        material.SetShaderParameter("empower_color",new Color("ff3d3d"));
        material.SetShaderParameter("empower_strength",0f);
        material.SetShaderParameter("weapon_glow",0f);
        return material;
    }
}
