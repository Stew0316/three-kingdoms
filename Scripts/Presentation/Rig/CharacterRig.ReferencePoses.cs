using Godot;
using static AnimationCurves;

public partial class CharacterRig
{
    // 所有关键姿态共用同一坐标系，只有最终求解阶段写入骨骼，避免多段动画争抢关节。
    private struct ReferenceFrame
    {
        public Vector2 Pelvis, FarHand, FreeHand, FarFoot, NearFoot;
        public float Waist, Chest, Head, Weapon, OffhandWeapon, Blade, Separation, Grip;
        public float WeaponProjection, HandDepth, FarDepth, NearDepth, FarLift, NearLift; // 纵深和离地高度分开保存，避免把前后换脚误当作上下跳动。
        public float FarFootRoll, NearFootRoll; // 靴掌独立绕接地点滚动，不继承小腿角度。

        public static ReferenceFrame From(CharacterPoseConfig pose) => new()
        {
            Pelvis = pose.PelvisOffset, FarHand = pose.FarHand, FreeHand = pose.FreeHand,
            FarFoot = pose.FarFoot, NearFoot = pose.NearFoot,
            Waist = Mathf.DegToRad(pose.PelvisDegrees), Chest = Mathf.DegToRad(pose.ChestDegrees), Head = Mathf.DegToRad(pose.HeadDegrees),
            Weapon = Mathf.DegToRad(pose.WeaponDegrees), Blade = pose.BladeDistance,
            OffhandWeapon = Mathf.DegToRad(pose.OffhandWeaponDegrees),
            Separation = pose.GripSeparation, Grip = pose.TwoHandWeight, WeaponProjection = pose.WeaponProjection, HandDepth = pose.HandDepth
        };

        public static ReferenceFrame Mix(ReferenceFrame a, ReferenceFrame b, float weight) => new()
        {
            Pelvis = a.Pelvis.Lerp(b.Pelvis, weight), FarHand = a.FarHand.Lerp(b.FarHand, weight),
            FreeHand = a.FreeHand.Lerp(b.FreeHand, weight), FarFoot = a.FarFoot.Lerp(b.FarFoot, weight),
            NearFoot = a.NearFoot.Lerp(b.NearFoot, weight), Waist = Mathf.LerpAngle(a.Waist, b.Waist, weight),
            Chest = Mathf.LerpAngle(a.Chest, b.Chest, weight),
            Head = Mathf.LerpAngle(a.Head, b.Head, weight), Weapon = Mathf.LerpAngle(a.Weapon, b.Weapon, weight),
            OffhandWeapon = Mathf.LerpAngle(a.OffhandWeapon, b.OffhandWeapon, weight),
            Blade = Mathf.Lerp(a.Blade, b.Blade, weight), Separation = Mathf.Lerp(a.Separation, b.Separation, weight),
            Grip = Mathf.Lerp(a.Grip, b.Grip, weight),
            WeaponProjection = Mathf.Lerp(a.WeaponProjection, b.WeaponProjection, weight),
            HandDepth = Mathf.Lerp(a.HandDepth, b.HandDepth, weight),
            FarDepth = Mathf.Lerp(a.FarDepth, b.FarDepth, weight), NearDepth = Mathf.Lerp(a.NearDepth, b.NearDepth, weight),
            FarLift = Mathf.Lerp(a.FarLift, b.FarLift, weight), NearLift = Mathf.Lerp(a.NearLift, b.NearLift, weight),
            FarFootRoll = Mathf.Lerp(a.FarFootRoll, b.FarFootRoll, weight),
            NearFootRoll = Mathf.Lerp(a.NearFootRoll, b.NearFootRoll, weight)
        };
    }

