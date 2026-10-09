using Godot;

public partial class CharacterRig
{
    // 所有关键姿态共用同一坐标系，只有最终求解阶段写入骨骼，避免多段动画争抢关节。
    private struct ReferenceFrame
    {
        public Vector2 Pelvis, FarHand, FreeHand, FarFoot, NearFoot;
        public float Waist, Chest, Head, Weapon, Blade, Separation, Grip;

        public static ReferenceFrame From(CharacterPoseConfig pose) => new()
        {
            Pelvis = pose.PelvisOffset, FarHand = pose.FarHand, FreeHand = pose.FreeHand,
            FarFoot = pose.FarFoot, NearFoot = pose.NearFoot,
            Waist = Mathf.DegToRad(pose.PelvisDegrees), Chest = Mathf.DegToRad(pose.ChestDegrees), Head = Mathf.DegToRad(pose.HeadDegrees),
            Weapon = Mathf.DegToRad(pose.WeaponDegrees), Blade = pose.BladeDistance,
            Separation = pose.GripSeparation, Grip = pose.TwoHandWeight
        };

        public static ReferenceFrame Mix(ReferenceFrame a, ReferenceFrame b, float weight) => new()
        {
            Pelvis = a.Pelvis.Lerp(b.Pelvis, weight), FarHand = a.FarHand.Lerp(b.FarHand, weight),
            FreeHand = a.FreeHand.Lerp(b.FreeHand, weight), FarFoot = a.FarFoot.Lerp(b.FarFoot, weight),
            NearFoot = a.NearFoot.Lerp(b.NearFoot, weight), Waist = Mathf.LerpAngle(a.Waist, b.Waist, weight),
            Chest = Mathf.LerpAngle(a.Chest, b.Chest, weight),
            Head = Mathf.LerpAngle(a.Head, b.Head, weight), Weapon = Mathf.LerpAngle(a.Weapon, b.Weapon, weight),
            Blade = Mathf.Lerp(a.Blade, b.Blade, weight), Separation = Mathf.Lerp(a.Separation, b.Separation, weight),
            Grip = Mathf.Lerp(a.Grip, b.Grip, weight)
        };
    }

    private ReferenceFrame _referenceFrame;
    private ReferenceFrame _referenceActionStart; // 从实际上一帧姿态起势，支持行走中出招。
    private bool _referenceInitialized;
    private Combatant.State _referencePreviousState;
    private float _referencePreviousActionTime;
    public float ReferenceGripWeight => _referenceFrame.Grip; // 调试读取副手握柄程度，0 为自由手。
    public Vector2 ReferencePrimaryGrip => _referenceFrame.FarHand; // 主握点，骨架局部坐标。
    public Vector2 ReferenceSecondaryGrip => _referenceFrame.FarHand
        + Vector2.Up.Rotated(_referenceFrame.Weapon) * _referenceFrame.Separation; // 副握点，骨架局部坐标。
    public Vector2 ReferenceBladeTip => Skeleton.ToGlobal(_referenceFrame.FarHand
        + Vector2.Up.Rotated(_referenceFrame.Weapon) * _referenceFrame.Blade); // 世界坐标戟尖，供刀光跟随真实挥击。
    private readonly Sprite2D[] _referenceFeet = new Sprite2D[2]; // 独立靴掌图层，业务骨链仍保持 16 关节。
    private readonly Transform2D[] _referenceFootOffsets = new Transform2D[2];
    private const float ReferenceFootHeight = 4; // 小腿末端的靴掌高度；其上方为固定胫骨段。

    /// <summary>直接采样已生成的参考配件，原始图片不改写，挂点不再取图像中心。</summary>
    private void BindReferenceAccessory(int bone, Sprite2D sprite)
    {
        var config = Actor.Presentation;
        if (bone == 14)
        {
            sprite.Texture = new AtlasTexture { Atlas = config.ReferenceAccessories, Region = config.ReferenceWeaponRegion };
            sprite.Scale = new Vector2(13, 86) / config.ReferenceWeaponRegion.Size;
            sprite.Position = new Vector2(-7.4f, -42);
        }
        else if (bone == 15)
        {
            sprite.Texture = new AtlasTexture { Atlas = config.ReferenceAccessories, Region = config.ReferenceCrestRegion };
            sprite.Scale = new Vector2(35, 30) / config.ReferenceCrestRegion.Size;
            sprite.Position = -config.ReferenceCrestRoot * sprite.Scale;
            sprite.FlipV = false;
        }
    }

