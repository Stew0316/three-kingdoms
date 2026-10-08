using Godot;
using System;
using System.Threading.Tasks;

/// <summary>显式 --pose-test 验收三姿态：真实移动和出招驱动，不提供强制姿态或改写时钟入口。</summary>
public partial class ReferencePoseTests : Node
{
    private Arena _arena;
    private int _checks;
    private Combatant Player => _arena.Player;
    private CharacterRig Rig => Player.Rig;
    private bool HasRenderer => DisplayServer.GetName() != "headless";
    // 统一裁区包含竖戟、后拖戟与横戟，保持三个姿态的显示尺度一致。
    private static readonly Vector2 CropOffset = new(-80, -115);
    private static readonly Vector2 CropSize = new(160, 140);

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
            // 只在截图进程关闭地面装饰，保留真实的角色缩放、部件和动作配置。
            Player.GetNode<GroundEffects>(NodeNames.GroundEffects).Hide();
            _arena.GetNode(NodeNames.Hud).ProcessMode = ProcessModeEnum.Disabled;
            _arena.Resolver.PassivesEnabled = false;
            Check(_arena.IsSoloPractice && !_arena.Enemy.Visible
                && _arena.Enemy.ProcessMode == ProcessModeEnum.Disabled, "三姿态验收保持魏延关闭");
            Check(Player.Presentation.ReferencePoseSet, "吕布已启用三姿态参考配置");
            Check(Rig.Joints.Count == 16, "三姿态复用同一套 16 业务关节");
            if (HasRenderer)
            {
                Error directory = DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://Build/reference-frames"));
                if (directory != Error.Ok) throw new InvalidOperationException($"无法创建动画帧目录：{directory}");
            }

            Player.Position = new Vector2(270, 242);
            Player.SetMoveInput(Vector2.Zero);
            Player.Face(Vector2.Right);
            await Frames(18);
            Check(Player.CurrentState == Combatant.State.Idle, "站立取自真实待机状态");
            Check(IsUpright(WeaponDirection()), $"站立单手竖戟，戟头朝上：{WeaponDirection()}");
            Check(Rig.ReferenceGripWeight < .05f, "站立解除副手握戟约束");
            Check(Rig.ToLocal(Rig.Joints[8].GlobalPosition).Y > Rig.ToLocal(Rig.Joints[6].GlobalPosition).Y + 6,
                "站立空闲手垂于肩下");
            CheckLegs("站立");
            if (HasRenderer) SaveDetail("lubu-reference-idle");

            float beforeWalk = Player.WalkDistance;
            Player.SetMoveInput(Vector2.Right);
            await Frames(18);
            Check(Player.CurrentState == Combatant.State.Move && Rig.WalkWeight > .9f, "移动取自真实迈步状态");
            Check(IsTrailing(WeaponDirection()), $"移动单手低持，戟头朝后下：{WeaponDirection()}");
            Check(Rig.ReferenceGripWeight < .05f, "移动仍保留单手持戟");
            Check(Player.WalkDistance > beforeWalk + 20, "移动姿态由实际路程推进");
            CheckLegs("移动");
            if (HasRenderer) SaveDetail("lubu-reference-move");
            await CaptureMovement("walk-right", 48);

            Player.SetMoveInput(Vector2.Left);
            await Frames(18);
            Check(IsTrailing(WeaponDirection()) && Player.FacingDirection.X < 0 && Rig.Scale.X < 0,
                "向左移动整体镜像，局部后拖戟方向不反转");
            await CaptureMovement("walk-left", 48);
            Player.SetMoveInput(Vector2.Zero);
            await Frames(18);
            Check(Player.CurrentState == Combatant.State.Idle, "停步恢复待机状态");
            Check(IsUpright(WeaponDirection()), "停步过渡回竖戟姿态");
            Check(Rig.ReferenceGripWeight < .05f, "停步仍是单手持戟");