    private ReferenceFrame _referenceFrame;
    private ReferenceFrame _referenceActionStart; // 从实际上一帧姿态起势，支持行走中出招。
    private ReferenceFrame _referenceCounterStart; // 反击同样从实际姿态起势，不叠加低通滤波改变握点轨迹。
    private float _referencePreviousCounter;
    private bool _referenceInitialized;
    private Combatant.State _referencePreviousState;
    private float _referencePreviousActionTime;
    public float ReferenceGripWeight => SpatialMotionActive ? _spatialPose.GripWeight : SupportHandWeight(_referenceFrame.Grip); // 实际副手握柄程度，0 为自由手。
    public Vector2 ReferencePrimaryGrip => SpatialMotionActive ? SpatialMotionSampler.Project(_spatialPose.Points[14]) : _referenceFrame.FarHand;
    public Vector2 ReferenceSecondaryGrip => SpatialMotionActive ? SpatialMotionSampler.Project(_spatialPose.Points[14]+_spatialPose.Blade*10) : Actor.Presentation.Model.DualWeapons ? _referenceFrame.FreeHand : _referenceFrame.FarHand
        + Vector2.Up.Rotated(_referenceFrame.Weapon) * _referenceFrame.Separation * _referenceFrame.WeaponProjection; // 双持为独立副剑握点，长柄为杆身副握点。
    public Vector2 ReferenceBladeTip => Skeleton.ToGlobal(SpatialMotionActive ? _spatialTip : _referenceFrame.FarHand
        + Vector2.Up.Rotated(_referenceFrame.Weapon) * _referenceFrame.Blade * _referenceFrame.WeaponProjection); // 世界坐标刀尖，供刀光跟随真实挥击。
    /// <summary>从长兵器关键姿态产生实际动作；姿态时序只读取战斗时钟。</summary>
    private void ProcessReferencePose(float elapsed)
    {
        RestoreSpatialBinding();
        var config = Actor.Presentation.Animation;
        var standing = ReferenceFrame.From(config.StandingPose);
        var moving = ReferenceFrame.From(config.MovingPose);
        if (!_referenceInitialized)
        {
            _referenceFrame = standing;
            _referenceInitialized = true;
        }

        float facing = Actor.FacingDirection.X < -.15f ? -1 : Actor.FacingDirection.X > .15f ? 1 : Mathf.Sign(Scale.X);
        if (Actor.Presentation.Model.DepthFacing != 0) facing = 1; // 已有正/背面源图不随横向输入镜像，避免左右持械手交换。
        float baseScale = ScreenLockedScale();
        float squash = Actor.IsDead ? .52f : 1;
        bool hurt = Actor.CurrentState == Combatant.State.Hurt;
        Scale = new Vector2(facing * baseScale * (hurt ? 1.025f : 1), baseScale * Actor.Presentation.Model.DepthScale * squash * (hurt ? .97f : 1));
        Modulate = _source.Modulate;
        Rotation = 0;
        Position = Actor.IsDead ? new Vector2(0, 2) : hurt ? Actor.LastDamage.KnockbackDirection * .8f : Vector2.Zero;

        bool action = Actor.CurrentState is Combatant.State.Attack or Combatant.State.Dodge;
        bool wasAction = _referencePreviousState is Combatant.State.Attack or Combatant.State.Dodge;
        if (action && (!wasAction || Actor.ActionTime < _referencePreviousActionTime))
            _referenceActionStart = _referenceFrame;
        // 零增量的表现帧也要保存起势，不能先更新计时再漏掉首次反击快照。
        if (Actor.CounterRemaining > 0 && (_referencePreviousCounter <= 0 || Actor.CounterRemaining > _referencePreviousCounter))
            _referenceCounterStart = _referenceFrame;

        // 动作关键帧直接随前摇/生效/后摇采样，不再叠加低通延迟，避免打击判定已生效而身体仍未起势。
        if (action)
            _referenceFrame = SampleReferenceAction(standing, moving);
        else if (elapsed > 0)
        {
            var target = ReferenceFrame.Mix(standing, moving, _walkWeight);
            float cycle = _walkPhase * Mathf.Tau;
            if (_walkWeight > 0)
            {
                target.FarFoot += ReferenceStep(_walkPhase, facing);
                float nearPhase = Mathf.PosMod(_walkPhase + .5f, 1);
                target.NearFoot += ReferenceStep(nearPhase, facing);
                target.FarFootRoll = ReferenceStepRoll(_walkPhase) * _walkWeight;
                target.NearFootRoll = ReferenceStepRoll(nearPhase) * _walkWeight;
                if (Actor.Presentation.Model.DepthFacing != 0)
                {
                    var farStep = DepthStep(_walkPhase); var nearStep = DepthStep(nearPhase);
                    target.FarDepth = farStep.X; target.FarLift = farStep.Y;
                    target.NearDepth = nearStep.X; target.NearLift = nearStep.Y;
                    target.FarFoot = moving.FarFoot; target.NearFoot = moving.NearFoot;
                    target.FarFootRoll = target.NearFootRoll = 0;
                }
                // 承重腿撑起身体，摆动腿才明显屈膝；腰高由固定腿长和落脚位置反求。
                float waistY = ReferenceWalkWaist(target, _walkPhase, nearPhase);
                float rise = (waistY - target.Pelvis.Y) * _walkWeight;
                target.Pelvis.Y += rise;
                target.FarHand.Y += rise;
                target.FreeHand.Y += rise;
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
                target.Pelvis += new Vector2(impact * config.HurtPelvisShift.X, config.HurtPelvisShift.Y);
                target.Chest = impact * Mathf.DegToRad(config.HurtChestDegrees);
                target.FreeHand = config.HurtFreeHand;
                target.Weapon = Mathf.DegToRad(config.HurtWeaponDegrees);
            }
            if (Actor.CounterRemaining > 0)
            {
                float phase = 1 - Actor.CounterRemaining / Actor.CounterDuration;
                // 技能只发出反击事实，武将自己的 Counter 资源决定如何回击。
                target = SampleReferenceTimeline(_referenceCounterStart, standing, config.Counter, phase, .2f, .6f, .2f);
            }
            _referenceFrame = Actor.CounterRemaining > 0 ? target
                : ReferenceFrame.Mix(_referenceFrame, target, 1 - Mathf.Exp(-22 * elapsed));
            if (Actor.CurrentState == Combatant.State.Move && _walkWeight >= .99f && Actor.CounterRemaining <= 0)
            {
                // 足底不能再经过低通滞后，否则落地后仍会漂移；起停和受击仍沿用姿态过渡。
                _referenceFrame.FarFoot = target.FarFoot;
                _referenceFrame.NearFoot = target.NearFoot;
                _referenceFrame.FarFootRoll = target.FarFootRoll;
                _referenceFrame.NearFootRoll = target.NearFootRoll;
                _referenceFrame.FarDepth = target.FarDepth; _referenceFrame.NearDepth = target.NearDepth;
                _referenceFrame.FarLift = target.FarLift; _referenceFrame.NearLift = target.NearLift;
                float correction = target.Pelvis.Y - _referenceFrame.Pelvis.Y;
                _referenceFrame.Pelvis.Y += correction;
                _referenceFrame.FarHand.Y += correction;
                _referenceFrame.FreeHand.Y += correction;
            }
            else if (Actor.CurrentState == Combatant.State.Idle && _walkWeight == 0 && Actor.CounterRemaining <= 0
                && _referenceFrame.FarFoot.DistanceTo(standing.FarFoot) < .01f
                && _referenceFrame.NearFoot.DistanceTo(standing.NearFoot) < .01f
                && _referenceFrame.Pelvis.DistanceTo(standing.Pelvis) < .01f)
            {
                // 收势到位后固定下盘，清掉指数插值的微小残差；呼吸仍只在胸头层发生。
                _referenceFrame.Pelvis = standing.Pelvis;
                _referenceFrame.Waist = standing.Waist;
                _referenceFrame.FarFoot = standing.FarFoot;
                _referenceFrame.NearFoot = standing.NearFoot;
                _referenceFrame.FarFootRoll = _referenceFrame.NearFootRoll = 0;
            }
        }
        _referencePreviousState = Actor.CurrentState;
        _referencePreviousActionTime = Actor.ActionTime;
        _referencePreviousCounter = Actor.CounterRemaining;
        ApplyReferenceFrame(_referenceFrame);
    }

