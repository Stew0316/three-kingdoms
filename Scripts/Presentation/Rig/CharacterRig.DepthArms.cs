using Godot;

/// <summary>正背面抬手时允许手在肩前经过；固定骨长在纵深中求解，避免二维投影重合时把肘部折成一团。</summary>
public partial class CharacterRig
{
    private readonly float[] _depthArmScales = { 1, 1, 1, 1, 1, 1 };
    private readonly float[] _depthElbowDegrees = new float[2];
    public float DepthArmLengthError { get; private set; }
    public float DepthElbowDegrees(int upper) => _depthElbowDegrees[upper == 3 ? 0 : 1];

    private void ApplyDepthArms(ReferenceFrame frame)
    {
        var model = Actor.Presentation.Model;
        Vector2 Project(Vector3 point) => new(point.X, point.Y + point.Z * model.GroundDepthProjection);
        Vector3 Unproject(Vector2 point, float depth) => new(point.X, point.Y - depth * model.GroundDepthProjection, depth);
        float secondaryDepth = frame.HandDepth + model.DepthFacing * Mathf.Sqrt(Mathf.Max(0, 1 - frame.WeaponProjection * frame.WeaponProjection)) * frame.Separation;
        Vector2 secondary = frame.FarHand + Vector2.Up.Rotated(frame.Weapon) * frame.Separation * frame.WeaponProjection;
        DepthArmLengthError = 0;
        for (int side = 0; side < 2; side++)
        {
            int upper = side == 0 ? 3 : 6, lower = upper + 1, hand = upper + 2;
            _bones[upper].Position = model.JointPositions[upper];
            Vector3 start = Unproject(Skeleton.ToLocal(_bones[upper].GlobalPosition), 0);
            float first = model.JointPositions[lower].Length(), second = model.JointPositions[hand].Length();
            Vector3 target = Unproject(frame.FarHand, frame.HandDepth);
            if (side == 1)
            {
                Vector3 free = Unproject(frame.FreeHand, 0) - start, grip = Unproject(secondary, secondaryDepth) - start;
                float weight = SupportHandWeight(frame.Grip);
                // 释放阶段仍沿肩前方的弧线收手；只在未完全握住时限制半径，双手握持误差由制作检查捕获。
                float radius = Mathf.Lerp(free.Length(), grip.Length(), weight);
                if (weight < .9999f) radius = Mathf.Min(radius, first + second - .8f);
                target = start + free.Normalized().Slerp(grip.Normalized(), weight) * radius;
            }
            Vector3 delta = target - start;
            float distance = Mathf.Clamp(delta.Length(), .1f, first + second - .001f);
            Vector3 axis = delta.Normalized(), end = start + axis * distance;
            Vector3 pole = new(side == 0 ? -.1f : .1f, 1, .15f); // 肘部自然下垂，不能把正背面双臂平摊成翼状。
            Vector3 bend = (pole - axis * pole.Dot(axis)).Normalized();
            float along = (first * first - second * second + distance * distance) / (2 * distance);
            Vector3 middle = start + axis * along + bend * Mathf.Sqrt(Mathf.Max(0, first * first - along * along));
            Vector2 a = Project(middle) - Project(start), b = Project(end) - Project(middle);
            _bones[upper].GlobalTransform = Skeleton.GlobalTransform * new Transform2D(a.Angle() - Mathf.Pi / 2, Project(start));
            _bones[lower].GlobalTransform = Skeleton.GlobalTransform * new Transform2D(b.Angle() - Mathf.Pi / 2, Project(middle));
            _bones[hand].GlobalTransform = Skeleton.GlobalTransform * new Transform2D(b.Angle() - Mathf.Pi / 2, Project(end));
            _depthArmScales[upper - 3] = a.Length() / first;
            _depthArmScales[lower - 3] = _depthArmScales[hand - 3] = b.Length() / second;
            _depthElbowDegrees[side] = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp((distance * distance - first * first - second * second) / (2 * first * second), -1, 1)));
            DepthArmLengthError = Mathf.Max(DepthArmLengthError, Mathf.Max(Mathf.Abs(start.DistanceTo(middle) - first), Mathf.Abs(middle.DistanceTo(end) - second)));
        }
    }
}
