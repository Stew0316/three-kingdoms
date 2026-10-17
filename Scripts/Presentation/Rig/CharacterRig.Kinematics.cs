using Godot;
using static AnimationCurves;

/// <summary>把姿态目标解算为固定骨长关节与持械握点；不改变共享配置。</summary>
public partial class CharacterRig
{
    /// <summary>先落腰胯和足底，再确定长戟及握点，最后求解双臂；统一写入同一骨架。</summary>
    private void ApplyReferenceFrame(ReferenceFrame frame)
    {
        _bones[0].Position = new Vector2(0, -_pelvisHeight) + frame.Pelvis;
        _bones[0].Rotation = frame.Waist;
        _bones[1].Rotation = frame.Chest;
        _bones[2].Rotation = frame.Head;
        // 解算到独立靴掌的踝点，承重时全掌接地，落地/蹬离时绕跟或趾滚动。
        SolveReferenceChain(9, 10, ReferenceAnkle(frame.FarFoot, frame.FarFootRoll), _thighLength, _lowerLegLength - ReferenceFootHeight, 1);
        SolveReferenceChain(11, 12, ReferenceAnkle(frame.NearFoot, frame.NearFootRoll), _thighLength, _lowerLegLength - ReferenceFootHeight, 1);

        _bones[14].Position = frame.FarHand;
        _bones[14].Rotation = frame.Weapon;
        Vector2 blade = Vector2.Up.Rotated(frame.Weapon);
        Vector2 secondGrip = frame.FarHand + blade * frame.Separation;
        float gripWeight = Actor.Presentation.Model.DualWeapons ? 0 : SupportHandWeight(frame.Grip);
        Vector2 nearHand = frame.FreeHand.Lerp(secondGrip, gripWeight);
        var animation = Actor.Presentation.Animation;
        SolveReferenceChain(3, 4, frame.FarHand, _bones[4].Position.Length(), _bones[5].Position.Length(), animation.FarElbowBend < 0 ? -1 : 1);
        SolveReferenceChain(6, 7, nearHand, _bones[7].Position.Length(), _bones[8].Position.Length(), animation.NearElbowBend < 0 ? -1 : 1);
        // 中立腕让掌骨顺前臂，握点不动；武将可独立选择手腕对武器方向的混合程度。
        SetReferenceHandRotation(5, 4, frame.Weapon + .15f, animation.HandWeaponRotationWeight);
        SetReferenceHandRotation(8, 7, Mathf.LerpAngle(.05f, frame.Weapon, gripWeight), animation.HandWeaponRotationWeight);
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
        var angles = TwoBoneIk2D.Solve(offset, firstLength, secondLength, bend);
        _bones[upper].Rotation = angles.Root;
        _bones[lower].Rotation = angles.Joint;
    }

    private void SetReferenceAbsoluteRotation(int bone, float angle)
    {
        Vector2 direction = Vector2.Right.Rotated(angle);
        var parent = _bones[bone].GetParent<Node2D>();
        Vector2 local = parent.ToLocal(Skeleton.ToGlobal(direction)) - parent.ToLocal(Skeleton.GlobalPosition);
        _bones[bone].Rotation = local.Angle();
    }

    private void SetReferenceHandRotation(int hand, int forearm, float weaponAngle, float weight)
    {
        Vector2 origin = Skeleton.ToLocal(_bones[forearm].GlobalPosition);
        Vector2 end = Skeleton.ToLocal(_bones[hand].GlobalPosition);
        float neutral = (end - origin).Angle() - Mathf.Pi / 2;
        SetReferenceAbsoluteRotation(hand, Mathf.LerpAngle(neutral, weaponAngle, weight));
    }

    private float SupportHandWeight(float weight)
    {
        float start = Mathf.Clamp(Actor.Presentation.Animation.SupportHandAttachStart, 0, .95f);
        return start <= 0 ? weight : Smooth((weight - start) / (1 - start));
    }
}