    /// <summary>从同一小腿采样区分出靴掌，支持脚掌保持水平；使用原 PNG 的两个 AtlasTexture，不生成改图。</summary>
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
            TextureFilter = TextureFilterEnum.Linear, Material = shin.Material,
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
            _referenceFeet[index].Transform = inverse * Skeleton.GlobalTransform
                * new Transform2D(0, ankle) * _referenceFootOffsets[index];
        }
    }

    /// <summary>从长兵器关键姿态产生实际动作；姿态时序只读取战斗时钟。</summary>
    private void ProcessReferencePose(float elapsed)
    {
        var config = Actor.Presentation;
        var standing = ReferenceFrame.From(config.StandingPose);
        var moving = ReferenceFrame.From(config.MovingPose);
        var windup = ReferenceFrame.From(config.WindupPose);
        if (!_referenceInitialized)
        {
            _referenceFrame = standing;
            _referenceInitialized = true;
        }

        float facing = Actor.FacingDirection.X < -.15f ? -1 : Actor.FacingDirection.X > .15f ? 1 : Mathf.Sign(Scale.X);
        float baseScale = ArtScale / .05f;
        float squash = Actor.IsDead ? .52f : 1;
        bool hurt = Actor.CurrentState == Combatant.State.Hurt;
        Scale = new Vector2(facing * baseScale * (hurt ? 1.025f : 1), baseScale * config.DepthScale * squash * (hurt ? .97f : 1));
        Modulate = _source.Modulate;
        Rotation = 0;
        Position = Actor.IsDead ? new Vector2(0, 2) : hurt ? Actor.LastDamage.KnockbackDirection * .8f : Vector2.Zero;

        bool action = Actor.CurrentState is Combatant.State.Attack or Combatant.State.Dodge;
        bool wasAction = _referencePreviousState is Combatant.State.Attack or Combatant.State.Dodge;
        if (action && (!wasAction || Actor.ActionTime < _referencePreviousActionTime))
            _referenceActionStart = _referenceFrame;

        // 动作关键帧直接随前摇/生效/后摇采样，不再叠加低通延迟，避免打击判定已生效而身体仍未起势。
        if (action)
            _referenceFrame = SampleReferenceAction(standing, moving, windup);
        else if (elapsed > 0)
        {
            var target = ReferenceFrame.Mix(standing, moving, _walkWeight);
            float cycle = _walkPhase * Mathf.Tau;
            if (_walkWeight > 0)
            {
                target.FarFoot += ReferenceStep(_walkPhase, facing);
                target.NearFoot += ReferenceStep(Mathf.PosMod(_walkPhase + .5f, 1), facing);
                target.Pelvis.Y += .35f * (1 - Mathf.Cos(cycle * 2)) * _walkWeight;
                target.Pelvis.X += Mathf.Sin(cycle) * .65f * _walkWeight;
                target.Chest -= Mathf.Sin(cycle) * .025f * _walkWeight;
                target.FreeHand += new Vector2(Mathf.Sin(cycle) * 1.4f, .4f * Mathf.Cos(cycle)) * _walkWeight;
            }
            else
            {
                // 呼吸只改变上身，不让两只脚跟着待机时间上下浮动。
                target.Chest += Mathf.Sin(Actor.VisualTime * 2.4f) * .008f;
            }
            if (Actor.CurrentState == Combatant.State.Hurt)
            {
                float impact = Actor.LastDamage.KnockbackDirection.X * facing;
                target.Pelvis += new Vector2(impact * 1.5f, 2);
                target.Chest = impact * .16f;
                target.FreeHand = new Vector2(12, -43);
                target.Weapon = -.25f;
            }
            if (Actor.CounterRemaining > 0)
            {
                float phase = 1 - Actor.CounterRemaining / Actor.CounterDuration;
                target = windup;
                target.Weapon += Mathf.Sin(phase * Mathf.Tau) * .7f;
                target.FarHand.X += Mathf.Sin(phase * Mathf.Tau) * 8;
            }
            _referenceFrame = ReferenceFrame.Mix(_referenceFrame, target, 1 - Mathf.Exp(-22 * elapsed));
        }
        _referencePreviousState = Actor.CurrentState;
        _referencePreviousActionTime = Actor.ActionTime;
        ApplyReferenceFrame(_referenceFrame);
    }

    /// <summary>脚底的接触、经过和摆动循环；脚距由关键姿态决定，步幅不移动髋关节。</summary>
    private Vector2 ReferenceStep(float phase, float facing)
    {
        const float support = .58f;
        float swing = Mathf.Clamp((phase - support) / (1 - support), 0, 1);
        float forward = phase < support ? Mathf.Lerp(1, -1, phase / support) : Mathf.Lerp(-1, 1, Smooth(swing));
        Vector2 direction = new(_walkDirection.X * facing, _walkDirection.Y * .35f);
        // 上下移动仍保留侧面透视的换脚量，避免只有两脚一起纵向滑动。
        if (Mathf.Abs(direction.X) < .2f) direction.X = .45f;
        float lift = Mathf.Sin(swing * Mathf.Pi) * Actor.Presentation.WalkFootLift;
        return (direction * (forward * Actor.Presentation.WalkStride) - new Vector2(0, lift)) * _walkWeight;
    }

    /// <summary>低位警戒、后收、过顶、斩入和随势；普攻/突进沿用同一持械语言，伤害时序不变。</summary>
    private ReferenceFrame SampleReferenceAction(ReferenceFrame standing, ReferenceFrame moving, ReferenceFrame windup)
    {
        float time = Actor.ActionTime;
        var spec = Actor.Spec;
        if (Actor.CurrentState == Combatant.State.Dodge)
        {
            var dodge = moving;
            dodge.Pelvis += new Vector2(3, 4);
            dodge.Chest = .3f;
            dodge.FarFoot = new Vector2(-13, -1);
            dodge.NearFoot = new Vector2(12, 0);
            float inWeight = Smooth(time / .045f);
            float outWeight = Smooth((time - spec.Active) / Mathf.Max(spec.Recover, .001f));
            return ReferenceFrame.Mix(ReferenceFrame.Mix(_referenceActionStart, dodge, inWeight), standing, outWeight);
        }
        // 长戟始终按近战劈扫处理；普攻缩短收戟幅度，突进也保留两手拧转，不能退回前送标枪。
        if (Actor.CurrentAction == CombatAction.Basic)
        {
            windup.Weapon = Mathf.DegToRad(-38);
            windup.FarHand = new Vector2(1, -35);
        }
        else if (Actor.CurrentAction == CombatAction.Dash)
        {
            windup.Weapon = Mathf.DegToRad(-60);
            windup.FarHand = new Vector2(3, -34);
        }
        if (time < spec.Prepare)
        {
            // 前摇后段明确停在身后收戟姿势，双手低于面部，不能把武器举在肩上等待投出。
            return ReferenceFrame.Mix(_referenceActionStart, windup, Smooth(time / Mathf.Max(spec.Prepare * .72f, .001f)));
        }
        var high = ReferenceFrame.From(Actor.Presentation.StrikeHighPose);
        var contact = ReferenceFrame.From(Actor.Presentation.StrikeContactPose);
        var follow = ReferenceFrame.From(Actor.Presentation.StrikeFollowPose);
        float strike = Mathf.Clamp((time - spec.Prepare) / Mathf.Max(spec.Active, .001f), 0, 1);
        // 显式绕身弧线：背后收戟→经过上方→前方切入→压低随势。每段不足180°，不会被最短角插值反向抄近路。
        ReferenceFrame result;
        if (strike < .26f) result = ReferenceFrame.Mix(windup, high, Smooth(strike / .26f));
        else if (strike < .70f) result = ReferenceFrame.Mix(high, contact, Smooth((strike - .26f) / .44f));
        else result = ReferenceFrame.Mix(contact, follow, Smooth((strike - .70f) / .23f));
        if (time >= spec.Prepare + spec.Active)
        {
            float recovery = (time - spec.Prepare - spec.Active) / Mathf.Max(spec.Recover, .001f);
            // 刃端先完成随势，腰胯和双手再收回低位警戒，不松手抛出长戟。
            result = ReferenceFrame.Mix(follow, standing, Smooth((recovery - .12f) / .88f));
        }
        return result;
    }

    /// <summary>先落腰胯和足底，再确定长戟及握点，最后求解双臂；统一写入同一骨架。</summary>
    private void ApplyReferenceFrame(ReferenceFrame frame)
    {
        _bones[0].Position = new Vector2(0, -_pelvisHeight) + frame.Pelvis;
        _bones[0].Rotation = frame.Waist;
        _bones[1].Rotation = frame.Chest;
        _bones[2].Rotation = frame.Head;
        // 解算到踝点，靴掌单独保持接地，避免小腿一转就把整个鞋底翘起来。
        Vector2 ankleOffset = new(0, -ReferenceFootHeight);
        SolveReferenceChain(9, 10, frame.FarFoot + ankleOffset, _thighLength, _lowerLegLength - ReferenceFootHeight, 1);
        SolveReferenceChain(11, 12, frame.NearFoot + ankleOffset, _thighLength, _lowerLegLength - ReferenceFootHeight, 1);

        _bones[14].Position = frame.FarHand;
        _bones[14].Rotation = frame.Weapon;
        Vector2 blade = Vector2.Up.Rotated(frame.Weapon);
        Vector2 secondGrip = frame.FarHand + blade * frame.Separation;
        Vector2 nearHand = frame.FreeHand.Lerp(secondGrip, frame.Grip);
        SolveReferenceChain(3, 4, frame.FarHand, 12, 12, 1);
        SolveReferenceChain(6, 7, nearHand, 12, 12, 1);
        // 手掌朝向握柄，不继承肘部旋转；放开的副手自然下垂。
        SetReferenceAbsoluteRotation(5, frame.Weapon + .15f);
        SetReferenceAbsoluteRotation(8, Mathf.LerpAngle(.05f, frame.Weapon, frame.Grip));
        _bones[13].Rotation = -.08f + _walkWeight * .10f + Mathf.Sin(Actor.VisualTime * 3.2f) * .025f;
        _bones[15].Rotation = Mathf.Sin(Actor.VisualTime * 3) * .025f;

        // 同一长戟保持固定尺寸，只按关键姿态沿柄改变主握位置。
        Transform2D weaponArt = _partOffsets[14];
        weaponArt.Origin = new Vector2(weaponArt.Origin.X, -frame.Blade);
        _partOffsets[14] = weaponArt;
    }

    /// <summary>两节固定骨长求解，目标以骨架脚点为原点；不拉伸贴图或骨段。</summary>
    private void SolveReferenceChain(int upper, int lower, Vector2 target, float firstLength, float secondLength, float bend)
    {
        var parent = _bones[upper].GetParent<Node2D>();
        Vector2 offset = parent.ToLocal(Skeleton.ToGlobal(target)) - _bones[upper].Position;
        float distance = Mathf.Clamp(offset.Length(), Mathf.Abs(firstLength - secondLength) + .01f, firstLength + secondLength - .01f);
        float joint = bend * Mathf.Acos(Mathf.Clamp((distance * distance - firstLength * firstLength - secondLength * secondLength)
            / (2 * firstLength * secondLength), -1, 1));
        _bones[upper].Rotation = offset.Angle() - Mathf.Pi / 2
            - Mathf.Atan2(secondLength * Mathf.Sin(joint), firstLength + secondLength * Mathf.Cos(joint));
        _bones[lower].Rotation = joint;
    }

    private void SetReferenceAbsoluteRotation(int bone, float angle)
    {
        Vector2 direction = Vector2.Right.Rotated(angle);
        var parent = _bones[bone].GetParent<Node2D>();
        Vector2 local = parent.ToLocal(Skeleton.ToGlobal(direction)) - parent.ToLocal(Skeleton.GlobalPosition);
        _bones[bone].Rotation = local.Angle();
    }
}
