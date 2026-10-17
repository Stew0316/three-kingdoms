using Godot;
using static AnimationCurves;

/// <summary>按实际行走路程采样足底接触、脚掌滚动与重心转换。</summary>
public partial class CharacterRig
{
    /// <summary>脚底的接触、经过和摆动循环；脚距由关键姿态决定，步幅不移动髋关节。</summary>
    private Vector2 ReferenceStep(float phase, float facing)
    {
        float swing = Mathf.Clamp((phase - ReferenceSupport) / (1 - ReferenceSupport), 0, 1);
        float forward = phase < ReferenceSupport ? Mathf.Lerp(1, -1, phase / ReferenceSupport) : Mathf.Lerp(-1, 1, Smooth(swing));
        // 地面纵深与抬脚不同：南北向缩短投影步深，避免把地面位移画成上下蹦跳。
        Vector2 direction = new(_walkDirection.X * facing, _walkDirection.Y * .16f);
        // 上下移动仍保留侧面透视的换脚量，避免只有两脚一起纵向滑动。
        if (Mathf.Abs(direction.X) < .2f) direction.X = .45f;
        float lift = Mathf.Sin(swing * Mathf.Pi) * Actor.Presentation.Animation.WalkFootLift;
        return (direction * (forward * Actor.Presentation.Animation.WalkStride) - new Vector2(0, lift)) * _walkWeight;
    }

    /// <summary>轻微脚跟接地、全掌承重、抬跟蹬离；摆动末段回到下次接触角。</summary>
    private static float ReferenceStepRoll(float phase)
    {
        float degrees;
        if (phase < .09f) degrees = Mathf.Lerp(-8, 0, Smooth(phase / .09f));
        else if (phase < .40f) degrees = 0;
        else if (phase < ReferenceSupport) degrees = Mathf.Lerp(0, 12, Smooth((phase - .40f) / (ReferenceSupport - .40f)));
        else degrees = Mathf.Lerp(12, -8, Smooth((phase - ReferenceSupport) / (1 - ReferenceSupport)));
        return Mathf.DegToRad(degrees);
    }

    /// <summary>围绕后跟或前掌滚动时反求踝点，接触边保持在足底目标高度。</summary>
    private static Vector2 ReferenceAnkle(Vector2 sole, float roll)
    {
        Vector2 pivot = new(roll < 0 ? -4.5f : 5.5f, ReferenceFootHeight);
        return sole + new Vector2(pivot.X, 0) - pivot.Rotated(roll);
    }

    /// <summary>承重腿中段接近伸展，换脚略屈膝缓冲；双腿可达性只限制腰高，不缩放腿长。</summary>
    private float ReferenceWalkWaist(ReferenceFrame frame, float farPhase, float nearPhase)
    {
        float shin = _lowerLegLength - ReferenceFootHeight;
        float knee = Mathf.DegToRad(Actor.Presentation.Animation.WalkSupportKneeDegrees);
        float reach = Mathf.Sqrt(_thighLength * _thighLength + shin * shin + 2 * _thighLength * shin * Mathf.Cos(knee));
        Vector2 farAnkle = ReferenceAnkle(frame.FarFoot, frame.FarFootRoll);
        Vector2 nearAnkle = ReferenceAnkle(frame.NearFoot, frame.NearFootRoll);
        float WaistFor(Vector2 ankle, int hip, float length)
        {
            Vector2 offset = _bones[hip].Position.Rotated(frame.Waist);
            float x = ankle.X - frame.Pelvis.X - offset.X;
            return _pelvisHeight + ankle.Y - offset.Y - Mathf.Sqrt(Mathf.Max(1, length * length - x * x));
        }
        float farY = WaistFor(farAnkle, 9, reach);
        float nearY = WaistFor(nearAnkle, 11, reach);
        float waist;
        const float transfer = ReferenceSupport - .5f;
        if (farPhase < transfer) waist = Mathf.Lerp(nearY, farY, Smooth(farPhase / transfer));
        else if (nearPhase < transfer) waist = Mathf.Lerp(farY, nearY, Smooth(nearPhase / transfer));
        else waist = farPhase < ReferenceSupport ? farY : nearY;
        // 摆动脚也必须够得到；留微小余量，避免膝盖被IK钳成完全直线。
        float maximumReach = _thighLength + shin - .12f;
        return Mathf.Max(waist, Mathf.Max(WaistFor(farAnkle, 9, maximumReach), WaistFor(nearAnkle, 11, maximumReach)));
    }
}