    /// <summary>读取当前技能的动作语义，映射到本武将自己的曲线；按技能规则时钟采样。</summary>
    private ReferenceFrame SampleReferenceAction(ReferenceFrame standing, ReferenceFrame moving)
    {
        float time = Actor.ActionTime;
        var spec = Actor.Spec;
        if (Actor.CurrentMotion == CombatMotion.Empower)
        {
            // 行走中爆气只平滑收稳到持械站姿；不提械、挥击或接入攻击关键帧。
            return ReferenceFrame.Mix(_referenceActionStart, standing, Smooth(time / Mathf.Max(spec.Prepare, .001f)));
        }
        if (Actor.CurrentState == Combatant.State.Dodge)
        {
            var dodge = ReferenceFrame.From(Actor.Presentation.Animation.DodgePose);
            float inWeight = Smooth(time / Mathf.Max(.001f, Actor.Presentation.Animation.DodgeEnterSeconds));
            float outWeight = Smooth((time - spec.Active) / Mathf.Max(spec.Recover, .001f));
            return ReferenceFrame.Mix(ReferenceFrame.Mix(_referenceActionStart, dodge, inWeight), standing, outWeight);
        }
        var animation = Actor.Presentation.Animation.GetAction(Actor.CurrentMotion);
        return SampleReferenceTimeline(_referenceActionStart, standing, animation, time, spec.Prepare, spec.Active, spec.Recover);
    }

