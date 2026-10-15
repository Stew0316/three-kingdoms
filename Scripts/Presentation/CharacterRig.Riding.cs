using Godot;

public partial class CharacterRig
{
    /// <summary>骑乘复用原武将拆件，屈髋弯膝踩镫、双手前伸执缰，身体随实际马步上下起伏。</summary>
    private void ProcessRidingPose()
    {
        // 骑乘期间同步基线，防止下马首帧把整段骑行距离当成一步步态。
        _lastTime=Actor.VisualTime; _lastWalkDistance=Actor.WalkDistance;
        _walkWeight=0;
        var horse = Actor.GetNodeOrNull<CityHorse>("RidingHorse");
        float facing = horse?.Facing ?? (Actor.FacingDirection.X < -.1f ? -1 : 1);
        float scale = ArtScale / .05f;
        Scale = new Vector2(facing * scale, scale * Actor.Presentation.Model.DepthScale);
        // 与马背读取同一状态，起停和转向时不再各自计算造成错位。
        Position = new Vector2(0, -24 + (horse?.BodyBob ?? 0));
        Rotation = 0; Modulate = Colors.White;
        _referenceFrame = ReferenceFrame.From(Actor.Presentation.Animation.StandingPose);
        _referenceFrame.Pelvis = new Vector2(0, -2);
        _referenceFrame.Chest = .1f;
        _referenceFrame.Waist = 0;
        _referenceFrame.FarFoot = new Vector2(10, -9);
        _referenceFrame.NearFoot = new Vector2(15, -5);
        _referenceFrame.FarHand = new Vector2(16, -36);
        _referenceFrame.FreeHand = new Vector2(19, -32);
        _referenceFrame.Grip = 0;
        _referenceFrame.FarFootRoll = _referenceFrame.NearFootRoll = 0;
        ApplyReferenceFrame(_referenceFrame);
        SyncParts();
    }
}