            Player.Position = new Vector2(320, 242);
            Player.Face(Vector2.Right);
            await Frames(10);
            await CaptureSweep();
            GD.Print($"三姿态回归通过：{_checks} 项；真实渲染：{HasRenderer}；截图位于 Build/lubu-reference-*.png，连续帧位于 Build/reference-frames/。");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"三姿态回归失败：{exception}");
            GetTree().Paused = false;
            GetTree().Quit(1);
        }
    }

    /// <summary>按真实横扫时钟检查前摇末段，同时保留起势、挥出、收势的完整连续帧。</summary>
    private async Task CaptureSweep()
    {
        Check(Player.TryAction(CombatAction.Sweep), "可从竖戟待机正常进入横扫");
        bool capturedWindup = false;
        bool fixedLegs = true;
        float primaryMaxError = 0, secondaryMaxError = 0;
        string primaryWorst = "无偏差", secondaryWorst = "无偏差";
        int twoHandSamples = 0;
        int totalFrames = Mathf.CeilToInt(Player.Spec.Duration * Engine.PhysicsTicksPerSecond) + 20;
        for (int frame = 0; frame < totalFrames; frame++)
        {
            await Frames(1);
            fixedLegs &= HasFixedLegLengths() && HasUnscaledLegBones();
            // 武器握点必须能被固定长度手臂够到；记录全程最大偏差，避免只验收静止蓄势一帧。
            Vector2 primaryHand = Rig.ToLocal(Rig.Joints[5].GlobalPosition);
            float primaryError = primaryHand.DistanceTo(Rig.ReferencePrimaryGrip);
            if (primaryError > primaryMaxError)
            {
                primaryMaxError = primaryError;
                primaryWorst = $"第 {frame} 帧，动作时间 {Player.ActionTime:F3} 秒，手 {primaryHand}，目标 {Rig.ReferencePrimaryGrip}";
            }
            // 副手离柄与接柄过渡允许保留自由手轨迹；完全接柄后必须落在实际副握点上。
            if (Rig.ReferenceGripWeight >= .99f)
            {
                twoHandSamples++;
                Vector2 secondaryHand = Rig.ToLocal(Rig.Joints[8].GlobalPosition);
                float secondaryError = secondaryHand.DistanceTo(Rig.ReferenceSecondaryGrip);
                if (secondaryError > secondaryMaxError)
                {
                    secondaryMaxError = secondaryError;
                    secondaryWorst = $"第 {frame} 帧，动作时间 {Player.ActionTime:F3} 秒，权重 {Rig.ReferenceGripWeight:F3}，手 {secondaryHand}，目标 {Rig.ReferenceSecondaryGrip}";
                }
            }
            if (HasRenderer) SaveDetail($"reference-frames/sweep-{frame:D3}");
            if (!capturedWindup && Player.CurrentState == Combatant.State.Attack
                && Player.ActionTime >= Player.Spec.Prepare * .85f && Player.ActionTime < Player.Spec.Prepare)
            {
                capturedWindup = true;
                Check(Player.CurrentAction == CombatAction.Sweep && !Player.IsAttackActive,
                    $"蓄势取自真实横扫前摇末段：{Player.ActionTime:F3} 秒");
                Vector2 direction = WeaponDirection();
                Check(direction.X > .8f && Mathf.Abs(direction.Y) < .5f,
                    $"蓄势双手横戟，戟头朝前近水平：{direction}");
                Check(Rig.ReferenceGripWeight > .9f, "蓄势已接入双手握戟约束");
                float hands = Rig.ToLocal(Rig.Joints[5].GlobalPosition).DistanceTo(Rig.ToLocal(Rig.Joints[8].GlobalPosition));
                Check(hands > Mathf.Max(6, Player.Presentation.SupportGripDistance * .7f),
                    $"横戟时双手有独立握距：{hands:F2} 骨架单位");
                CheckLegs("蓄势");
                if (HasRenderer) SaveDetail("lubu-reference-windup");
            }
        }
        Check(capturedWindup, "已采样前摇 85% 至生效前的蓄势画面");
        Check(fixedLegs, "完整攻击过渡中大小腿保持固定骨长和缩放");
        // 先同时输出两只手的实测最差帧；任意一项失败时也能看到另一只手的诊断结果。
        GD.Print($"横扫主握点最大误差：{primaryMaxError:F3} 骨架单位；{primaryWorst}。");
        GD.Print($"横扫副握点最大误差：{secondaryMaxError:F3} 骨架单位；{secondaryWorst}；双手采样 {twoHandSamples} 帧。");
        Check(primaryMaxError < 1, $"完整攻击主握点误差小于 1：实际 {primaryMaxError:F3}；{primaryWorst}");
        Check(twoHandSamples > 0, "完整攻击包含副手完全接柄的有效采样");
        Check(secondaryMaxError < 1, $"完全接柄后副握点误差小于 1：实际 {secondaryMaxError:F3}；{secondaryWorst}");
        Check(Player.CurrentState == Combatant.State.Idle, "横扫结束回到待机状态");
        Check(Rig.ReferenceGripWeight < .05f, "收势后自然解除副手约束");
        Check(IsUpright(WeaponDirection()), "收势后恢复竖戟姿态");
    }

    /// <summary>保存角色跟随裁区中的连续迈步，不修改位移、速度或角色比例。</summary>
    private async Task CaptureMovement(string name, int count)
    {
        bool fixedLegs = true;
        for (int frame = 0; frame < count; frame++)
        {
            await Frames(1);
            fixedLegs &= HasFixedLegLengths() && HasUnscaledLegBones();
            if (HasRenderer) SaveDetail($"reference-frames/{name}-{frame:D3}");
        }
        Check(fixedLegs, $"{(name == "walk-right" ? "右行" : "左行")}连续迈步不拉伸腿骨");
    }

    /// <summary>兵器局部负 Y 为戟头；转换回骨架空间，排除左右镜像和整体显示比例。</summary>
    private Vector2 WeaponDirection() => (Rig.ToLocal(Rig.Joints[14].ToGlobal(Vector2.Up))
        - Rig.ToLocal(Rig.Joints[14].GlobalPosition)).Normalized();

    private static bool IsUpright(Vector2 direction) => direction.Y < -.8f && Mathf.Abs(direction.X) < .5f;
    private static bool IsTrailing(Vector2 direction) => direction.X < -.3f && direction.Y > .2f;

    private bool HasFixedLegLengths() => Mathf.IsEqualApprox(Rig.Joints[10].Position.Length(), Rig.ThighLength)
        && Mathf.IsEqualApprox(Rig.Joints[12].Position.Length(), Rig.ThighLength)
        && Mathf.IsEqualApprox(Rig.Joints[10].GetLength(), Rig.LowerLegLength)
        && Mathf.IsEqualApprox(Rig.Joints[12].GetLength(), Rig.LowerLegLength);

    private bool HasUnscaledLegBones() => Rig.Joints[9].Scale.IsEqualApprox(Vector2.One)
        && Rig.Joints[10].Scale.IsEqualApprox(Vector2.One) && Rig.Joints[11].Scale.IsEqualApprox(Vector2.One)
        && Rig.Joints[12].Scale.IsEqualApprox(Vector2.One);

    private void CheckLegs(string pose)
    {
        Check(HasFixedLegLengths(), $"{pose}保持固定大腿和小腿长度");
        Check(HasUnscaledLegBones(), $"{pose}不通过腿骨缩放伪造屈膝");
    }

    /// <summary>从刚完成的真实渲染帧裁切；窗口 1280×720 与设计视口 640×360 使用相同换算。</summary>
    private void SaveDetail(string name)
    {
        using var image = GetViewport().GetTexture().GetImage();
        Vector2 scale = new(image.GetWidth() / 640f, image.GetHeight() / 360f);
        Vector2 origin = (Player.GlobalPosition + CropOffset) * scale;
        Vector2 size = CropSize * scale;
        var crop = new Rect2I((int)origin.X, (int)origin.Y, (int)size.X, (int)size.Y);
        using var detail = image.GetRegion(crop);
        detail.Convert(Image.Format.Rgba8);
        detail.Resize(480, 420, Image.Interpolation.Nearest);
        Error result = detail.SavePng($"res://Build/{name}.png");
        if (result != Error.Ok) throw new InvalidOperationException($"保存姿态截图失败：{name}，{result}");
    }

    /// <summary>等待正常物理和表现更新；图形模式再等待读回帧完成，避免取到上一个姿态。</summary>
    private async Task Frames(int count)
    {
        for (int frame = 0; frame < count; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (HasRenderer) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        }
    }

    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
        GD.Print($"通过：{message}");
    }
}
