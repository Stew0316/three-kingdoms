using Godot;

/// <summary>副手武器、独立靴掌和冠翎等附件的采样与同步。</summary>
public partial class CharacterRig
{
    public Sprite2D OffhandWeapon => _offhandWeapon; // 副剑独立图层，供预览与双手握点验收读取。
    private Sprite2D _offhandWeapon;
    public Vector2 OffhandBladeTip => Skeleton.ToGlobal(_referenceFrame.FreeHand
        + Vector2.Up.Rotated(_referenceFrame.OffhandWeapon) * Actor.Presentation.Model.OffhandBladeDistance);

    /// <summary>副剑是模型附件，跟随独立前手目标，不增加技能或武将身份分支。</summary>
    private void BuildOffhandWeapon()
    {
        var model = Actor.Presentation.Model;
        if (!model.DualWeapons || !model.OffhandWeaponRegion.HasArea()) return;
        _offhandWeapon = new Sprite2D { Name = "副手剑图层", Centered = false,
            Texture = new AtlasTexture { Atlas = model.PartsAtlas, Region = model.OffhandWeaponRegion },
            TextureFilter = TextureFilterEnum.Linear, Material = _cartoonMaterial };
        _drawLayers.AddChild(_offhandWeapon);
        // 杆身先画、握拳后画，使剑柄穿过握心而不是盖住整个手掌。
        _drawLayers.MoveChild(_offhandWeapon, _partByBone[8].GetIndex());
        _drawOrderedParts.Insert(_drawOrderedParts.IndexOf(_partByBone[8]), _offhandWeapon);
    }

    private void SyncOffhandWeapon(Transform2D inverse)
    {
        if (_offhandWeapon == null) return;
        var model = Actor.Presentation.Model;
        var art = new Transform2D(0, model.OffhandWeaponSize / model.OffhandWeaponRegion.Size, 0,
            new Vector2(-model.OffhandWeaponSize.X * .5f, -model.OffhandBladeDistance));
        _offhandWeapon.Transform = inverse * Skeleton.GlobalTransform
            * new Transform2D(_referenceFrame.OffhandWeapon, _referenceFrame.FreeHand) * art;
    }
    private readonly Sprite2D[] _referenceFeet = new Sprite2D[2]; // 独立靴掌图层，业务骨链仍保持 16 关节。
    private readonly Transform2D[] _referenceFootOffsets = new Transform2D[2];
    private const float ReferenceFootHeight = 4; // 小腿末端的靴掌高度；其上方为固定胫骨段。
    private const float ReferenceSupport = .54f; // 每只脚承重的周期占比，其余时间向前摆动。
    public float ReferenceSupportFraction => ReferenceSupport; // 供步态验收区分承重和摆动阶段。

    /// <summary>直接采样已生成的参考配件，原始图片不改写，挂点不再取图像中心。</summary>
    private void BindReferenceAccessory(int bone, Sprite2D sprite)
    {
        var config = Actor.Presentation.Model;
        if (bone == 14)
        {
            sprite.Texture = new AtlasTexture { Atlas = config.ReferenceAccessories, Region = config.ReferenceWeaponRegion };
            sprite.Scale = config.AccessoryWeaponSize / config.ReferenceWeaponRegion.Size;
            sprite.Position = config.AccessoryWeaponOffset;
        }
        else if (bone == 15 && config.ReferenceCrestRegion.HasArea())
        {
            sprite.Texture = new AtlasTexture { Atlas = config.ReferenceAccessories, Region = config.ReferenceCrestRegion };
            sprite.Scale = config.AccessoryCrestSize / config.ReferenceCrestRegion.Size;
            sprite.Position = -config.ReferenceCrestRoot * sprite.Scale;
            sprite.FlipV = false;
        }
    }

    /// <summary>从同一小腿采样区分出靴掌，支持独立落地和蹬离；原 PNG 不变。</summary>
    private void BuildReferenceFoot(int bone, Sprite2D shin)
    {
        var original = (AtlasTexture)shin.Texture;
        Rect2 region = original.Region;
        // 脚踝约在原小腿包围盒的 80% 处，与原绑定的膝下长度相对应。
        float split = Mathf.Round(region.Size.Y * .8f);
        var foot = new Sprite2D
        {
            Name = bone == 10 ? "远靴掌图层" : "近靴掌图层", Centered = false,
            Texture = new AtlasTexture { Atlas = original.Atlas,
                Region = new Rect2(region.Position + new Vector2(0, split), new Vector2(region.Size.X, region.Size.Y - split)) },
            TextureFilter = TextureFilterEnum.Linear, Material = shin.Material, FlipH = shin.FlipH,
            Position = new Vector2(shin.Position.X, 0),
            Scale = new Vector2(10 / region.Size.X, ReferenceFootHeight / (region.Size.Y - split))
        };
        shin.Texture = new AtlasTexture { Atlas = original.Atlas,
            Region = new Rect2(region.Position, new Vector2(region.Size.X, split)) };
        shin.Scale = new Vector2(shin.Scale.X, (_lowerLegLength - ReferenceFootHeight + 2) / split);
        _partOffsets[bone] = shin.Transform;
        int index = bone == 10 ? 0 : 1;
        _referenceFeet[index] = foot;
        _referenceFootOffsets[index] = foot.Transform;
        _drawLayers.AddChild(foot);
        _drawOrderedParts.Add(foot);
    }

    private void SyncReferenceFeet(Transform2D inverse)
    {
        for (int index = 0; index < _referenceFeet.Length; index++)
        {
            if (_referenceFeet[index] == null) continue;
            int bone = index == 0 ? 10 : 12;
            Vector2 ankle = Skeleton.ToLocal(_bones[bone].ToGlobal(new Vector2(0, _lowerLegLength - ReferenceFootHeight)));
            float roll = index == 0 ? _referenceFrame.FarFootRoll : _referenceFrame.NearFootRoll;
            _referenceFeet[index].Transform = inverse * Skeleton.GlobalTransform
                * new Transform2D(roll, ankle) * _referenceFootOffsets[index];
        }
    }
}
