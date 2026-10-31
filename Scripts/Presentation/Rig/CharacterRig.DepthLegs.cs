using Godot;

/// <summary>正背面腿部在纵深平面屈膝，再投影到二维部件；身体移动和命中仍使用原二维世界。</summary>
public partial class CharacterRig
{
    private readonly float[] _depthLegScales = { 1, 1, 1, 1 };
    private readonly Vector2[] _depthAnkles = new Vector2[2];
    public float DepthLegLengthError { get; private set; }
    public float DepthFootReachError { get; private set; }
    public float DepthFootLift(int leg) => leg == 0 ? _referenceFrame.FarLift : _referenceFrame.NearLift;

    private Vector2 DepthStep(float phase)
    {
        float swing = Mathf.Clamp((phase - ReferenceSupport) / (1 - ReferenceSupport), 0, 1);
        float forward = phase < ReferenceSupport ? Mathf.Lerp(1, -1, phase / ReferenceSupport)
            : Mathf.Lerp(-1, 1, AnimationCurves.Smooth(swing));
        return new Vector2(Actor.Presentation.Model.DepthFacing * forward * Actor.Presentation.Animation.WalkStride,
            Mathf.Sin(swing * Mathf.Pi) * Actor.Presentation.Animation.WalkFootLift) * _walkWeight;
    }

    private Vector3 DepthSole(ReferenceFrame frame, int leg)
    {
        var foot = leg == 0 ? frame.FarFoot : frame.NearFoot;
        float depth = leg == 0 ? frame.FarDepth : frame.NearDepth;
        float lift = leg == 0 ? frame.FarLift : frame.NearLift;
        return new Vector3(foot.X, -lift, depth + foot.Y / Actor.Presentation.Model.GroundDepthProjection);
    }

    private float DepthWalkWaist(ReferenceFrame frame, float farPhase, float nearPhase)
    {
        float shin = _lowerLegLength - ReferenceFootHeight;
        float knee = Mathf.DegToRad(Actor.Presentation.Animation.WalkSupportKneeDegrees);
        float reach = Mathf.Sqrt(_thighLength * _thighLength + shin * shin + 2 * _thighLength * shin * Mathf.Cos(knee));
        float Height(int leg, float length)
        {
            Vector3 ankle = DepthSole(frame, leg) - new Vector3(0, ReferenceFootHeight, 0);
            var offset = Actor.Presentation.Model.JointPositions[leg == 0 ? 9 : 11].Rotated(frame.Waist);
            float x = ankle.X - frame.Pelvis.X - offset.X;
            return _pelvisHeight + ankle.Y - offset.Y - Mathf.Sqrt(Mathf.Max(1, length * length - x * x - ankle.Z * ankle.Z));
        }
        float far = Height(0, reach), near = Height(1, reach), waist;
        const float transfer = ReferenceSupport - .5f;
        if (farPhase < transfer) waist = Mathf.Lerp(near, far, AnimationCurves.Smooth(farPhase / transfer));
        else if (nearPhase < transfer) waist = Mathf.Lerp(far, near, AnimationCurves.Smooth(nearPhase / transfer));
        else waist = farPhase < ReferenceSupport ? far : near;
        return Mathf.Max(waist, Mathf.Max(Height(0, _thighLength + shin - .15f), Height(1, _thighLength + shin - .15f)));
    }

    private void ApplyDepthLegs(ReferenceFrame frame)
    {
        var model = Actor.Presentation.Model;
        Vector2 Project(Vector3 point) => new(point.X, point.Y + point.Z * model.GroundDepthProjection);
        DepthLegLengthError = DepthFootReachError = 0;
        float shin = _lowerLegLength - ReferenceFootHeight;
        for (int leg = 0; leg < 2; leg++)
        {
            int hip = leg == 0 ? 9 : 11, knee = hip + 1;
            _bones[hip].Position = model.JointPositions[hip];
            Vector2 hip2 = Skeleton.ToLocal(_bones[hip].GlobalPosition);
            Vector3 start = new(hip2.X, hip2.Y, 0);
            Vector3 target = DepthSole(frame, leg) - new Vector3(0, ReferenceFootHeight, 0);
            Vector3 delta = target - start;
            float distance = Mathf.Clamp(delta.Length(), .1f, _thighLength + shin - .001f);
            Vector3 axis = delta.Normalized();
            Vector3 end = start + axis * distance;
            Vector3 pole = new(0, 0, model.DepthFacing);
            Vector3 bend = (pole - axis * pole.Dot(axis)).Normalized();
            float along = (_thighLength * _thighLength - shin * shin + distance * distance) / (2 * distance);
            Vector3 middle = start + axis * along + bend * Mathf.Sqrt(Mathf.Max(0, _thighLength * _thighLength - along * along));
            Vector2 upper = Project(middle) - Project(start), lower = Project(end) - Project(middle);
            _bones[hip].GlobalTransform = Skeleton.GlobalTransform * new Transform2D(upper.Angle() - Mathf.Pi / 2, Project(start));
            _bones[knee].GlobalTransform = Skeleton.GlobalTransform * new Transform2D(lower.Angle() - Mathf.Pi / 2, Project(middle));
            _depthLegScales[hip - 9] = upper.Length() / _thighLength;
            _depthLegScales[knee - 9] = lower.Length() / shin;
            _depthAnkles[leg] = Project(end);
            DepthLegLengthError = Mathf.Max(DepthLegLengthError, Mathf.Max(Mathf.Abs(start.DistanceTo(middle) - _thighLength), Mathf.Abs(middle.DistanceTo(end) - shin)));
            DepthFootReachError = Mathf.Max(DepthFootReachError, target.DistanceTo(end));
        }
    }
}
