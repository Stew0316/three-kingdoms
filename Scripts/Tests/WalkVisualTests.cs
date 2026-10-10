using Godot;
using System;
using System.Threading.Tasks;

/// <summary>显式 --walk-test 启动的步态回归：检查支撑与摆腿、固定骨长、起停暂停，并输出真实渲染图。</summary>
public partial class WalkVisualTests : Node
{
    private Arena _arena;
    private Combatant Player => _arena.Player;
    private int _checks;
    private bool HasRenderer => DisplayServer.GetName() != "headless";

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        CallDeferred(MethodName.Run);
    }

    private async void Run()
    {
        try
        {
            _arena = (Arena)GetTree().CurrentScene;
            Player.GetNode(NodeNames.Controller).ProcessMode = ProcessModeEnum.Disabled;
            Check(_arena.IsSoloPractice && !_arena.Enemy.Visible, "默认单人练习，魏延隐藏");
            Check(_arena.Enemy.ProcessMode == ProcessModeEnum.Disabled && _arena.Enemy.CollisionLayer == 0
                && _arena.Enemy.GetNode<Area2D>(NodeNames.Hurtbox).CollisionLayer == 0, "魏延处理、实体碰撞和受击关闭");
            await Frames(15);
            float standingHip = Player.Rig.Joints[0].Position.Y;
            Check(!Player.Rig.Parts[10].FlipH && Player.Rig.Parts[12].FlipH == Player.Presentation.AlignBootsForward,
                "小腿方向遵循实际素材配置，不强制镜像新素材");
            Vector2 farLegScale = Player.Rig.Parts[9].Scale;
            Vector2 nearLegScale = Player.Rig.Parts[11].Scale;
            if (HasRenderer) await Screenshot("lubu-practice");
            // 清除遮挡腿部的装饰，只在此测试进程中生效；正式练习场仍保留光环。
            Player.GetNode<GroundEffects>(NodeNames.GroundEffects).Hide();
            _arena.GetNode(NodeNames.Hud).ProcessMode = ProcessModeEnum.Disabled;
            foreach (Vector2 direction in new[] { Vector2.Right, Vector2.Left, Vector2.Up, Vector2.Down,
                new Vector2(1, 1).Normalized(), new Vector2(-1, -1).Normalized(),
                new Vector2(1, -1).Normalized(), new Vector2(-1, 1).Normalized() })
            {
                // 给纵向循环留出完整步程，避免走到边界后把停步误判成步态失败。
                Player.Position = new Vector2(320, 210) - direction * 50;
                float before = Player.WalkDistance;
                Player.SetMoveInput(direction);
                await Frames(15);
                Check(Player.WalkDistance > before + 20, $"方向 {direction} 按实际位移推进步态");
                await VerifyWalkingCycle(direction, farLegScale, nearLegScale);
            }
            Player.SetMoveInput(Vector2.Zero);
            await Frames(20);
            float stoppedPhase = Player.Rig.WalkPhase;
            await Frames(15);
            Check(Mathf.IsEqualApprox(stoppedPhase, Player.Rig.WalkPhase) && Player.Rig.WalkWeight < .01f,
                "停步收势后相位不再自行摆动");
            Check(Mathf.Abs(Player.Rig.Joints[0].Position.Y - standingHip) < .1f, "停步回到独立站立姿态");
            var stoppedLegs = LegTransforms();
            await Frames(8);
            Check(SameTransforms(stoppedLegs, LegTransforms()), "停步后双腿与独立靴掌稳定，不继续摆动");
            Check(Player.TryAction(CombatAction.Basic), "站立可正常进入攻击姿态");
            await Frames(7);
            if (Player.Presentation.ReferencePoseSet)
            {
                // 新样板以沉胯横戟蓄势，不能再用旧动作的站直腰高作为攻击验收标准。
                Check(Player.Rig.Joints[0].Position.Y > standingHip + 2
                    && Mathf.IsEqualApprox(Player.Rig.Joints[10].Position.Length(), Player.Rig.ThighLength)
                    && Mathf.IsEqualApprox(Player.Rig.Joints[12].Position.Length(), Player.Rig.ThighLength)
                    && Mathf.IsEqualApprox(Player.Rig.Joints[10].GetLength(), Player.Rig.LowerLegLength)
                    && Mathf.IsEqualApprox(Player.Rig.Joints[12].GetLength(), Player.Rig.LowerLegLength),
                    "参考攻击姿态比站立沉胯至少 2 个骨架单位，大小腿保持固定骨长");
            }
            else
            {
                Check(Mathf.IsEqualApprox(Player.Rig.Joints[0].Position.Y, -Player.Rig.RestPelvisHeight)
                    && Mathf.IsEqualApprox(Player.Rig.Joints[10].Position.Y, Player.Rig.ThighLength),
                    "旧攻击退出行走沉胯，沿用相同固定骨长");
            }
            await Frames(30);
            Player.Position = new Vector2(570, 230);
            Player.SetMoveInput(Vector2.Right);
            await Frames(35);
            float wallPhase = Player.Rig.WalkPhase;
            await Frames(20);
            Check(Player.WalkVelocity.Length() < 1 && Player.Rig.WalkWeight < .01f
                && Mathf.IsEqualApprox(wallPhase, Player.Rig.WalkPhase), "持续顶墙停止迈步");
            Player.Position = new Vector2(320, 235);
            Player.SetMoveInput(Vector2.Left);
            await Frames(10);
            GetTree().Paused = true;
            float pausePhase = Player.Rig.WalkPhase;
            var pausedLegs = LegTransforms();
            await Frames(5);
            Check(Mathf.IsEqualApprox(pausePhase, Player.Rig.WalkPhase)
                && SameTransforms(pausedLegs, LegTransforms()), "暂停同时冻结步态相位、双腿和靴掌");
            GetTree().Paused = false;
            Player.SetMoveInput(Vector2.Zero);
            Player.TryAction(CombatAction.Dodge);
            float actionDistance = Player.WalkDistance;
            await Frames(12);
            Check(Mathf.IsEqualApprox(actionDistance, Player.WalkDistance), "闪避位移不被当作行走步长");
            await Frames(25);
            // 穿过原魏延位置，验证不是留下一个看不见的碰撞体。
            Player.Position = new Vector2(390, 229);
            Player.SetMoveInput(Vector2.Right);
            await Frames(30);
            Check(Player.Position.X > 450 && Player.Health.CurrentHealth == Player.Health.MaxHealth,
                "可穿过魏延原位置，练习期间无战斗伤害");
            if (HasRenderer)
            {
                Player.Position = new Vector2(220, 235);
                Player.SetMoveInput(Vector2.Right);
                await Frames(8);
                await CaptureCycle("right");
                Player.SetMoveInput(Vector2.Left);
                await Frames(8);
                await CaptureCycle("left");
                Player.SetMoveInput(Vector2.Zero);
                await Frames(15);
                await Screenshot("lubu-walk-stopped");
            }
            GetTree().ReloadCurrentScene();
            await Frames(10);
            _arena = (Arena)GetTree().CurrentScene;
            Check(_arena.IsSoloPractice && !_arena.Enemy.Visible && !_arena.Finished,
                "重开仍保持单人练习，未意外结算");
            GD.Print($"步态回归通过：{_checks} 项；真实渲染：{HasRenderer}。");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"步态回归失败：{exception}");
            GetTree().Paused = false;
            GetTree().Quit(1);
        }
    }

    /// <summary>检查最终骨架而非复算 IK；整周期覆盖每条腿的承重伸展、前摆屈膝及落脚。</summary>
    private async Task VerifyWalkingCycle(Vector2 direction, Vector2 farLegScale, Vector2 nearLegScale)
    {
        var rig = Player.Rig;
        float startDistance = Player.WalkDistance;
        float targetDistance = Player.Presentation.WalkCycleDistance * 1.2f;
        float[] leastBend = { 180, 180 }, mostBend = { 0, 0 };
        float[] leastFootAngle = { 180, 180 }, mostFootAngle = { -180, -180 };
        float[] mostContrast = { 0, 0 };
        float[] supportBendSum = { 0, 0 }, swingBendPeak = { 0, 0 }, contactError = { 0, 0 };
        int[] supportSamples = { 0, 0 };
        bool fixedLengths = true, fixedArtScale = true;
        int samples = 0;
        while (Player.WalkDistance - startDistance < targetDistance && samples < 180)
        {
            await Frames(1);
            samples++;
            fixedLengths &= HasFixedLegLengths();
            fixedArtScale &= rig.Parts[9].Scale.Abs().IsEqualApprox(farLegScale.Abs())
                && rig.Parts[11].Scale.Abs().IsEqualApprox(nearLegScale.Abs());
            for (int leg = 0; leg < 2; leg++)
            {
                int knee = leg == 0 ? 10 : 12;
                float bend = Mathf.Abs(Mathf.RadToDeg(rig.Joints[knee].Rotation));
                leastBend[leg] = Mathf.Min(leastBend[leg], bend);
                mostBend[leg] = Mathf.Max(mostBend[leg], bend);
                float otherBend = Mathf.Abs(Mathf.RadToDeg(rig.Joints[leg == 0 ? 12 : 10].Rotation));
                mostContrast[leg] = Mathf.Max(mostContrast[leg], otherBend - bend);
                if (Player.Presentation.ReferencePoseSet)
                {
                    Sprite2D foot = Foot(leg);
                    // 消除整个人物的左右镜像和透视缩放，只观察独立靴掌相对骨架的转动。
                    Vector2 axis = rig.Skeleton.ToLocal(foot.ToGlobal(Vector2.Right))
                        - rig.Skeleton.ToLocal(foot.GlobalPosition);
                    float angle = Mathf.RadToDeg(axis.Angle());
                    leastFootAngle[leg] = Mathf.Min(leastFootAngle[leg], angle);
                    mostFootAngle[leg] = Mathf.Max(mostFootAngle[leg], angle);
                    float phase = Mathf.PosMod(rig.WalkPhase + leg * .5f, 1);
                    // 只在承重中段验收伸展；落地缓冲和后脚蹬离允许更大屈膝。
                    if (phase >= .18f && phase <= .36f)
                    {
                        supportBendSum[leg] += bend;
                        supportSamples[leg]++;
                        Vector2 size = foot.Texture.GetSize();
                        float heel = rig.Skeleton.ToLocal(foot.ToGlobal(new Vector2(0, size.Y))).Y;
                        float toe = rig.Skeleton.ToLocal(foot.ToGlobal(size)).Y;
                        contactError[leg] = Mathf.Max(contactError[leg], Mathf.Abs(Mathf.Max(heel, toe)));
                    }
                    if (phase > rig.ReferenceSupportFraction + .08f && phase < .95f)
                        swingBendPeak[leg] = Mathf.Max(swingBendPeak[leg], bend);
                }
            }
        }
        Check(Player.WalkDistance - startDistance >= targetDistance, $"方向 {direction} 完成整轮真实行走采样");
        Check(fixedLengths, $"方向 {direction} 整周期大小腿骨长固定，膝点不伸缩");
        Check(fixedArtScale, $"方向 {direction} 整周期大腿图层不随步幅拉伸");
        if (!Player.Presentation.ReferencePoseSet) return;
        for (int leg = 0; leg < 2; leg++)
        {
            string name = leg == 0 ? "远腿" : "近腿";
            float supportBend = supportBendSum[leg] / Mathf.Max(1, supportSamples[leg]);
            GD.Print($"步态实测：{direction} {name} 屈膝 {leastBend[leg]:F1}°～{mostBend[leg]:F1}°，"
                + $"承重均值 {supportBend:F1}°、摆动峰值 {swingBendPeak[leg]:F1}°，"
                + $"两腿最大屈膝差 {mostContrast[leg]:F1}°，靴掌 {leastFootAngle[leg]:F1}°～{mostFootAngle[leg]:F1}°，接地误差 {contactError[leg]:F2}");
            Check(leastBend[leg] < 35, $"方向 {direction} {name} 能伸展支撑身体，不全程半蹲");
            Check(supportSamples[leg] >= 2 && supportBend is >= 8 and < 35,
                $"方向 {direction} {name} 承重中段较直并保留膝关节余量");
            Check(swingBendPeak[leg] > 40 && swingBendPeak[leg] - supportBend > 10,
                $"方向 {direction} {name} 有明确前摆屈膝，不锁成直腿");
            // 俯视纵向移动包含地面深度位移，不能把它误作抬脚高度；水平移动才有固定的屏幕接地线。
            if (Mathf.Abs(direction.Y) < .01f)
                Check(contactError[leg] < 1.25f, $"方向 {direction} {name} 承重中段靴底接地，不穿地或悬空");
            Check(mostContrast[leg] > 12, $"方向 {direction} {name} 与另一腿交替伸屈，承重和迈步有区别");
            Check(mostFootAngle[leg] - leastFootAngle[leg] > 5,
                $"方向 {direction} {name} 靴掌参与落脚与蹬离，不全程平移");
        }
    }

    private bool HasFixedLegLengths() => Player.Rig.Joints[10].Position.IsEqualApprox(new Vector2(0, Player.Rig.ThighLength))
        && Player.Rig.Joints[12].Position.IsEqualApprox(new Vector2(0, Player.Rig.ThighLength))
        && Mathf.IsEqualApprox(Player.Rig.Joints[9].GetLength(), Player.Rig.ThighLength)
        && Mathf.IsEqualApprox(Player.Rig.Joints[11].GetLength(), Player.Rig.ThighLength)
        && Mathf.IsEqualApprox(Player.Rig.Joints[10].GetLength(), Player.Rig.LowerLegLength)
        && Mathf.IsEqualApprox(Player.Rig.Joints[12].GetLength(), Player.Rig.LowerLegLength);

    private Sprite2D Foot(int leg)
    {
        string name = leg == 0 ? "远靴掌图层" : "近靴掌图层";
        foreach (var part in Player.Rig.DrawOrderedParts)
            if (part.Name == name) return part;
        throw new InvalidOperationException($"参考骨架缺少{name}");
    }

    /// <summary>记录实际绘制所用的腿和脚，暂停验证同时覆盖相位之外的独立脚掌动画。</summary>
    private Transform2D[] LegTransforms()
    {
        var rig = Player.Rig;
        if (!Player.Presentation.ReferencePoseSet)
            return new[] { rig.Joints[9].GlobalTransform, rig.Joints[10].GlobalTransform,
                rig.Joints[11].GlobalTransform, rig.Joints[12].GlobalTransform };
        return new[] { rig.Joints[9].GlobalTransform, rig.Joints[10].GlobalTransform,
            rig.Joints[11].GlobalTransform, rig.Joints[12].GlobalTransform,
            Foot(0).GlobalTransform, Foot(1).GlobalTransform };
    }

    private static bool SameTransforms(Transform2D[] before, Transform2D[] after)
    {
        for (int index = 0; index < before.Length; index++)
            if (!before[index].IsEqualApprox(after[index])) return false;
        return true;
    }

    /// <summary>输出一整轮正常尺寸截图与角色局部连拍，供肉眼检查承重、换脚和膝盖连接。</summary>
    private async Task CaptureCycle(string direction)
    {
        using var sheet = Image.CreateEmpty(240 * 8, 368, false, Image.Format.Rgba8);
        for (int frame = 0; frame < 32; frame++)
        {
            await Frames(1);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var shot = GetViewport().GetTexture().GetImage();
            float scale = shot.GetWidth() / 640f;
            var rect = new Rect2I((int)((Player.Position.X - 30) * scale), (int)((Player.Position.Y - 88) * scale),
                (int)(60 * scale), (int)(92 * scale));
            using var detail = shot.GetRegion(rect);
            detail.Convert(Image.Format.Rgba8); // D3D12 与 OpenGL 的读回格式可能不同，统一后再拼接。
            detail.Resize(240, 368, Image.Interpolation.Nearest);
            detail.SavePng($"res://Build/lubu-walk-{direction}-{frame:D2}.png");
            if (frame % 4 == 0) sheet.BlitRect(detail, new Rect2I(0, 0, 240, 368), new Vector2I(frame / 4 * 240, 0));
        }
        sheet.SavePng($"res://Build/lubu-walk-{direction}-sheet.png");
    }

    private async Task Screenshot(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        image.SavePng($"res://Build/{name}.png");
    }

    private async Task Frames(int count)
    {
        for (int frame = 0; frame < count; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
        GD.Print($"通过：{message}");
    }
}
