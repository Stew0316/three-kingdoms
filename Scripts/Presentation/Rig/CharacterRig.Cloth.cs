using Godot;

/// <summary>实例独立的布料参数；只变形披风采样和冠翎网格，身体和固定骨长不受影响。</summary>
public partial class CharacterRig
{
    private readonly System.Collections.Generic.List<ShaderMaterial> _clothMaterials = new();
    private readonly System.Collections.Generic.List<Sprite2D> _clothSprites = new();
    private readonly System.Collections.Generic.List<ProceduralPlume2D> _crestParts = new();
    private readonly System.Collections.Generic.List<Transform2D> _crestOffsets = new();
    public System.Collections.Generic.IReadOnlyList<ProceduralPlume2D> CrestParts => _crestParts;
    public Node2D ArtworkRoot => _drawLayers; // 验收复制全部实际绘制层，包含程序网格。
    private float _clothTime, _clothLastTime, _clothWeight;
    private Vector2 _clothFlow = Vector2.Left;
    public float ClothWeight => _clothWeight; // 验收可读取起停权重，不改变游戏规则。
    public float ClothTime => _clothTime; // 使用角色表现时钟，暂停不会继续飘动。

    private void BindMovementCloth(int bone, Sprite2D sprite)
    {
        if (sprite.Texture is not AtlasTexture atlas) return;
        int slot = bone == 15 ? 0 : 1;
        Vector2 root = bone == 15 ? Actor.Presentation.Model.ReferenceCrestRoot
            : new Vector2(atlas.Region.Size.X * .67f, 0);
        var material = CreateClothMaterial(sprite, root, slot == 0 ? Actor.Presentation.Model.ClothAmplitudes.X
            : Actor.Presentation.Model.ClothAmplitudes.Y, 1, 1, slot * 2.3f, bone == 13);
        if (bone == 13 && Actor.Presentation.Model.ClothCapeClipPolygon.Length is >= 3 and <= 16)
        {
            var polygon = new Vector2[16];
            Actor.Presentation.Model.ClothCapeClipPolygon.CopyTo(polygon, 0);
            material.SetShaderParameter("clip_polygon", polygon);
            material.SetShaderParameter("clip_count", Actor.Presentation.Model.ClothCapeClipPolygon.Length);
        }
    }

    private ShaderMaterial CreateClothMaterial(Sprite2D sprite, Vector2 root, float amplitude,
        float rate, float flex, float phase, bool cape)
    {
        var atlas = (AtlasTexture)sprite.Texture;
        var material = BuildCartoonMaterial(Actor.Presentation.Model);
        material.SetShaderParameter("cloth_enabled", true);
        material.SetShaderParameter("cloth_region", new Vector4(atlas.Region.Position.X, atlas.Region.Position.Y,
            atlas.Region.Size.X, atlas.Region.Size.Y));
        material.SetShaderParameter("cloth_root", root);
        material.SetShaderParameter("cloth_cape", cape);
        material.SetShaderParameter("cloth_amplitude", amplitude / Mathf.Max(.001f, sprite.Scale.X));
        material.SetShaderParameter("cloth_rate", rate);
        material.SetShaderParameter("cloth_flex", flex);
        material.SetShaderParameter("cloth_seed", (float)(Actor.GetInstanceId() % 997) * .17f + phase);
        sprite.Material = material;
        _clothSprites.Add(sprite);
        _clothMaterials.Add(material);
        return material;
    }

    /// <summary>每条冠翎独立生成网格与材质；冠座由头图绘制，不参与冠翎弯曲。</summary>
    private void BuildIndependentCrests()
    {
        var model = Actor.Presentation.Model;
        if (!model.ProceduralCrests) return;
        int count = model.CrestProfiles.Count;
        int pointsPerPlume = count > 0 ? model.CrestCurves.Count / count : 0;
        if (count == 0 || model.CrestCurves.Count != count * pointsPerPlume || pointsPerPlume < 4 || (pointsPerPlume - 1) % 3 != 0)
            throw new System.InvalidOperationException("每条程序冠翎必须配置相同数量的 3n+1 个曲线点和一组运动参数。");
        _partByBone[15].Visible = false;
        // 先绘制较细的后翎，再绘制宽前翎；两者都在冠座与头部后面。
        for (int index = count - 1; index >= 0; index--)
        {
            var sprite = new ProceduralPlume2D { Name = $"程序冠翎{index + 1}", Visible = model.CrestVisible };
            var points = new Vector2[pointsPerPlume];
            for (int point = 0; point < points.Length; point++) points[point] = model.CrestCurves[index * points.Length + point];
            sprite.Build(points, model.CrestProfiles[index]);
            _drawLayers.AddChild(sprite);
            _drawLayers.MoveChild(sprite, _partByBone[2].GetIndex());
            _crestParts.Add(sprite);
            _crestOffsets.Add(sprite.Transform);
        }
    }

    private void SyncIndependentCrests(Transform2D inverse)
    {
        for (int index = 0; index < _crestParts.Count; index++)
            _crestParts[index].Transform = inverse * _bones[15].GlobalTransform * _crestOffsets[index];
    }

    private void UpdateMovementCloth()
    {
        if (!Actor.Presentation.Model.MovementCloth) return;
        float elapsed = Mathf.Max(0, Actor.VisualTime - _clothLastTime);
        _clothLastTime = Actor.VisualTime;
        // 碰撞后的实际速度使顶墙停止鼓动；骑乘时读取同样的世界位移速度。
        Vector2 velocity = Actor.IsMounted ? RidingMount?.MotionSource?.HorseMotion.Velocity ?? Vector2.Zero : Actor.GetRealVelocity();
        float speed = velocity.Length();
        float target = Actor.IsDead || Actor.BattleFinished || speed < 1 ? 0 : Mathf.Clamp(speed / 150f, 0, 1);
        _clothWeight = Mathf.MoveToward(_clothWeight, target, elapsed * (target > _clothWeight ? 5 : 3));
        if (speed > 1) _clothFlow = _clothFlow.Lerp(-velocity / speed, 1 - Mathf.Exp(-10 * elapsed));
        if (_clothWeight > 0) _clothTime += elapsed;
        foreach (var plume in _crestParts)
            plume.SetWind(_clothTime, _clothWeight, plume.GlobalTransform.AffineInverse().BasisXform(_clothFlow).Normalized());
        for (int slot = 0; slot < _clothMaterials.Count; slot++)
        {
            if (_clothMaterials[slot] == null) continue;
            var localFlow = _clothSprites[slot].GlobalTransform.AffineInverse().BasisXform(_clothFlow).Normalized();
            _clothMaterials[slot].SetShaderParameter("cloth_weight", _clothWeight);
            _clothMaterials[slot].SetShaderParameter("cloth_time", _clothTime);
            _clothMaterials[slot].SetShaderParameter("cloth_flow", localFlow);
        }
    }
}
