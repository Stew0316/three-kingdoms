using Godot;
using System.Collections.Generic;

/// <summary>横放兵器与独立附属贴图的绑定；不修改原图像素，也不改变业务骨架。</summary>
public partial class CharacterRig
{
    private Vector2 _weaponTipOffset; // 刃尖为零时的原图原点；姿态只改变握点到刃尖的距离。
    private readonly List<(Sprite2D Sprite, int Bone, Transform2D Offset)> _artworkAttachments = new();
    private readonly List<Node2D> _weaponLayerRestOrder = new();
    private bool _weaponBehindHead;

    private void CacheWeaponLayerOrder()
    {
        if (!Actor.Presentation.Model.RaisedWeaponBehindHead && Actor.Presentation.Model.DepthFacing >= 0) return;
        foreach (Node2D layer in _drawLayers.GetChildren()) _weaponLayerRestOrder.Add(layer);
    }

    private void SyncWeaponLayerOrder()
    {
        if (_weaponLayerRestOrder.Count == 0) return;
        bool rearView = Actor.Presentation.Model.DepthFacing < 0;
        bool behind = (rearView || _referenceFrame.Weapon < 0) && _referenceFrame.Grip > .3f;
        if (behind == _weaponBehindHead) return;
        _weaponBehindHead = behind;
        for (int i = 0; i < _weaponLayerRestOrder.Count; i++) _drawLayers.MoveChild(_weaponLayerRestOrder[i], i);
        if (behind)
        {
            // 背面接柄后副手在身体前方，必须被背甲遮挡；放开后恢复模型配置的顺序，让披风遮住下垂空手。
            foreach (int bone in rearView ? new[] { 8 } : new[] { 14, 4, 7, 5, 8 })
            {
                var layer = _partByBone[bone];
                int headIndex = _partByBone[rearView ? 1 : 2].GetIndex();
                _drawLayers.MoveChild(layer, headIndex - (layer.GetIndex() < headIndex ? 1 : 0));
            }
        }
        _drawOrderedParts.Clear();
        foreach (Node2D layer in _drawLayers.GetChildren())
            if (layer is Sprite2D sprite) _drawOrderedParts.Add(sprite);
    }

    private void BindWeaponAxis(Sprite2D sprite)
    {
        _weaponTipOffset = new Vector2(sprite.Position.X, 0);
        var model = Actor.Presentation.Model;
        if (model.WeaponAxisAnchors.Length != 2 || sprite.Texture is not AtlasTexture texture) return;
        Vector2 tail = model.WeaponAxisAnchors[0], tip = model.WeaponAxisAnchors[1];
        Vector2 axis = tip - tail;
        if (axis.LengthSquared() < 1) return;
        float rotation = -Mathf.Pi / 2 - axis.Angle();
        float scale = model.AccessoryWeaponSize.Y / axis.Length();
        sprite.Rotation = rotation;
        sprite.Scale = Vector2.One * scale;
        _weaponTipOffset = -(tip - texture.Region.Position).Rotated(rotation) * scale;
        sprite.Position = _weaponTipOffset;
    }

    private void BuildArtworkAttachments()
    {
        foreach (var config in Actor.Presentation.Model.Attachments)
        {
            if (config == null || config.BoneIndex < 0 || config.BoneIndex >= _bones.Length
                || !config.Region.HasArea()) continue;
            var sprite = new Sprite2D
            {
                Name = config.LayerName, Centered = false, Material = _cartoonMaterial,
                Texture = new AtlasTexture { Atlas = config.Texture ?? Actor.Presentation.Model.PartsAtlas, Region = config.Region },
                TextureFilter = TextureFilterEnum.Linear, Position = config.DisplayRect.Position,
                Scale = config.DisplayRect.Size / config.Region.Size, RotationDegrees = config.RotationDegrees
            };
            if (config.ClothAmplitude > 0)
            {
                var material = CreateClothMaterial(sprite, config.ClothRoot, config.ClothAmplitude, config.ClothRate,
                    Mathf.Max(.1f, config.ClothFlex), config.ClothPhase, false, config.AmbientWind);
                // 分段调节贴图纵向比例，根部不移动；独立材质同时被实机预览复用。
                material.SetShaderParameter("cloth_vertical_scale", new Vector2(
                    Mathf.Max(.1f, config.ClothVerticalScale.X), Mathf.Max(.1f, config.ClothVerticalScale.Y)));
                material.SetShaderParameter("cloth_arc", config.ClothArc);
                material.SetShaderParameter("cloth_arc_width_scale", Mathf.Clamp(config.ClothArcWidthScale, .1f, 1f));
            }
            if (config.ExcludeRegion.HasArea())
            {
                // 排除同一矩形裁区内的邻件，独立材质避免影响其他附件；柔性采样后仍按原图坐标裁剪。
                var material = sprite.Material is ShaderMaterial ownMaterial && ownMaterial != _cartoonMaterial
                    ? ownMaterial : BuildCartoonMaterial(Actor.Presentation.Model);
                Rect2 exclusion = config.ExcludeRegion;
                material.SetShaderParameter("exclude_region", new Vector4(exclusion.Position.X, exclusion.Position.Y,
                    exclusion.Size.X, exclusion.Size.Y));
                sprite.Material = material;
            }
            _drawLayers.AddChild(sprite);
            if (config.BeforeBone >= 0 && config.BeforeBone < _bones.Length)
            {
                var next = _partByBone[config.BeforeBone];
                _drawLayers.MoveChild(sprite, next.GetIndex());
                _drawOrderedParts.Insert(_drawOrderedParts.IndexOf(next), sprite);
            }
            else _drawOrderedParts.Add(sprite);
            _artworkAttachments.Add((sprite, config.BoneIndex, sprite.Transform));
        }
    }

    private void SyncArtworkAttachments(Transform2D inverse)
    {
        foreach (var (sprite, bone, offset) in _artworkAttachments)
            sprite.Transform = inverse * _bones[bone].GlobalTransform * offset;
    }
}
