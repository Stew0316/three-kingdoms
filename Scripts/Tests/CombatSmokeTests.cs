using Godot;
using System;
using System.Threading.Tasks;

// 显式传 -- --combat-test 才运行；使用真实物理帧和场景，不修改正常游戏输入。
public partial class CombatSmokeTests : Node
{
    private int _checks;
    private Arena _arena;
    private Combatant Player => _arena.Player;
    private Combatant Enemy => _arena.Enemy;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        CallDeferred(MethodName.Run);
    }

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
            Check(Player.GetNode<Sprite2D>("Visual").Texture != null && Enemy.GetNode<Sprite2D>("Visual").Texture != null, "character textures loaded");
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
            var extraHurtbox = new Hurtbox { CollisionLayer = 8, CollisionMask = 0, Monitoring = false };
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
            Player.GetNode("Controller").ProcessMode = ProcessModeEnum.Inherit;
            await Frames(3);
            Input.ActionPress("attack");
            await Frames(2);
            Input.ActionRelease("attack");
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
                Check(_arena.GetNode("Fighters").GetChildCount() == 2, "only two fighters after restart");
            }

            await Reset();
            Enemy.GetNode("Controller").ProcessMode = ProcessModeEnum.Inherit;
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

    private async Task Reset()
    {
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
        await Frames(3);
        _arena = (Arena)GetTree().CurrentScene;
        Player.GetNode("Controller").ProcessMode = ProcessModeEnum.Disabled;
        Enemy.GetNode("Controller").ProcessMode = ProcessModeEnum.Disabled;
        Player.SetMoveInput(Vector2.Zero);
        Enemy.SetMoveInput(Vector2.Zero);
    }

    private async Task RestartThroughHud(string context)
    {
        ulong previousScene = _arena.GetInstanceId();
        var hud = _arena.GetNode<BattleHud>("HUD");
        using var input = new InputEventAction { Action = "restart_battle", Pressed = true };
        hud._UnhandledInput(input);
        await Frames(3);
        _arena = (Arena)GetTree().CurrentScene;
        Check(_arena.GetInstanceId() != previousScene && !GetTree().Paused
            && Player.Health.CurrentHealth == 120 && Enemy.Health.CurrentHealth == 100,
            $"HUD restart {context} completes without accessing detached nodes");
        Player.GetNode("Controller").ProcessMode = ProcessModeEnum.Disabled;
        Enemy.GetNode("Controller").ProcessMode = ProcessModeEnum.Disabled;
    }

    private void PlaceDuel()
    {
        Player.Position = new Vector2(280, 240);
        Enemy.Position = new Vector2(319, 240);
        Player.Face(Vector2.Right);
        Enemy.Face(Vector2.Left);
    }

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        _checks++;
        GD.Print($"PASS {_checks}: {description}");
    }
}
