using Godot;
using System;
using System.Threading.Tasks;

/// <summary>通过真实移动和施法入口验收魏延长刀；覆盖最终握点、固定骨长、八向步态、暂停与受击。</summary>
public partial class WeiYanPoseTests : Node
{
    private Arena _arena;
    private Combatant Actor => _arena.Enemy;
    private CharacterRig Rig => Actor.Rig;
    private bool Rendered => DisplayServer.GetName() != "headless";
    private int _checks;
    private float _primaryError, _secondaryError;
    private bool _fixedLengths = true;

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
            foreach (var actor in new[] { _arena.Player, Actor })
            {
                actor.GetNode(NodeNames.Controller).ProcessMode = ProcessModeEnum.Disabled;
                actor.GetNode<GroundEffects>(NodeNames.GroundEffects).Hide();
            }
            _arena.Player.Position = new Vector2(90, 310);
            _arena.GetNode(NodeNames.Hud).ProcessMode = ProcessModeEnum.Disabled;
            _arena.Resolver.PassivesEnabled = false;
            Check(!_arena.IsSoloPractice && Actor.Visible, "魏延测试显式恢复对手");
            Check(Actor.Presentation.Animation.ReferencePoseSet && Rig.Joints.Count == 16, "魏延启用同一套16关节参考求解");
            Check(Actor.Presentation.Animation.StandingPose != _arena.Player.Presentation.Animation.StandingPose
                && Actor.Presentation.Animation.MovingPose != _arena.Player.Presentation.Animation.MovingPose, "两人使用独立姿态资源");
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://Build/weiyan-frames"));
            Actor.Position = new Vector2(330, 245);
            Actor.Face(Vector2.Right);
            await Frames(30);
            CheckGuard("站立");
            Check(Foot(0).FlipH && !Foot(1).FlipH, "旧图集远靴和近靴统一朝前，独立靴掌保留翻转");
            if (Rendered) await Capture("idle");
            for (int direction = 0; direction < 8; direction++)
                await CheckWalking(Vector2.Right.Rotated(direction * Mathf.Pi / 4), direction);
            Actor.SetMoveInput(Vector2.Zero);
            await Frames(35);
            CheckGuard("停步");
            float distance = Actor.WalkDistance, phase = Rig.WalkPhase;
            await Frames(12);
            Check(Mathf.IsEqualApprox(distance, Actor.WalkDistance) && Mathf.IsEqualApprox(phase, Rig.WalkPhase), "待机不继续迈步");

