using Godot;
using static AnimationCurves;

/// <summary>旧拆件资源的回退姿态；正式武将使用独立配置的参考姿态。</summary>
public partial class CharacterRig
{
    private const float WeaponRestAngle = 1.2f; // 基础持械角：刃端指向前上方。

    /// <summary>唯一姿态选择入口：同一骨架复用关节，各状态只生成目标值，不各自争抢骨骼写入。</summary>
    private void SamplePoseTargets(bool martial, float time, float stride)
    {
        System.Array.Clear(_angles);
        SampleStandPose(time, martial);
        switch (Actor.CurrentState)
        {
            case Combatant.State.Move: SampleMovePose(stride, martial); break;
            case Combatant.State.Attack:
            case Combatant.State.Dodge: SampleActionPose(); break;
            case Combatant.State.Hurt: SampleHurtPose(); break;
        }
        // 反击是显式的上身覆盖层，只覆盖列出的关节，不混入行走腿部循环。
        if(Actor.CounterRemaining>0) SampleCounterPose();
    }

    /// <summary>站立警戒：持械稳定、轻微呼吸，腿部不播放移动循环。</summary>
    private void SampleStandPose(float time, bool martial)
    {
        _angles[1] = martial ? .035f + Mathf.Sin(time * 2.8f) * .008f : Mathf.Sin(time * 2.8f) * .025f;
        _angles[2] = -_angles[1] * .5f;
        _angles[6]=.45f; _angles[7]=-1.45f; _angles[14]=WeaponRestAngle;
        _angles[13]=.12f+Mathf.Sin(time*4)*.09f;
        _angles[15]=Mathf.Sin(time*5)*.12f;
    }

    /// <summary>移动姿态只负责行走目标；武艺步态采用沉胯换步，未启用时保留回退步态。</summary>
    private void SampleMovePose(float stride, bool martial)
    {
        if (martial)
        {
            float transfer = Mathf.Sin(_walkPhase * Mathf.Tau) * _walkWeight;
            _angles[0] = transfer * .018f;
            _angles[1] = .035f + _walkWeight * .04f - transfer * .025f;
            _angles[2] = -_angles[1] * .6f;
            _angles[13] = .12f + _walkWeight * .10f + transfer * .045f;
        }
        else
        {
            _angles[1] += .06f; _angles[2] = -_angles[1] * .5f;
            _angles[9]=stride*.52f; _angles[11]=-stride*.52f;
            _angles[10]=Mathf.Max(0,-stride)*.65f; _angles[12]=Mathf.Max(0,stride)*.65f;
            _angles[13] += .25f;
        }
    }

    /// <summary>出招与闪避独立读取动作时钟，保留原前摇、生效与后摇，行走循环不驱动攻击。</summary>
    private void SampleActionPose()
    {
        var s=Actor.Spec; float at=Actor.ActionTime;
        float wind=Smooth(at/Mathf.Max(s.Prepare,.001f));
        float hit=Smooth((at-s.Prepare)/Mathf.Max(s.Active,.001f));
        float recovery=Smooth((at-s.Prepare-s.Active)/Mathf.Max(s.Recover,.001f));
        float pose=(-wind+hit*2.1f)*(1-recovery);
        _angles[0]=pose*.18f; _angles[1]=pose*.38f; _angles[2]=-pose*.2f;
        _angles[6]=.45f+pose*1.15f; _angles[7]=-1.45f+pose*.65f; _angles[14]=WeaponRestAngle+pose*.8f;
        _angles[9]=-.35f*wind*(1-recovery); _angles[11]=.3f*wind*(1-recovery);
        _angles[12]=.22f*wind*(1-recovery); _angles[13]=.2f-pose*.45f;
        if(Actor.CurrentMotion is CombatMotion.Dash or CombatMotion.Dodge) { _angles[1]=.35f; _angles[9]=-.7f; _angles[11]=.7f; }
    }

    /// <summary>受击目标与行走目标分离；肩胸收缩、抬臂防御，不翻转整个人物。</summary>
    private void SampleHurtPose()
    {
        _angles[0]=Actor.LastDamage.KnockbackDirection.X*.08f;
        _angles[1]=-.19f; _angles[2]=.11f; _angles[6]=.2f;
    }

    /// <summary>反击的局部上身覆盖层：躯干扭转、披风随动，兵器绕手旋转。</summary>
    private void SampleCounterPose()
    {
        float p=1-Actor.CounterRemaining/Actor.CounterDuration;
        float wave=Mathf.Sin(p*Mathf.Tau);
        _angles[0]=wave*.20f; _angles[1]=wave*.24f;
        _angles[6]=.3f+wave*.28f; _angles[7]=-1.2f-wave*.22f;
        _angles[14]=WeaponRestAngle+p*Mathf.Tau*1.15f;
        _angles[13]=-.45f-wave*.28f;
    }

    /// <summary>固定骨长的两段腿求解：以落脚点弯曲髋膝，超出可达范围时约束落点，不拉伸腿段。</summary>
    private void ApplyMartialLeg(int thigh, int shin, float side, float phase, float facing)
    {
        var config = Actor.Presentation.Animation;
        // 62% 周期承重，38% 周期低抬脚前摆；两脚错开半周期，保留双脚接地的时间。
        const float support = .62f;
        float swing = Mathf.Clamp((phase - support) / (1 - support), 0, 1);
        float stride = phase < support ? Mathf.Lerp(1, -1, phase / support) : Mathf.Lerp(-1, 1, Smooth(swing));
        float lift = Mathf.Pow(Mathf.Sin(swing * Mathf.Pi), 2) * config.WalkFootLift * _walkWeight;
        Vector2 direction = new(_walkDirection.X * facing, _walkDirection.Y * .45f);
        float width = Mathf.Lerp(config.StandStanceWidth, config.WalkStanceWidth, _walkWeight);
        Vector2 sole = new Vector2(side * width, side * .5f)
            + direction * (stride * config.WalkStride * _walkWeight) - new Vector2(0, lift);
        // 在骨盆局部坐标求解，左右镜像不改变膝盖朝向；两腿均向角色前方屈膝。
        Vector2 target = _bones[thigh].GetParent<Node2D>().ToLocal(Skeleton.ToGlobal(sole));
        Vector2 offset = target - _bones[thigh].Position;
        var angles = TwoBoneIk2D.Solve(offset, _thighLength, _lowerLegLength, 1);
        _bones[thigh].Rotation = Mathf.LerpAngle(_bones[thigh].Rotation, angles.Root, _martialWeight);
        _bones[shin].Rotation = Mathf.LerpAngle(_bones[shin].Rotation, angles.Joint, _martialWeight);
    }

    /// <summary>远手对齐兵器上的独立握点，在胸部局部坐标内求解两节 IK；横向镜像不改变求解结果。</summary>
    private void SolveSupportArm()
    {
        float gripDistance=Actor.Presentation?.Model.SupportGripDistance ?? 10;
        Vector2 target=_bones[1].ToLocal(_bones[14].ToGlobal(new Vector2(0,-gripDistance)));
        Vector2 diff=target-_bones[3].Position;
        float upperLength=_bones[4].Position.Length(),lowerLength=_bones[5].Position.Length();
        var angles = TwoBoneIk2D.Solve(diff, upperLength, lowerLength, -1, .1f);
        _bones[3].Rotation = angles.Root;
        _bones[4].Rotation = angles.Joint;
    }
}
