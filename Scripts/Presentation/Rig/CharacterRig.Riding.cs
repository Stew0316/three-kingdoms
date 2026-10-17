using Godot;

public partial class CharacterRig
{
    /// <summary>当前近靴底的实际骨架目标，供马镫跟随；不读取或修改共享模型资源。</summary>
    public Vector2 RidingSoleWorld => Skeleton.ToGlobal(_referenceFrame.NearFoot);

    /// <summary>坐骨跟随马鞍，腰髋与胸肩分开吸收跑步俯仰；手执缰、脚踩镫，沿用武将固定骨长。</summary>
    private void ProcessRidingPose()
    {
        // 骑乘期间同步基线，防止下马首帧把整段骑行距离当成一步步态。
        _lastTime = Actor.VisualTime;
        _lastWalkDistance = Actor.WalkDistance;
        _walkWeight = 0;
        var horse = Actor.GetNodeOrNull<CityHorse>("RidingHorse");
        float facing = horse?.Facing ?? (Actor.FacingDirection.X < -.1f ? -1 : 1);
        float scale = ArtScale / .05f;
        Scale = new Vector2(facing * scale, scale * Actor.Presentation.Model.DepthScale);

        // 鞍点已经包含马躯干起伏与俯仰造成的位移；不能再叠加一套骑手弹跳时钟。
        Position = new Vector2(0, -24) + (horse?.SaddleOffset ?? Vector2.Zero);
        Rotation = 0;
        Modulate = Colors.White;
        float pitch = horse?.BodyPitch ?? 0;
        float lean = horse?.RiderLean ?? 0;
        float phase = (horse?.GaitPhase ?? 0) * Mathf.Tau;
        float weight = horse?.GaitWeight ?? 0;

        _referenceFrame = ReferenceFrame.From(Actor.Presentation.Animation.StandingPose);
        _referenceFrame.Pelvis = new Vector2(0, -2);
        // Chest/Head 都是父骨相对角度：腰随鞍滚动，胸椎抵消大部分摇摆并保持轻前倾。
        // 这使肩髋有相对运动，视线仍稳定向前，避免整个人像木柱一样跟马背平移。
        _referenceFrame.Waist = pitch * .7f;
        _referenceFrame.Chest = .045f + lean - _referenceFrame.Waist * .82f;
        float upperBodyAngle = _referenceFrame.Waist + _referenceFrame.Chest;
        _referenceFrame.Head = .015f - upperBodyAngle * .9f;

        // 手脚属于鞍的接触点，绕同一坐骨基准变换；腰部摇摆由膝、肘的固定骨长 IK 吸收。
        Vector2 seat = new(0, -_pelvisHeight - 2);
        Vector2 FollowSaddle(Vector2 point) => seat + (point - seat).Rotated(pitch);
        _referenceFrame.FarFoot = FollowSaddle(new Vector2(10, -9));
        _referenceFrame.NearFoot = FollowSaddle(new Vector2(15, -5));
        Vector2 reinFollow = new(lean * 4, -lean * 2);
        _referenceFrame.FarHand = FollowSaddle(new Vector2(16, -36)) + reinFollow;
        _referenceFrame.FreeHand = FollowSaddle(new Vector2(19, -32)) + reinFollow;
        _referenceFrame.Grip = 0;
        _referenceFrame.Weapon = _referenceFrame.OffhandWeapon = 0;
        _referenceFrame.FarFootRoll = _referenceFrame.NearFootRoll = pitch * .7f;
        ApplyReferenceFrame(_referenceFrame);

        // 执缰时拳掌沿前臂保持中立，不能继承各武将为长柄刀剑设置的握械腕角。
        SetReferenceHandRotation(5, 4, 0, 0);
        SetReferenceHandRotation(8, 7, 0, 0);
        // 披风与冠翎只作滞后小摆，主节奏仍来自实际路程，不用视觉时间让停马继续颠簸。
        _bones[13].Rotation = -.08f - lean * .45f + Mathf.Sin(phase - .65f) * .035f * weight;
        _bones[15].Rotation = -upperBodyAngle * .12f + Mathf.Sin(phase - .9f) * .025f * weight;
        SyncParts();
    }
}
