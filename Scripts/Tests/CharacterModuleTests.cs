using Godot;
using System;
using System.Threading.Tasks;

/// <summary>验证武将模型、动画、UI 与技能装备独立；真实交换技能并检查伤害、动作、冷却和界面。</summary>
public partial class CharacterModuleTests : Node
{
    private Arena _arena;
    private Combatant Player => _arena.Player;
    private Combatant Enemy => _arena.Enemy;
    private int _checks;

    public override void _Ready() => CallDeferred(MethodName.Run);

    private async void Run()
    {
        try
        {
            _arena = (Arena)GetTree().CurrentScene;
            foreach (var actor in new[] { Player, Enemy })
                actor.GetNode(NodeNames.Controller).ProcessMode = ProcessModeEnum.Disabled;
            _arena.Resolver.PassivesEnabled = false;
            await Frames(20);
            var playerModel = Player.Presentation.Model;
            var enemyModel = Enemy.Presentation.Model;
            var playerAnimation = Player.Presentation.Animation;
            var enemyAnimation = Enemy.Presentation.Animation;
            var playerUi = Player.Ui;
            var enemyUi = Enemy.Ui;
            Check(playerModel != enemyModel && playerAnimation != enemyAnimation && playerUi != enemyUi, "双方独立模型、动作和UI资源");
            Check(playerModel.JointPositions.Count == 16 && enemyModel.JointPositions.Count == 16
                && playerModel.PartRects.Count == 16 && enemyModel.PartRects.Count == 16, "每位武将显式保存完整关节及拆件绑定");
            Check(!Enemy.Rig.Parts[15].Visible && Player.Rig.Parts[15].Visible, "魏延关闭重复盔缨，吕布仍显示独立双翎");
            var head = (AtlasTexture)Enemy.Rig.Parts[2].Texture;
            Check(head.Region.Position.Y < 30 && head.Region.Size.Y > 290,
                $"魏延完整头图保留盔缨根部，不再从85像素截断：{head.Region}");

            var originalBasic = Player.GetSkill(CombatAction.Basic);
            var borrowedBasic = Enemy.GetSkill(CombatAction.Basic);
            Check(Player.TryEquipSkill(CombatAction.Basic, borrowedBasic)
                && Enemy.TryEquipSkill(CombatAction.Basic, originalBasic), "双方直接交换普攻技能定义");
            Check(Player.Loadout.Basic == originalBasic && Enemy.Loadout.Basic == borrowedBasic, "换装不改写共享默认装备资源");
            PlaceDuel();
            int health = Enemy.Health.CurrentHealth;
            Check(Player.TryAction(CombatAction.Basic), "吕布可使用魏延普攻");
            await Frames(21);
            Check(Mathf.Abs(Mathf.RadToDeg(Player.Rig.Joints[14].Rotation) + 38) < 1,
                "借用魏延技能时仍使用吕布的收戟动作");
            Check(Player.Spec.Damage == borrowedBasic.Damage && Mathf.IsEqualApprox(Player.Spec.Prepare, borrowedBasic.Prepare), "本次技能快照来自魏延技能规则");
            Check(!Player.TryEquipSkill(CombatAction.Basic, originalBasic), "出招过程中拒绝换装，不扰动正在施放的技能");
            await Frames(45);
            Check(health - Enemy.Health.CurrentHealth == borrowedBasic.Damage, "真实命中使用借入技能伤害");

            PlaceDuel();
            health = Player.Health.CurrentHealth;
            Check(Enemy.TryAction(CombatAction.Basic), "魏延可使用吕布普攻");
            await Frames(8);
            Check(Mathf.Abs(Mathf.RadToDeg(Enemy.Rig.Joints[14].Rotation) + 28) < 1, "借用吕布技能时仍使用魏延的短收刀动作");
            await Frames(40);
            Check(health - Player.Health.CurrentHealth == originalBasic.Damage, "反向交换同样按借入伤害结算");

            // 将横扫放入原K突进槽，确认输入槽位不决定动作、名称或判定。
            Player.Position = new Vector2(210, 245);
            Enemy.Position = new Vector2(490, 245);
            var borrowedSweep = Enemy.GetSkill(CombatAction.Sweep);
            var originalDash = Player.GetSkill(CombatAction.Dash);
            Check(Player.TryEquipSkill(CombatAction.Dash, borrowedSweep), "横扫可以装入原突进输入槽");
            Check(Player.TryAction(CombatAction.Dash) && Player.CurrentMotion == CombatMotion.Sweep
                && Player.Spec.Damage == borrowedSweep.Damage, "K槽按横扫语义执行，不被槽名强制成突进");
            await Frames(75);
            float remaining = Player.Cooldown(CombatAction.Dash);
            Check(remaining > 4 && Enemy.Cooldown(CombatAction.Sweep) == 0, "同一技能在不同单位上的冷却完全独立");
            Check(Player.TryEquipSkill(CombatAction.Dash, originalDash)
                && Player.TryEquipSkill(CombatAction.Dash, borrowedSweep), "允许卸下后重新装备同一技能");
            Check(Mathf.IsEqualApprox(Player.Cooldown(CombatAction.Dash), remaining)
                && !Player.TryAction(CombatAction.Dash), "卸装重装不会清空冷却或绕过冷却施法");
            Check(!Player.TryEquipSkill(CombatAction.Basic, borrowedSweep), "同一单位禁止重复装入相同技能ID");
            await Frames(2);
            Check(HasLabel(_arena.GetNode(NodeNames.Hud), text => text.Contains("K 转腰横扫")), "HUD技能名跟随当前装备，不写死突进标签");

            var playerPassive = Player.Skills.Passives[0];
            var enemyPassive = Enemy.Skills.Passives[0];
            Check(Player.TryEquipPassive(0, enemyPassive) && Enemy.TryEquipPassive(0, playerPassive), "反伤和反击被动也能跨武将交换");
            PlaceDuel();
            _arena.Resolver.PassivesEnabled = true;
            _arena.Resolver.RollOverride = () => .5f; // 触发60%反击，避免双方低概率暴击。
            health = Enemy.Health.CurrentHealth;
            Player.ReceiveDamage(new DamageInfo(Enemy, 10, Vector2.Left, DamageKind.Attack));
            Check(Player.CounterRemaining > 0 && health - Enemy.Health.CurrentHealth == 30, "吕布装备狂骨后实际触发30点反击");
            await Frames(40);
            PlaceDuel();
            health = Player.Health.CurrentHealth;
            Enemy.ReceiveDamage(new DamageInfo(Player, 10, Vector2.Right, DamageKind.Attack));
            Check(health - Player.Health.CurrentHealth == 20 && Player.LastDamage.Kind == DamageKind.Reflected, "魏延装备荆甲后实际反伤20点");
            Check(Player.Loadout.Passives[0] == playerPassive && Enemy.Loadout.Passives[0] == enemyPassive, "被动换装不污染其他单位或默认资源");
            Check(Player.Presentation.Model == playerModel && Enemy.Presentation.Model == enemyModel
                && Player.Presentation.Animation == playerAnimation && Enemy.Presentation.Animation == enemyAnimation
                && Player.Ui == playerUi && Enemy.Ui == enemyUi, "主动被动交换后双方模型、动作、UI身份保持自身配置");
            await Frames(35);
            Check(HasLabel(_arena.GetNode(NodeNames.Hud), text => text.Contains("狂骨旋斩"))
                && HasLabel(_arena.GetNode(NodeNames.Hud), text => text.Contains("荆甲")), "HUD被动列表跟随实时装备刷新");
            if (DisplayServer.GetName() != "headless")
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var image = GetViewport().GetTexture().GetImage();
                image.SavePng("res://Build/character-module-swapped-hud.png");
            }
            GD.Print($"武将模块回归通过：{_checks} 项。");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"武将模块回归失败：{exception}");
            GetTree().Quit(1);
        }
    }

    private void PlaceDuel()
    {
        Player.Position = new Vector2(280, 240); Enemy.Position = new Vector2(318, 240);
        Player.SetMoveInput(Vector2.Zero); Enemy.SetMoveInput(Vector2.Zero);
        Player.Face(Vector2.Right); Enemy.Face(Vector2.Left);
    }

    private static bool HasLabel(Node root, Func<string, bool> match)
    {
        if (root is Label label && match(label.Text)) return true;
        foreach (var child in root.GetChildren()) if (HasLabel(child, match)) return true;
        return false;
    }

    private async Task Frames(int count)
    {
        for (int index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++; GD.Print($"通过：{message}");
    }
}
