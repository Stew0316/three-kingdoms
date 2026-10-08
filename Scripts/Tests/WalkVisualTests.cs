using Godot;
using System;
using System.Threading.Tasks;

/// <summary>显式 --walk-test 启动的步态回归：检查实际位移、顶墙、停步、暂停，并输出真实渲染图。</summary>
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
                Player.Position = new Vector2(320, 235);
                float before = Player.WalkDistance;
                Player.SetMoveInput(direction);
                await Frames(15);
                Check(Player.WalkDistance > before + 20, $"方向 {direction} 按实际位移推进步态");
                Check(Player.Rig.Joints[10].Position.IsEqualApprox(new Vector2(0, Player.Rig.ThighLength))
                    && Player.Rig.Joints[12].Position.IsEqualApprox(new Vector2(0, Player.Rig.ThighLength)),
                    $"方向 {direction} 大腿骨长固定，膝点不伸缩");
                Check(Player.Rig.Parts[9].Scale.Abs().IsEqualApprox(farLegScale.Abs())
                    && Player.Rig.Parts[11].Scale.Abs().IsEqualApprox(nearLegScale.Abs()), $"方向 {direction} 大腿图层不随步幅拉伸");
                Check(Player.Rig.Joints[0].Position.Y > standingHip + .7f, $"方向 {direction} 移动适度沉胯");
            }
            Player.SetMoveInput(Vector2.Zero);
            await Frames(20);
            float stoppedPhase = Player.Rig.WalkPhase;
            await Frames(15);
            Check(Mathf.IsEqualApprox(stoppedPhase, Player.Rig.WalkPhase) && Player.Rig.WalkWeight < .01f,
                "停步收势后相位不再自行摆动");
            Check(Mathf.Abs(Player.Rig.Joints[0].Position.Y - standingHip) < .1f, "停步回到独立站立姿态");
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
            await Frames(5);
            Check(Mathf.IsEqualApprox(pausePhase, Player.Rig.WalkPhase), "暂停冻结步态");
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
