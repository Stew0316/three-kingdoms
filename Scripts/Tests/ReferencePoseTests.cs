using Godot;
using System;
using System.Threading.Tasks;

/// <summary>显式 --pose-test 验收长兵器姿态与挥击弧线：真实移动和出招驱动，不改写动作时钟。</summary>
public partial class ReferencePoseTests : Node
{
    private Arena _arena;
    private int _checks;
    private Combatant Player => _arena.Player;
    private CharacterRig Rig => Player.Rig;
    private bool HasRenderer => DisplayServer.GetName() != "headless";
    // 统一裁区包含低持、后拖和过顶挥戟，保持各姿态的显示尺度一致。
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
                && _arena.Enemy.ProcessMode == ProcessModeEnum.Disabled, "长兵器姿态验收保持魏延关闭");
            Check(Player.Presentation.ReferencePoseSet, "吕布已启用独立关键姿态配置");
            Check(Rig.Joints.Count == 16, "各类姿态复用同一套 16 业务关节");
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
            Check(IsLowGuard(WeaponDirection()), $"站立双手低持长戟，戟头朝前上：{WeaponDirection()}");
            Check(Rig.ReferenceGripWeight >= .99f, "站立保持双手握戟约束");
            Check(HandPosition(5).Y > Rig.ToLocal(Rig.Joints[2].GlobalPosition).Y + 6
                && HandPosition(8).Y > Rig.ToLocal(Rig.Joints[2].GlobalPosition).Y + 6,
                "站立双手均位于头部下方，不摆出过肩投掷姿势");
            CheckLegs("站立");
            if (HasRenderer) SaveDetail("lubu-reference-idle");

            float beforeWalk = Player.WalkDistance;
            Player.SetMoveInput(Vector2.Right);
            await Frames(18);
            Check(Player.CurrentState == Combatant.State.Move && Rig.WalkWeight > .9f, "移动取自真实迈步状态");
            Check(IsTrailing(WeaponDirection()), $"移动单手近水平拖戟，戟头朝后下：{WeaponDirection()}");
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
            Check(IsLowGuard(WeaponDirection()), "停步过渡回低位持戟姿态");
            Check(Rig.ReferenceGripWeight >= .99f, "停步重新接入双手持戟");

            Player.Position = new Vector2(320, 242);
            Player.Face(Vector2.Right);
            await Frames(10);
            await CheckAttack(CombatAction.Sweep, true);
            await CheckAttack(CombatAction.Basic, false);
            await CheckAttack(CombatAction.Dash, false);
            await CheckHurtDirections();
            GD.Print($"长兵器姿态回归通过：{_checks} 项；真实渲染：{HasRenderer}；截图位于 Build/lubu-reference-*.png，连续帧位于 Build/reference-frames/。");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"长兵器姿态回归失败：{exception}");
            GetTree().Paused = false;
            GetTree().Quit(1);
        }
    }

    /// <summary>按真实动作时钟检查挥击角度及完整握点误差；横扫同时保留起势、挥出、收势连续帧。</summary>
    private async Task CheckAttack(CombatAction action, bool capture)
    {
        string name = action switch { CombatAction.Sweep => "横扫", CombatAction.Basic => "普攻", _ => "突进" };
        Check(Player.TryAction(action), $"可从低位持戟待机正常进入{name}");
        bool capturedWindup = false;
        bool fixedLegs = true;
        bool sawRear = false, sawOverheadAfterRear = false, sawFrontDownAfterOverhead = false;
        bool activeStateValid = true, pausedChecked = false;
        float primaryMaxError = 0, secondaryMaxError = 0;
        float activeGripMinimum = 1, unwrappedAngle = 0, minimumAngle = 0, maximumAngle = 0;
        string primaryWorst = "无偏差", secondaryWorst = "无偏差";
        int twoHandSamples = 0, activeSamples = 0;
        Vector2 previousActiveDirection = Vector2.Zero;
        int totalFrames = Mathf.CeilToInt(Player.Spec.Duration * Engine.PhysicsTicksPerSecond) + 20;
        for (int frame = 0; frame < totalFrames; frame++)
        {
            await Frames(1);
            fixedLegs &= HasFixedLegLengths() && HasUnscaledLegBones();
            // 武器握点必须能被固定长度手臂够到；记录全程最大偏差，避免只验收静止蓄势一帧。
            Vector2 primaryHand = HandPosition(5);
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
                Vector2 secondaryHand = HandPosition(8);
                float secondaryError = secondaryHand.DistanceTo(Rig.ReferenceSecondaryGrip);
                if (secondaryError > secondaryMaxError)
                {
                    secondaryMaxError = secondaryError;
                    secondaryWorst = $"第 {frame} 帧，动作时间 {Player.ActionTime:F3} 秒，权重 {Rig.ReferenceGripWeight:F3}，手 {secondaryHand}，目标 {Rig.ReferenceSecondaryGrip}";
                }
            }
            if (Player.IsAttackActive)
            {
                Vector2 direction = WeaponDirection();
                activeStateValid &= Player.CurrentState == Combatant.State.Attack && Player.CurrentAction == action;
                activeGripMinimum = Mathf.Min(activeGripMinimum, Rig.ReferenceGripWeight);
                // 连续解包跨越正负 180 度的方向，避免将一段完整挥击误读为短角或直线前送。
                if (activeSamples > 0) unwrappedAngle += previousActiveDirection.AngleTo(direction);
                minimumAngle = Mathf.Min(minimumAngle, unwrappedAngle);
                maximumAngle = Mathf.Max(maximumAngle, unwrappedAngle);
                previousActiveDirection = direction;
                activeSamples++;
                if (direction.X < -.5f) sawRear = true;
                if (sawRear && direction.Y < -.6f) sawOverheadAfterRear = true;
                if (sawOverheadAfterRear && direction.X > .35f && direction.Y > .08f)
                    sawFrontDownAfterOverhead = true;
                if (capture && !pausedChecked)
                {
                    pausedChecked = true;
                    await CheckPausedAttack();
                }
            }
            if (capture && HasRenderer) SaveDetail($"reference-frames/sweep-{frame:D3}");
            if (capture && !capturedWindup && Player.CurrentState == Combatant.State.Attack
                && Player.ActionTime >= Player.Spec.Prepare * .85f && Player.ActionTime < Player.Spec.Prepare)
            {
                capturedWindup = true;
                Check(Player.CurrentAction == CombatAction.Sweep && !Player.IsAttackActive,
                    $"蓄势取自真实横扫前摇末段：{Player.ActionTime:F3} 秒");
                Vector2 direction = WeaponDirection();
                Check(direction.X < -.85f && Mathf.Abs(direction.Y) < .4f,
                    $"蓄势双手收戟至身后，戟头朝后近水平：{direction}");
                Check(Rig.ReferenceGripWeight >= .99f, "蓄势已完全接入双手握戟约束");
                float hands = HandPosition(5).DistanceTo(HandPosition(8));
                Check(hands > Mathf.Max(6, Player.Presentation.SupportGripDistance * .7f),
                    $"蓄势时双手有独立握距：{hands:F2} 骨架单位");
                CheckLegs("蓄势");
                if (HasRenderer) SaveDetail("lubu-reference-windup");
            }
        }
        if (capture) Check(capturedWindup, "已采样前摇 85% 至生效前的蓄势画面");
        Check(fixedLegs, $"{name}完整过渡中大小腿保持固定骨长和缩放");
        // 先同时输出两只手的实测最差帧；任意一项失败时也能看到另一只手的诊断结果。
        float activeDegrees = Mathf.RadToDeg(maximumAngle - minimumAngle);
        GD.Print($"{name}主握点最大误差：{primaryMaxError:F3} 骨架单位；{primaryWorst}。");
        GD.Print($"{name}副握点最大误差：{secondaryMaxError:F3} 骨架单位；{secondaryWorst}；双手采样 {twoHandSamples} 帧。");
        GD.Print($"{name}生效段实测挥击角范围：{activeDegrees:F2} 度；生效采样 {activeSamples} 帧；最小副握权重 {activeGripMinimum:F3}。");
        Check(primaryMaxError < 1, $"{name}全程主握点误差小于 1：实际 {primaryMaxError:F3}；{primaryWorst}");
        Check(twoHandSamples > 0, $"{name}包含副手完全接柄的有效采样");
        Check(secondaryMaxError < 1, $"{name}完全接柄后副握点误差小于 1：实际 {secondaryMaxError:F3}；{secondaryWorst}");
        Check(activeSamples >= 3 && activeStateValid, $"{name}生效采样来自真实攻击状态与对应动作");
        Check(activeGripMinimum >= .99f, $"{name}生效期间双手持续握柄，最小权重 {activeGripMinimum:F3}");
        float requiredDegrees = action == CombatAction.Sweep ? 100 : 50;
        Check(activeDegrees >= requiredDegrees, $"{name}生效段挥击角范围至少 {requiredDegrees} 度：实际 {activeDegrees:F2} 度");
        if (action == CombatAction.Sweep)
        {
            Check(sawRear && sawOverheadAfterRear && sawFrontDownAfterOverhead,
                "横扫生效段戟头依次经过身后、头顶和前下方，形成劈扫弧线");
            Check(pausedChecked, "已在横扫生效期间检查暂停冻结");
        }
        Check(Player.CurrentState == Combatant.State.Idle, $"{name}结束回到待机状态");
        Check(Rig.ReferenceGripWeight >= .99f, $"{name}收势后保持双手低持约束");
        Check(IsLowGuard(WeaponDirection()), $"{name}收势后恢复低位持戟姿态");
    }

    /// <summary>暂停完整场景，验证动作时钟、长戟方向与双手约束同时停止，不只检查一项时钟。</summary>
    private async Task CheckPausedAttack()
    {
        float time = Player.ActionTime;
        Vector2 direction = WeaponDirection();
        Vector2 primary = Rig.ReferencePrimaryGrip, secondary = Rig.ReferenceSecondaryGrip;
        Vector2 primaryHand = HandPosition(5), secondaryHand = HandPosition(8);
        float weight = Rig.ReferenceGripWeight;
        GetTree().Paused = true;
        await Frames(5);
        Check(Mathf.IsEqualApprox(Player.ActionTime, time), "暂停冻结真实攻击时钟");
        Check(WeaponDirection().DistanceTo(direction) < .0001f, "暂停后武器方向保持不变");
        Check(Rig.ReferencePrimaryGrip.DistanceTo(primary) < .0001f
            && Rig.ReferenceSecondaryGrip.DistanceTo(secondary) < .0001f
            && Mathf.IsEqualApprox(Rig.ReferenceGripWeight, weight)
            && HandPosition(5).DistanceTo(primaryHand) < .0001f
            && HandPosition(8).DistanceTo(secondaryHand) < .0001f,
            "暂停后双握点、约束权重和实际双手位置保持不变");
        GetTree().Paused = false;
    }

    /// <summary>通过正常伤害入口分别施加左右击退，检查躯干和整体偏移随受力方向改变。</summary>
    private async Task CheckHurtDirections()
    {
        Player.SetMoveInput(Vector2.Zero);
        Player.Position = new Vector2(320, 242);
        Player.Face(Vector2.Right);
        await Frames(18);
        float leftChest = 0, rightChest = 0;
        foreach (Vector2 direction in new[] { Vector2.Left, Vector2.Right })
        {
            Player.ReceiveDamage(new DamageInfo(_arena.Enemy, 1, direction));
            await Frames(Mathf.CeilToInt((Player.Config.HitStopDuration + .07f) * Engine.PhysicsTicksPerSecond));
            string name = direction.X < 0 ? "左" : "右";
            Check(Player.CurrentState == Combatant.State.Hurt, $"向{name}受击取自真实伤害与硬直状态");
            Check(Rig.Position.X * direction.X > .1f, $"向{name}受击的整体偏移符合击退方向");
            if (direction.X < 0) leftChest = Rig.Joints[1].Rotation;
            else rightChest = Rig.Joints[1].Rotation;
            await Frames(Mathf.CeilToInt((Player.Config.HurtDuration + .3f) * Engine.PhysicsTicksPerSecond));
            Check(Player.CurrentState == Combatant.State.Idle && IsLowGuard(WeaponDirection()),
                $"向{name}受击结束恢复低位持戟待机");
        }
        Check(leftChest < -.02f && rightChest > .02f, $"左右受击躯干朝受力方向倾斜：左 {leftChest:F3}，右 {rightChest:F3}");
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

    private Vector2 HandPosition(int joint) => Rig.ToLocal(Rig.Joints[joint].GlobalPosition);
    private static bool IsLowGuard(Vector2 direction) => direction.X > .7f && direction.Y < -.1f && direction.Y > -.7f;
    private static bool IsTrailing(Vector2 direction) => direction.X < -.8f && direction.Y > .08f && direction.Y < .5f;

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
