using Godot;
using System;
using System.Threading.Tasks;

// 显式传 -- --combat-test 才运行；使用真实物理帧和场景，不修改正常游戏输入。
/// <summary>跨场景重载执行真实物理帧回归；仅由显式测试参数启动，不参与正常游戏流程。</summary>
public partial class CombatSmokeTests : Node
{
    // 已通过的断言数量，用于最终报告和失败定位。
    private int _checks;
    // 当前测试场景；每次重载后必须重新取得引用。
    private Arena _arena;
    // 当前场景的玩家，避免缓存重载前已经失效的角色节点。
    private Combatant Player => _arena.Player;
    // 当前场景的敌人，随 _arena 更新而切换。
    private Combatant Enemy => _arena.Enemy;

    /// <summary>使测试器在暂停期间仍可等待物理帧，再延迟启动测试流程。</summary>
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        CallDeferred(MethodName.Run);
    }

    /// <summary>顺序执行输入、骨骼、刀光和战斗边界检查；异常时打印位置并以非零退出码结束。</summary>
    private async void Run()
    {
        try
        {
            // 直接执行 HUD 的输入回调，让异常传播到测试；只调用 ReloadCurrentScene
            // 会绕过“重载后仍访问旧 HUD”的生命周期错误。
            await Reset();
            await RestartThroughHud("during battle");
            GetTree().Paused = true;
            await RestartThroughHud("while paused");
            Enemy.ReceiveDamage(new DamageInfo(Player, 100, Vector2.Right));
            await RestartThroughHud("after victory");
            await Reset();
            Check(Player.Health.CurrentHealth == 120 && Enemy.Health.CurrentHealth == 100, "initial health");
            Check(Player.Config != null && Enemy.Config != null
                && Player.Config.Basic.Damage == 12 && Enemy.Config.Basic.Damage == 10,
                "角色场景已加载各自的可编辑战斗配置");
            Check(Player.GetNode<Sprite2D>(NodeNames.Visual).Texture != null && Enemy.GetNode<Sprite2D>(NodeNames.Visual).Texture != null, "character textures loaded");
            var rig = Player.GetNode<CharacterRig>(NodeNames.Rig);
            Check(rig.Skeleton.GetBoneCount() == CharacterBoneNames.Count && rig.Skin.GetBoneCount() == CharacterBoneNames.Count, "二维骨骼和蒙皮均已绑定八根骨骼");
            Check(Mathf.IsEqualApprox(rig.ArtScale, .05f), "角色缩小至原尺寸约百分之六十八");
            bool normalized = true;
            for (int v = 0; v < rig.Skin.Polygon.Length; v++)
            {
                float total = 0;
                for (int b = 0; b < CharacterBoneNames.Count; b++) total += rig.Skin.GetBoneWeights(b)[v];
                normalized &= Mathf.IsEqualApprox(total, 1);
            }
            Check(normalized, "蒙皮所有顶点权重归一化");
            float beforePose = rig.Skeleton.GetBone(1).Rotation;
            Player.SetMoveInput(Vector2.Right);
            await Frames(10);
            Check(!Mathf.IsEqualApprox(beforePose, rig.Skeleton.GetBone(1).Rotation), "行走动画确实改变骨骼姿态");
            Player.SetMoveInput(Vector2.Zero);
            await Frames(2);
            Player.TryAction(CombatAction.Sweep);
            await Frames(23);
            var trail = Player.GetNode<WeaponTrail>(NodeNames.WeaponTrail);
            Check(trail.IsEmitting, "生效窗口出现刀光");
            GetTree().Paused = true;
            float pausedTime = Player.ActionTime;
            await Frames(4);
            Check(Mathf.IsEqualApprox(Player.ActionTime, pausedTime), "暂停冻结刀光与骨骼的动作时钟");
            GetTree().Paused = false;
            Player.ReceiveDamage(new DamageInfo(Enemy, 1, Vector2.Left));
            await Frames(3);
            Check(!trail.IsEmitting, "受击打断立即清理刀光");
            await Reset();
            PlaceDuel();
            Player.GetNode<WeaponTrail>(NodeNames.WeaponTrail).EffectsEnabled = false;
            await Frames(3);
            Player.TryAction(CombatAction.Basic);
            await Frames(35);
            Check(Enemy.Health.CurrentHealth == 88, "关闭装饰刀光不改变伤害结果");
            await Reset();
            PlaceDuel();
            await Frames(3);
            Check(Player.TryAction(CombatAction.Basic), "basic accepted");
            Check(!Player.TryAction(CombatAction.Sweep), "repeated input rejected during attack");
            Player.Face(Vector2.Left);
            Check(Player.FacingDirection == Vector2.Right, "cast direction locked");
            await Frames(6);
            Check(Enemy.Health.CurrentHealth == 100, "windup deals no damage");
            await Frames(32);
            Check(Enemy.Health.CurrentHealth == 88, "one swing deals exactly 12 damage");

            await Reset();
            PlaceDuel();
            var extraHurtbox = new Hurtbox { CollisionLayer = PhysicsLayers.EnemyHurtbox, CollisionMask = PhysicsLayers.None, Monitoring = false };
            extraHurtbox.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 14 } });
            Enemy.AddChild(extraHurtbox);
            await Frames(4);
            Player.TryAction(CombatAction.Sweep);
            await Frames(25);
            Enemy.Position = new Vector2(450, 240);
            await Frames(2);
            Enemy.Position = new Vector2(319, 240);
            await Frames(38);
            Check(Enemy.Health.CurrentHealth == 76, "multiple hurtboxes and reentry still hit once per cast");

            await Reset();
            PlaceDuel();
            Player.GetNode(NodeNames.Controller).ProcessMode = ProcessModeEnum.Inherit;
            await Frames(3);
            Input.ActionPress(InputActions.Attack);
            await Frames(2);
            Input.ActionRelease(InputActions.Attack);
            await Frames(35);
            Check(Enemy.Health.CurrentHealth == 88, "player input reaches shared combat action");

            await Reset();
            PlaceDuel();
            Player.Face(Vector2.Left);
            await Frames(3);
            Player.TryAction(CombatAction.Basic);
            await Frames(38);
            Check(Enemy.Health.CurrentHealth == 100, "attack away from target misses");

            await Reset();
            PlaceDuel();
            await Frames(3);
            Player.TryAction(CombatAction.Sweep);
            Player.ReceiveDamage(new DamageInfo(Enemy, 1, Vector2.Left));
            await Frames(65);
            Check(Enemy.Health.CurrentHealth == 100 && Player.CanAct, "hurt cancels old attack and unlocks state");
            Check(Player.Cooldown(CombatAction.Sweep) > 0 && !Player.TryAction(CombatAction.Sweep), "cancel does not refund cooldown");

            await Reset();
            PlaceDuel();
            Enemy.Position = Player.Position + Vector2.Right * 65;
            await Frames(3);
            Player.TryAction(CombatAction.Basic);
            await Frames(35);
            Check(Enemy.Health.CurrentHealth == 100, "basic cannot hit at sweep range");
            Player.TryAction(CombatAction.Sweep);
            await Frames(65);
            Check(Enemy.Health.CurrentHealth == 76, "sweep reaches farther and hits once");

            await Reset();
            Player.Position = new Vector2(590, 220);
            Enemy.Position = new Vector2(200, 220);
            Player.Face(Vector2.Right);
            Player.TryAction(CombatAction.Dash);
            await Frames(55);
            Check(Player.Position.X <= 604.2f && Player.Position.X >= 590, "dash stops at wall");
            Player.ReceiveDamage(new DamageInfo(Enemy, 1, Vector2.Right));
            await Frames(22);
            Check(Player.Position.X <= 604.2f, "knockback stops at wall");

            await Reset();
            PlaceDuel();
            Player.TryAction(CombatAction.Dodge);
            Player.ReceiveDamage(new DamageInfo(Enemy, 10, Vector2.Left));
            Check(Player.Health.CurrentHealth == 110 && Player.CurrentState == Combatant.State.Hurt, "dodge has no invulnerability");

            await Reset();
            PlaceDuel();
            await Frames(3);
            Player.TryAction(CombatAction.Basic);
            await Frames(4);
            float time = Player.ActionTime;
            GetTree().Paused = true;
            await Frames(20);
            Check(Mathf.IsEqualApprox(Player.ActionTime, time) && Enemy.Health.CurrentHealth == 100, "pause freezes attack timeline");
            GetTree().Paused = false;
            await Frames(35);
            Check(Enemy.Health.CurrentHealth == 88, "resume completes only one hit");

            await Reset();
            PlaceDuel();
            Player.TryAction(CombatAction.Sweep);
            Player.ReceiveDamage(new DamageInfo(Enemy, 999, Vector2.Left));
            Vector2 deathPosition = Player.Position;
            await Frames(60);
            Check(Player.IsDead && _arena.Finished && !_arena.Result.Contains("成功"), "defeat panel state");
            Check(Player.Position == deathPosition && Enemy.Health.CurrentHealth == 100 && !Player.TryAction(CombatAction.Dash), "death cancels motion and old attacks");

            // 切换场景时旧前摇不能在新一局结算；多次重载检测残留角色。
            for (int i = 0; i < 10; i++)
            {
                await Reset();
                PlaceDuel();
                Player.TryAction(CombatAction.Sweep);
                await Reset();
                await Frames(45);
                Check(Player.Health.CurrentHealth == 120 && Enemy.Health.CurrentHealth == 100, $"reload {i + 1} has fresh health and no delayed hit");
                if (i % 2 == 0) Enemy.ReceiveDamage(new DamageInfo(Player, 100, Vector2.Right));
                else Player.ReceiveDamage(new DamageInfo(Enemy, 120, Vector2.Left));
                await Frames(2);
                Check(_arena.Finished && (i % 2 == 0 ? _arena.Result == "挑战成功" : Player.IsDead), $"round {i + 1} ends correctly");
                Check(_arena.GetNode(NodeNames.Fighters).GetChildCount() == 2, "only two fighters after restart");
            }

            await Reset();
            Enemy.GetNode(NodeNames.Controller).ProcessMode = ProcessModeEnum.Inherit;
            await Frames(720);
            Check(Player.Health.CurrentHealth < 120, "AI chases and damages through combat rules");
            await Frames(1800);
            Check(_arena.Finished && Player.IsDead, "AI can complete a defeat round");
            GD.Print($"COMBAT_TEST_PASS: {_checks} assertions; 10 restart/result cycles plus combat and AI checks.");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PushError($"COMBAT_TEST_FAIL after {_checks} assertions: {ex}");
            GetTree().Paused = false;
            GetTree().Quit(1);
        }
    }

    /// <summary>重载并等待节点就绪，关闭双方控制器，隔离人为设置的测试条件。</summary>
    private async Task Reset()
    {
        GetTree().Paused = false;
        Error reloadError = GetTree().ReloadCurrentScene();
        if (reloadError != Error.Ok)
            throw new InvalidOperationException($"场景重载请求失败：{reloadError}");
        _arena = await WaitForArenaReady();
        Player.GetNode(NodeNames.Controller).ProcessMode = ProcessModeEnum.Disabled;
        Enemy.GetNode(NodeNames.Controller).ProcessMode = ProcessModeEnum.Disabled;
        Player.SetMoveInput(Vector2.Zero);
        Enemy.SetMoveInput(Vector2.Zero);
    }

    /// <summary>通过真实 HUD 回调触发重开；context 标注对战、暂停或胜利等触发场景。</summary>
    private async Task RestartThroughHud(string context)
    {
        ulong previousScene = _arena.GetInstanceId();
        var hud = _arena.GetNode<BattleHud>(NodeNames.Hud);
        using var input = new InputEventAction { Action = InputActions.Restart, Pressed = true };
        hud._UnhandledInput(input);
        _arena = await WaitForArenaReady();
        Check(_arena.GetInstanceId() != previousScene && !GetTree().Paused
            && Player.Health.CurrentHealth == 120 && Enemy.Health.CurrentHealth == 100,
            $"HUD restart {context} completes without accessing detached nodes");
        Player.GetNode(NodeNames.Controller).ProcessMode = ProcessModeEnum.Disabled;
        Enemy.GetNode(NodeNames.Controller).ProcessMode = ProcessModeEnum.Disabled;
    }

    /// <summary>等待重载后的 Arena 成为 CurrentScene 且角色引用就绪，避免把场景切换的空窗期当成业务错误。</summary>
    private async Task<Arena> WaitForArenaReady()
    {
        const int maxFrames = 30;
        for (int i = 0; i < maxFrames; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (GetTree().CurrentScene is Arena arena && arena.Player != null && arena.Enemy != null)
                return arena;
        }
        throw new InvalidOperationException($"等待 {maxFrames} 帧后 Arena 仍未完成重载。");
    }

    /// <summary>摆放面对面的固定近战目标，使普攻范围与方向检查可重复。</summary>
    private void PlaceDuel()
    {
        Player.Position = new Vector2(280, 240);
        Enemy.Position = new Vector2(319, 240);
        Player.Face(Vector2.Right);
        Enemy.Face(Vector2.Left);
    }

    /// <summary>等待 count 个真实物理帧；用于观察引擎碰撞更新，不使用墙钟延时替代。</summary>
    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    /// <summary>condition 为验证条件，description 说明预期行为；失败立即中断本轮测试。</summary>
    private void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        _checks++;
        GD.Print($"PASS {_checks}: {description}");
    }
}