            foreach (var action in new[] { CombatAction.Basic, CombatAction.Dash, CombatAction.Sweep, CombatAction.Dodge })
                await CheckAction(action);
            Actor.Position = new Vector2(330, 245);
            Actor.PlayCounterSpin(68, .48f);
            for (int frame = 0; frame < 40; frame++)
            {
                await Frames(1);
                if (Rendered) await Capture($"counter-{frame:D3}");
            }
            Check(Actor.CounterRemaining <= 0, "狂骨回斩后正常结束");
            foreach (var direction in new[] { Vector2.Left, Vector2.Right })
            {
                Actor.ReceiveDamage(new DamageInfo(_arena.Player, 1, direction));
                await Frames(7);
                Check(Actor.CurrentState == Combatant.State.Hurt && Rig.Position.X * direction.X > 0, "受击位移遵循实际受力方向");
                await CheckPause();
                await Frames(35);
                CheckGuard("受击恢复");
            }
            Check(_fixedLengths, "全程大小腿骨长和骨骼缩放固定");
            GD.Print($"魏延全程主握误差 {_primaryError:F3}，副握误差 {_secondaryError:F3} 骨架单位。");
            Check(_primaryError < 1 && _secondaryError < 1, "站立、八向行走、攻击、闪避、反击及受击全程双手不脱柄");
            GD.Print($"魏延姿态回归通过：{_checks} 项；真实渲染：{Rendered}。");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"魏延姿态回归失败：{exception}");
            GetTree().Paused = false;
            GetTree().Quit(1);
        }
    }

    private async Task CheckWalking(Vector2 direction, int index)
    {
        Actor.Position = new Vector2(330, 205);
        Actor.SetMoveInput(direction);
        await Frames(12);
        float start = Actor.WalkDistance;
        float[] support = { 0, 0 }, swing = { 0, 0 }, contactError = { 0, 0 };
        int[] samples = { 0, 0 };
        for (int frame = 0; frame < 44; frame++)
        {
            await Frames(1);
            for (int leg = 0; leg < 2; leg++)
            {
                float phase = Mathf.PosMod(Rig.WalkPhase + leg * .5f, 1);
                float bend = Mathf.Abs(Mathf.RadToDeg(Rig.Joints[leg == 0 ? 10 : 12].Rotation));
                if (phase is >= .18f and <= .36f)
                {
                    support[leg] += bend;
                    samples[leg]++;
                    var foot = Foot(leg);
                    Vector2 size = foot.Texture.GetSize();
                    float heel = Rig.Skeleton.ToLocal(foot.ToGlobal(new Vector2(0, size.Y))).Y;
                    float toe = Rig.Skeleton.ToLocal(foot.ToGlobal(size)).Y;
                    contactError[leg] = Mathf.Max(contactError[leg], Mathf.Abs(Mathf.Max(heel, toe)));
                }
                if (phase > Rig.ReferenceSupportFraction + .08f && phase < .95f)
                    swing[leg] = Mathf.Max(swing[leg], bend);
            }
            if (Rendered && index is 0 or 4) await Capture($"walk-{index}-{frame:D3}");
        }
        Check(Actor.WalkDistance - start >= Actor.Presentation.Animation.WalkCycleDistance, $"方向{index}完成整周期实际移动");
        for (int leg = 0; leg < 2; leg++)
        {
            float mean = support[leg] / Mathf.Max(1, samples[leg]);
            GD.Print($"魏延方向{index} 腿{leg}：承重膝 {mean:F1}°，摆动膝 {swing[leg]:F1}°，接地误差 {contactError[leg]:F2}。");
            Check(samples[leg] > 1 && mean is > 8 and < 35 && swing[leg] > mean + 10,
                $"方向{index} 腿{leg}承重撑起、摆动屈膝，避免持续蹲行");
            if (Mathf.Abs(direction.Y) < .01f) Check(contactError[leg] < 1.25f, $"方向{index} 腿{leg}承重脚底接地");
        }
        Vector2 weapon = WeaponDirection();
        Check(weapon.Y < -.9f && weapon.X > .05f && Rig.ReferenceGripWeight > .99f, $"方向{index}双手收刀行走");
        if (Mathf.Abs(direction.X) > .1f) Check(Rig.Scale.X * direction.X > 0, $"方向{index}人物整体镜像符合朝向");
        if (index == 0) await CheckPause();
    }

    private async Task CheckAction(CombatAction action)
    {
        Actor.Position = new Vector2(330, 245);
        Actor.Face(Vector2.Right);
        // 从移动中的实姿态起势，验证起停切换而非只测静帧。
        Actor.SetMoveInput(Vector2.Right);
        await Frames(12);
        Check(Actor.TryAction(action), $"移动中启动{action}");
        Actor.SetMoveInput(Vector2.Zero);
        float min = float.MaxValue, max = float.MinValue;
        int active = 0;
        bool paused = false;
        int count = Mathf.CeilToInt(Actor.Spec.Duration * Engine.PhysicsTicksPerSecond) + 28;
        for (int frame = 0; frame < count; frame++)
        {
            await Frames(1);
            if (Actor.IsAttackActive)
            {
                float angle = Rig.Joints[14].Rotation;
                min = Mathf.Min(min, angle); max = Mathf.Max(max, angle); active++;
                if (!paused) { await CheckPause(); paused = true; }
            }
            if (Rendered) await Capture($"{action}-{frame:D3}");
        }
        if (action != CombatAction.Dodge)
            Check(active >= 3 && Mathf.RadToDeg(max - min) > (action == CombatAction.Dash ? 35 : 65), $"{action}生效期间有明确挥斩弧线");
        Check(Actor.CurrentState == Combatant.State.Idle, $"{action}结束正常收势");
        CheckGuard($"{action}恢复");
    }

    private void CheckGuard(string label)
    {
        Vector2 direction = WeaponDirection();
        Check(direction.X is > .4f and < .75f && direction.Y < -.65f && Rig.ReferenceGripWeight > .99f,
            $"{label}双手斜持长刀");
    }

    private async Task CheckPause()
    {
        float time = Actor.VisualTime;
        var weapon = Rig.Joints[14].GlobalTransform;
        var foot = Foot(0).GlobalTransform;
        GetTree().Paused = true;
        await Frames(5);
        Check(Mathf.IsEqualApprox(time, Actor.VisualTime) && weapon.IsEqualApprox(Rig.Joints[14].GlobalTransform)
            && foot.IsEqualApprox(Foot(0).GlobalTransform), "暂停冻结动作时钟、武器及独立靴掌");
        GetTree().Paused = false;
    }

    private Vector2 WeaponDirection() => Vector2.Up.Rotated(Rig.Joints[14].Rotation);
    private Sprite2D Foot(int leg)
    {
        foreach (var part in Rig.DrawOrderedParts)
            if (part.Name == (leg == 0 ? "远靴掌图层" : "近靴掌图层")) return part;
        throw new InvalidOperationException("缺少独立靴掌");
    }

    private async Task Frames(int count)
    {
        for (int frame = 0; frame < count; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _primaryError = Mathf.Max(_primaryError, Rig.Skeleton.ToLocal(Rig.Joints[5].GlobalPosition).DistanceTo(Rig.ReferencePrimaryGrip));
            _secondaryError = Mathf.Max(_secondaryError, Rig.Skeleton.ToLocal(Rig.Joints[8].GlobalPosition).DistanceTo(Rig.ReferenceSecondaryGrip));
            _fixedLengths &= Mathf.IsEqualApprox(Rig.Joints[10].Position.Length(), Rig.ThighLength)
                && Mathf.IsEqualApprox(Rig.Joints[12].Position.Length(), Rig.ThighLength);
            foreach (int joint in new[] { 9, 10, 11, 12 }) _fixedLengths &= Rig.Joints[joint].Scale.IsEqualApprox(Vector2.One);
        }
    }

    /// <summary>读取实际渲染帧，跟随裁区保留刀尖和脚底；仅将预览放大，不改变实机比例。</summary>
    private async Task Capture(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        float scale = image.GetWidth() / 640f;
        using var detail = image.GetRegion(new Rect2I((int)((Actor.Position.X - 80) * scale),
            (int)((Actor.Position.Y - 110) * scale), (int)(160 * scale), (int)(135 * scale)));
        detail.Resize(480, 405, Image.Interpolation.Nearest);
        detail.SavePng($"res://Build/weiyan-frames/{name}.png");
    }

    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
        GD.Print($"通过：{message}");
    }
}