    /// <summary>通用四关键点曲线，只解释角色动画资源；不包含武将名、技能ID或伤害规则分支。</summary>
    private static ReferenceFrame SampleReferenceTimeline(ReferenceFrame start, ReferenceFrame standing,
        CharacterActionAnimationConfig animation, float time, float prepare, float active, float recover)
    {
        if (animation == null) return standing;
        var windup = ReferenceFrame.From(animation.Windup);
        var high = ReferenceFrame.From(animation.High);
        var contact = ReferenceFrame.From(animation.Contact);
        var follow = ReferenceFrame.From(animation.Follow);
        if (time < prepare)
        {
            float progress = time / Mathf.Max(prepare * animation.WindupReach, .001f);
            if (animation.PrepareVia != null)
            {
                var via = ReferenceFrame.From(animation.PrepareVia);
                float viaAt = Mathf.Clamp(animation.PrepareViaAt, .1f, .9f);
                return progress < viaAt
                    ? ReferenceFrame.Mix(start, via, Smooth(progress / viaAt))
                    : ReferenceFrame.Mix(via, windup, Smooth((progress - viaAt) / (1 - viaAt)));
            }
            return ReferenceFrame.Mix(start, windup, Smooth(progress));
        }
        if (time >= prepare + active)
        {
            float recovery = (time - prepare - active) / Mathf.Max(recover, .001f);
            return ReferenceFrame.Mix(follow, standing, Smooth((recovery - animation.RecoveryHold) / Mathf.Max(1 - animation.RecoveryHold, .001f)));
        }
        float strike = Mathf.Clamp((time - prepare) / Mathf.Max(active, .001f), 0, 1);
        if (strike < animation.HighAt)
            return ReferenceFrame.Mix(windup, high, Smooth(strike / Mathf.Max(animation.HighAt, .001f)));
        if (strike < animation.ContactAt)
            return ReferenceFrame.Mix(high, contact, Smooth((strike - animation.HighAt) / Mathf.Max(animation.ContactAt - animation.HighAt, .001f)));
        return ReferenceFrame.Mix(contact, follow, Smooth((strike - animation.ContactAt) / Mathf.Max(animation.FollowAt - animation.ContactAt, .001f)));
    }


}
