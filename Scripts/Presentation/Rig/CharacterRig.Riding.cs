using Godot;

public partial class CharacterRig
{
    /// <summary>骑乘适配层显式绑定，不依赖坐骑的节点名或城市类型。</summary>
    public HorseActor RidingMount { get; set; }
    /// <summary>取最终靴掌纹理的底边中心；IK不可达时仍与实际画出的脚一致。</summary>
    public Vector2 RidingSoleWorld => _referenceFeet[1] is Sprite2D foot
        ? foot.ToGlobal(new Vector2(foot.Texture.GetWidth() * .5f, foot.Texture.GetHeight()))
        : _bones[12].ToGlobal(new Vector2(0, _lowerLegLength));

    /// <summary>主拳掌的标定握点经最终渲染变换得到世界端点，不读取手部目标。</summary>
    public Vector2 RidingReinWorld => _partByBone[5] is Sprite2D hand
        ? hand.ToGlobal(_partOffsets[5].AffineInverse() * Vector2.Zero) : _bones[5].GlobalPosition;

    /// <summary>坐骨跟随马鞍，腰髋与胸肩分开吸收跑步俯仰；手执缰、脚踩镫，沿用武将固定骨长。</summary>
    private void ProcessRidingPose()
    {
        // 骑乘期间同步基线，防止下马首帧把整段骑行距离当成一步步态。
        _lastTime = Actor.VisualTime;
        _lastWalkDistance = Actor.WalkDistance;
        _walkWeight = 0;
        var horse = RidingMount;
        float facing = horse?.Facing ?? (Actor.FacingDirection.X < -.1f ? -1 : 1);
        float scale = ArtScale / .05f;
        Scale = new Vector2(facing * scale, scale * Actor.Presentation.Model.DepthScale);

        // 鞍点已经包含马躯干起伏与俯仰造成的位移；不能再叠加一套骑手弹跳时钟。
        // 用世界鞍点换到人物局部坐标，坐骑重命名或换父节点后仍保持实际接触。
        Position = horse?.PaintedRig!=null ? Actor.ToLocal(horse.PaintedRig.SaddleWorld)+new Vector2(0,19)
            : new Vector2(0,-24);
        Rotation = 0;
        Modulate = Colors.White;
        float pitch = horse?.BodyPitch ?? 0;
        float lean = horse?.RiderLean ?? 0;
        float weight = horse?.GaitWeight ?? 0;
        var riding = HorseRiderPose.Sample(pitch,lean,horse?.RenderGaitPhase??0,weight,_pelvisHeight);

        _referenceFrame = ReferenceFrame.From(Actor.Presentation.Animation.StandingPose);
        _referenceFrame.Pelvis = new Vector2(0, -2);
        // Chest/Head 都是父骨相对角度：腰随鞍滚动，胸椎抵消大部分摇摆并保持轻前倾。
        // 这使肩髋有相对运动，视线仍稳定向前，避免整个人像木柱一样跟马背平移。
        _referenceFrame.Waist = riding.Waist;
        _referenceFrame.Chest = riding.Chest;
        _referenceFrame.Head = riding.Head;

        // 手脚属于鞍的接触点，绕同一坐骨基准变换；腰部摇摆由膝、肘的固定骨长 IK 吸收。
        _referenceFrame.FarFoot = riding.FarFoot;
        _referenceFrame.NearFoot = riding.NearFoot;
        _referenceFrame.FarHand = riding.FarHand;
        _referenceFrame.FreeHand = riding.NearHand;
        _referenceFrame.Grip = 0;
        _referenceFrame.Weapon = _referenceFrame.OffhandWeapon = 0;
        _referenceFrame.FarFootRoll = _referenceFrame.NearFootRoll = riding.FootRoll;
        ApplyReferenceFrame(_referenceFrame);

        // 执缰时拳掌沿前臂保持中立，不能继承各武将为长柄刀剑设置的握械腕角。
        SetReferenceHandRotation(5, 4, 0, 0);
        SetReferenceHandRotation(8, 7, 0, 0);
        // 披风与冠翎只作滞后小摆，主节奏仍来自实际路程，不用视觉时间让停马继续颠簸。
        _bones[13].Rotation = riding.Cape;
        _bones[15].Rotation = riding.Crest;
        SyncParts();
    }
}
