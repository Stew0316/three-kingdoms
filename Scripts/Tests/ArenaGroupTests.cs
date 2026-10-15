using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>真实四人演武场验收：人物穿越、墙体边界、独立生命配置、群体结算及重开。</summary>
public partial class ArenaGroupTests : Node
{
    private Arena _arena;
    private int _checks;
    public override void _Ready() { ProcessMode = ProcessModeEnum.Always; CallDeferred(MethodName.Run); }

    private async void Run()
    {
        try
        {
            _arena = (Arena)GetTree().CurrentScene;
            DisableControllers(); await Frames(4);
            Check(_arena.IsGroupPractice && _arena.Fighters.Count == 4 && _arena.Enemies.Count() == 3, "正常演武场生成一名玩家与三名AI");
            Check(_arena.Fighters.Select(actor => actor.Ui.CharacterId).Distinct().Count() == 4, "吕布、魏延、关羽、刘备各出现一次");
            Check(_arena.Fighters.All(actor => actor.Config.MaxHealth == (actor.Ui.CharacterId == "core:lubu" ? 1200 : 1000) && actor.Health.CurrentHealth == actor.Config.MaxHealth && actor.Health.MaxHealth == actor.Config.MaxHealth), "吕布1200、其余三人1000，初始生命和配置一致");
            Check(_arena.Enemies.All(actor => actor.Resolver == _arena.Resolver && actor.GetNode<WeiYanController>(NodeNames.Controller).Target == _arena.Player), "三名对手均接入伤害队列与AI目标");
            await Capture("four-fighters");

            // 对每一名武将都用真实身体碰撞检查闪避，包含玩家和敌方两套物理层。
            foreach (var actor in _arena.Fighters)
            {
                ParkEveryone();
                var obstacle = actor.IsEnemy ? _arena.Player : _arena.Enemy;
                actor.Position = new Vector2(290, 225); obstacle.Position = new Vector2(318, 225);
                actor.Face(Vector2.Right); await Frames(4);
                actor.SetMoveInput(Vector2.Right); await Frames(12); actor.SetMoveInput(Vector2.Zero);
                Check(actor.Position.X < obstacle.Position.X - 22, actor.Ui.DisplayName + "普通行走仍被对方身体阻挡");
                Check(actor.TryAction(CombatAction.Dodge), actor.Ui.DisplayName + "可进入闪避");
                Check(actor.CollisionLayer == 0 && actor.CollisionMask == PhysicsLayers.WorldBody, "闪避只保留世界碰撞，不改变受击框");
                Check(actor.GetNode<Hurtbox>(NodeNames.Hurtbox).CollisionLayer != 0, "闪避不是关闭受击或无敌");
                float dodgeTime = actor.ActionTime;
                GetTree().Paused = true; await Frames(3);
                Check(Mathf.IsEqualApprox(actor.ActionTime, dodgeTime), "暂停保留闪避时钟与穿人状态");
                GetTree().Paused = false; await Frames(30);
                Check(actor.Position.X > obstacle.Position.X + 16, actor.Ui.DisplayName + "实际从对手身后穿出");
                Check(actor.CollisionLayer == (actor.IsEnemy ? PhysicsLayers.EnemyBody : PhysicsLayers.PlayerBody)
                    && actor.CollisionMask == (actor.IsEnemy ? PhysicsLayers.EnemyBodyMask : PhysicsLayers.PlayerBodyMask), "闪避结束恢复完整身体阻挡");
                actor.Face(Vector2.Left); actor.SetMoveInput(Vector2.Left); await Frames(12); actor.SetMoveInput(Vector2.Zero);
                Check(actor.Position.X > obstacle.Position.X + 22, "恢复后反向行走再次被身体阻挡");
                await Frames(100);
            }

            ParkEveryone(); var player = _arena.Player; var enemy = _arena.Enemy;
            player.Position = new Vector2(290, 225); enemy.Position = new Vector2(327, 225);
            player.Face(Vector2.Right); enemy.SetMoveInput(Vector2.Left); await Frames(1);
            Check(player.TryAction(CombatAction.Dodge), "对方迎面移动时可启动闪避"); await Frames(20);
            Check(player.Position.X > enemy.Position.X && enemy.Position.X < 310, $"移动中的敌人也不会反向把闪避者挡住：玩家{player.Position.X:F1}，敌人{enemy.Position.X:F1}，方向{player.CastDirection}");
            enemy.SetMoveInput(Vector2.Zero); await Frames(100);

            ParkEveryone(); player.Position = new Vector2(560, 225); player.Face(Vector2.Right); await Frames(2);
            Check(player.TryAction(CombatAction.Dodge), "贴近墙体可启动闪避"); await Frames(30);
            Check(player.Position.X <= 580.2f && player.Position.X >= 565, "闪避依然被场地右墙阻挡，不穿出地图"); await Frames(90);

            ParkEveryone(); player.Position = new Vector2(260, 225); player.Face(Vector2.Right);
            Check(player.TryAction(CombatAction.Dodge), "可开始待打断的闪避"); await Frames(2);
            int hp = player.Health.CurrentHealth;
            player.ReceiveDamage(new DamageInfo(null, 7, Vector2.Zero));
            Check(player.CurrentState == Combatant.State.Hurt && player.Health.CurrentHealth == hp - 7
                && player.CollisionLayer == PhysicsLayers.PlayerBody && player.CollisionMask == PhysicsLayers.PlayerBodyMask, "闪避被命中可受伤打断，立即恢复碰撞");
            await Frames(35);

            // 范围反击必须能遍历三名对手，不能只注册旧Enemy引用。
            player.Health.ResetHealth(); player.Position = new Vector2(300, 225);
            var foes = _arena.Enemies.ToArray();
            for (int i = 0; i < foes.Length; i++) { foes[i].Position = player.Position + new Vector2(38, (i - 1) * 20); foes[i].Health.ResetHealth(); }
            player.TryEquipPassive(0, GD.Load<PassiveConfig>("res://Resources/Config/Skills/weiyan-counter.tres"));
            _arena.Resolver.RollOverride = () => 0;
            player.ReceiveDamage(new DamageInfo(foes[0], 1, Vector2.Zero, DamageKind.Attack));
            Check(foes.All(actor => actor.Health.CurrentHealth == actor.Health.MaxHealth - 30), "范围反击能同时结算三名已注册对手");
            await Reload();
            Check(_arena.Fighters.Count == 4 && _arena.Fighters.All(actor => actor.Health.CurrentHealth == actor.Health.MaxHealth && !actor.IsDead && actor.Status.EmpowerRemaining == 0), "重开重建四人并恢复独立生命配置及干净状态");

            // 让三名AI分别独立追击，确认新增角色不是只可见的展示模型。
            foreach (var foe in _arena.Enemies)
            {
                ParkEveryone(); _arena.Player.Health.ResetHealth(); _arena.Player.Position = new Vector2(220, 225);
                foe.Position = new Vector2(430, 225);
                foe.GetNode(NodeNames.Controller).ProcessMode = ProcessModeEnum.Inherit;
                await Frames(600);
                foe.GetNode(NodeNames.Controller).ProcessMode = ProcessModeEnum.Disabled; foe.SetMoveInput(Vector2.Zero);
                Check(_arena.Player.Health.CurrentHealth < _arena.Player.Health.MaxHealth, foe.Ui.DisplayName + "实际追击并通过技能造成伤害");
                await Frames(80);
            }
            await Reload();
            foes = _arena.Enemies.ToArray();
            foes[0].ReceiveDamage(new DamageInfo(null, 99999, Vector2.Zero));
            Check(!_arena.Finished && !foes[1].BattleFinished && !foes[2].BattleFinished, "击败第一名对手不提前结束");
            foes[1].ReceiveDamage(new DamageInfo(null, 99999, Vector2.Zero));
            Check(!_arena.Finished, "击败第二名对手仍继续对战");
            foes[2].ReceiveDamage(new DamageInfo(null, 99999, Vector2.Zero));
            Check(_arena.Finished && _arena.Result == BattleTexts.Victory && _arena.Fighters.All(actor => actor.BattleFinished), "三名对手全部击败才胜利，全员停止行动");
            await Reload();
            Check(_arena.Player.TryAction(CombatAction.Dodge), "重开后闪避冷却恢复");
            _arena.Player.ReceiveDamage(new DamageInfo(null, 99999, Vector2.Zero));
            Check(_arena.Finished && _arena.Player.IsDead && _arena.Player.CollisionLayer == 0 && _arena.Player.CollisionMask == 0
                && _arena.Enemies.All(actor => actor.BattleFinished), "闪避中死亡清除身体碰撞，三名对手一并结算");
            await Reload();

            // 换主控角色后，其余三人仍自动补齐，重复选择不会生成相同身份。
            HeroCatalog.PlayerIndex = 2; HeroCatalog.EnemyIndex = 2;
            await Reload();
            Check(_arena.Player.Ui.CharacterId == "core:guanyu" && _arena.Fighters.Select(actor => actor.Ui.CharacterId).Distinct().Count() == 4, "切换操控关羽后仍自动补齐四名不同武将");
            HeroCatalog.PlayerIndex = 0; HeroCatalog.EnemyIndex = 1;
            GD.Print($"ARENA_GROUP_TEST_PASS: {_checks} 项；真实渲染 {DisplayServer.GetName() != "headless"}。");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError("ARENA_GROUP_TEST_FAIL: " + error); GetTree().Paused = false; GetTree().Quit(1); }
    }

    private void DisableControllers()
    {
        foreach (var actor in _arena.Fighters) { actor.GetNode(NodeNames.Controller).ProcessMode = ProcessModeEnum.Disabled; actor.SetMoveInput(Vector2.Zero); }
    }
    private void ParkEveryone()
    {
        foreach (var actor in _arena.Fighters) { actor.Position = new Vector2(-1000, -1000); actor.SetMoveInput(Vector2.Zero); }
    }
    private async Task Reload()
    {
        GetTree().Paused = false;
        using var input = new InputEventAction { Action = InputActions.Restart, Pressed = true };
        _arena.GetNode<BattleHud>(NodeNames.Hud)._UnhandledInput(input);
        await Frames(5); _arena = (Arena)GetTree().CurrentScene; DisableControllers();
    }
    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++) { await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    }
    private async Task Capture(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage(); image.SavePng($"res://Build/{name}.png");
    }
    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++; GD.Print("通过：" + message);
    }
}
