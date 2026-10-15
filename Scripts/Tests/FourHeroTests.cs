using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>四武将真实驱动验收：独立动作、双剑握点、概率被动、状态、治疗及换技能后保留外观。</summary>
public partial class FourHeroTests : Node
{
    private Arena _arena;
    private int _checks;
    private bool Rendered => DisplayServer.GetName() != "headless";
    private Combatant _subject;
    private float _minElbow, _maxElbow, _gripError;
    private string _worst;
    public override void _Ready() { ProcessMode = ProcessModeEnum.Always; CallDeferred(MethodName.Run); }

    private async void Run()
    {
        try
        {
            _arena = (Arena)GetTree().CurrentScene;
            foreach (var actor in new[] { _arena.Player, _arena.Enemy }) { actor.ProcessMode = ProcessModeEnum.Disabled; actor.Hide(); actor.Position = new Vector2(-1000, -1000); }
            _arena.GetNode<CanvasLayer>(NodeNames.Hud).Hide();
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://Build/four-heroes"));
            var animations = new HashSet<CharacterAnimationConfig>();
            var skillIds = new HashSet<string>();
            var passiveIds = new HashSet<string>();
            var signatures = new Dictionary<string, HashSet<string>>();
            for (int index = 0; index < 4; index++)
            {
                var actor = Spawn(index, false);
                Check(animations.Add(actor.Presentation.Animation), $"{actor.Ui.DisplayName}使用独立动作集");
                Check(actor.Skills.Passives.Count == 1, $"{actor.Ui.DisplayName}恰好装备一个被动，另有两个主动与基础普攻/闪避");
                Check(passiveIds.Add(actor.Skills.Passives[0].SkillId), "四武将默认被动均不重复");
                var animation = actor.Presentation.Animation;
                foreach (var (name, pose) in new[] { ("待机", animation.StandingPose), ("移动", animation.MovingPose),
                    ("普攻", animation.Basic.Contact), ("K技能", animation.Dash.Contact), ("L技能", animation.Sweep.Contact), ("闪避", animation.DodgePose) })
                {
                    if (!signatures.ContainsKey(name)) signatures[name] = new HashSet<string>();
                    Check(signatures[name].Add($"{pose.FarHand}/{pose.FreeHand}/{pose.WeaponDegrees}/{pose.OffhandWeaponDegrees}/{pose.ChestDegrees}"), $"{actor.Ui.DisplayName}{name}握点和动作姿态不与其他武将雷同");
                }
                foreach (var slot in new[] { CombatAction.Dash, CombatAction.Sweep }) Check(skillIds.Add(actor.GetSkill(slot).SkillId), "四武将主动技能ID均独立");
                await Frames(3); // GameEntry跨场景启动后，先等待新骨架完成首次姿态采样，再测动画握点。
                _subject = actor; _minElbow = 180; _maxElbow = _gripError = 0;
                await Frames(24); await Capture(actor, "idle");
                for (int direction = 0; direction < 8; direction++)
                {
                    actor.Position = new Vector2(300, 240);
                    actor.SetMoveInput(Vector2.Right.Rotated(direction * Mathf.Pi / 4));
                    await Frames(35);
                    if (direction is 0 or 4) await Capture(actor, $"walk-{direction}");
                }
                actor.SetMoveInput(Vector2.Zero); await Frames(24);
                actor.Face(Vector2.Right);
                foreach (var action in new[] { CombatAction.Basic, CombatAction.Dash, CombatAction.Sweep, CombatAction.Dodge })
                {
                    actor.Position = new Vector2(300, 240); actor.SetMoveInput(Vector2.Right); await Frames(16);
                    Check(actor.TryAction(action), $"{actor.Ui.DisplayName}从移动启动{action}");
                    actor.SetMoveInput(Vector2.Zero);
                    int count = Mathf.CeilToInt(actor.Spec.Duration * 60) + 30;
                    for (int frame = 0; frame < count; frame++)
                    {
                        await Frames(1);
                        if (frame is 5 or 12 or 20 or 28 or 38) await Capture(actor, $"{action}-{frame:D2}");
                    }
                }
                actor.PlayCounterSpin(68, .48f); await Frames(45);
                actor.ReceiveDamage(new DamageInfo(null, 1, Vector2.Right)); await Frames(35);
                GD.Print($"{actor.Ui.DisplayName} 屈肘 {_minElbow:F1}～{_maxElbow:F1}，握点误差 {_gripError:F3}；{_worst}");
                Check(_minElbow > 20 && _maxElbow < 150 && _gripError < .12f, $"{actor.Ui.DisplayName}全程握点与手臂可达性正常");
                if (index == 3) Check(actor.Rig.OffhandWeapon != null && actor.Rig.DrawOrderedParts.Contains(actor.Rig.OffhandWeapon), "刘备存在真实独立副剑图层");
                _subject = null; actor.QueueFree(); await Frames(2);
            }
            await CheckMechanics();
            await CheckRoster();
            GD.Print($"FOUR_HERO_TEST_PASS: {_checks} 项；真实渲染 {Rendered}。");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError($"FOUR_HERO_TEST_FAIL: {error}"); GetTree().Paused = false; GetTree().Quit(1); }
    }

    private Combatant Spawn(int index, bool enemy)
    {
        var actor = GD.Load<PackedScene>("res://Components/Characters/Combatant.tscn").Instantiate<Combatant>();
        HeroCatalog.Configure(actor, index); actor.IsEnemy = enemy; actor.Name = HeroCatalog.Ids[index] + (enemy ? "敌" : "友");
        actor.Position = new Vector2(enemy ? 342 : 300, 240);
        _arena.GetNode<Node2D>("Fighters").AddChild(actor);
        actor.GetNode<GroundEffects>(NodeNames.GroundEffects).Hide();
        return actor;
    }

    private async Task CheckMechanics()
    {
        var guan = Spawn(2, false); var liu = Spawn(3, true);
        var resolver = new CombatResolver(); resolver.Register(guan); resolver.Register(liu);
        guan.Resolver = liu.Resolver = resolver;
        await Frames(8);
        int hp = guan.Health.CurrentHealth;
        resolver.RollOverride = () => 0;
        guan.ReceiveDamage(new DamageInfo(liu, 20, Vector2.Left, DamageKind.Attack, false, .8f, .5f, .3f, 2));
        Check(guan.Health.CurrentHealth == hp && guan.Status.StunRemaining == 0 && guan.Status.SlowRemaining == 0, "关羽成功闪避不扣血、不受控、不挂负面状态");
        resolver.RollOverride = () => .18f;
        guan.ReceiveDamage(new DamageInfo(liu, 10, Vector2.Left, DamageKind.Attack));
        Check(guan.Health.CurrentHealth == hp - 10, "闪避概率边界使用严格小于");
        await Frames(25); resolver.RollOverride = () => 0;
        hp = liu.Health.CurrentHealth;
        liu.ReceiveDamage(new DamageInfo(guan, 10, Vector2.Right, DamageKind.Attack));
        Check(liu.Health.CurrentHealth == hp - 20 && liu.LastDamage.IsCritical, "关羽独立掷暴击，命中为2倍总伤害");
        await Frames(25); resolver.RollOverride = () => .99f;
        liu.Health.ApplyDamage(new DamageInfo(null, 20, Vector2.Zero));
        hp = liu.Health.CurrentHealth;
        guan.ReceiveDamage(new DamageInfo(liu, 20, Vector2.Left, DamageKind.Attack));
        Check(liu.Health.CurrentHealth == hp + 3, "刘备按实际20点伤害回复3点生命");
        await Frames(25);
        // 可交换的通用状态规则，不依赖关羽名字或长柄模型。
        var smash = GD.Load<CombatActionConfig>("res://Resources/Config/Skills/guanyu-dash.tres");
        Check(liu.TryEquipSkill(CombatAction.Dash, smash), "刘备可以借用关羽主动技能");
        resolver.PassivesEnabled = false;
        guan.Position = new Vector2(300, 240); liu.Position = new Vector2(342, 240); liu.Face(Vector2.Left);
        Check(liu.TryAction(CombatAction.Dash), "双剑模型可执行借入重劈技能");
        await Frames(32);
        Check(guan.Status.StunRemaining > 0 && !guan.CanAct && liu.Rig.OffhandWeapon != null, "借入技能实际眩晕，模型仍保留双剑");
        await Frames(65);
        guan.Health.ResetHealth(); liu.Health.ResetHealth();
        var slow = GD.Load<CombatActionConfig>("res://Resources/Config/Skills/guanyu-sweep.tres");
        guan.Face(Vector2.Right); guan.Position = new Vector2(300, 240); liu.Position = new Vector2(342, 240);
        Check(guan.TryAction(CombatAction.Sweep), "关羽可释放范围震地"); await Frames(38);
        Check(liu.Status.MoveMultiplier < .6f, "震地实际施加45%减速");
        await Frames(150); Check(Mathf.IsEqualApprox(liu.Status.MoveMultiplier, 1), "减速到期自动恢复，不污染角色基础移速");
        var combo = GD.Load<CombatActionConfig>("res://Resources/Config/Skills/liubei-dash.tres");
        liu.TryEquipSkill(CombatAction.Dash, combo); guan.Health.ResetHealth();
        // 多段命中测试保留碰撞与真实生效窗口，降低击退避免第一剑把靶子推出范围。
        guan.Config = (CombatantConfig)guan.Config.Duplicate(); guan.Config.KnockbackSpeed = 0;
        guan.Position = new Vector2(300, 240); liu.Position = new Vector2(340, 240); liu.Face(Vector2.Left);
        hp = guan.Health.CurrentHealth;
        Check(liu.TryAction(CombatAction.Dash), "刘备可使用三段双剑连击"); await Frames(80);
        Check(hp - guan.Health.CurrentHealth == 30, "双剑连击按3段各10结算，同段不重复命中");
        liu.Health.ApplyDamage(new DamageInfo(null, 35, Vector2.Zero)); hp = liu.Health.CurrentHealth;
        Check(liu.TryAction(CombatAction.Sweep), "刘备可释放仁德治疗震击"); await Frames(60);
        Check(liu.Health.CurrentHealth == hp + 24, "仁德只在生效节点治疗一次且不超上限");
        await Frames(4); guan.QueueFree(); liu.QueueFree(); await Frames(2);
        var wei = Spawn(1, false); var target = Spawn(3, true);
        resolver = new CombatResolver { PassivesEnabled = false };
        resolver.Register(wei); resolver.Register(target); wei.Resolver = target.Resolver = resolver;
        target.Config = (CombatantConfig)target.Config.Duplicate(); target.Config.KnockbackSpeed = 0;
        wei.Face(Vector2.Right); await Frames(3);
        Check(wei.TryAction(CombatAction.Dash), "魏延可使用裂甲重斩"); await Frames(55);
        Check(target.Status.IncomingMultiplier > 1.29f, "裂甲实际附加30%易伤");
        hp = target.Health.CurrentHealth;
        target.ReceiveDamage(new DamageInfo(wei, 10, Vector2.Right, DamageKind.Attack));
        Check(hp - target.Health.CurrentHealth == 13, "后续攻击按易伤倍率结算");
        await Frames(210);
        Check(Mathf.IsEqualApprox(target.Status.IncomingMultiplier, 1), "易伤到期自动移除");
        target.Position = new Vector2(520, 240); hp = wei.Health.CurrentHealth;
        Check(wei.TryAction(CombatAction.Sweep), "魏延可使用狂骨战意"); await Frames(60);
        Check(wei.Health.CurrentHealth == hp - 10 && Mathf.IsEqualApprox(wei.Status.DamageMultiplier, 1.45f), "狂骨仅扣除一次10点生命并强化伤害");
        hp = wei.Health.CurrentHealth; int targetHp = target.Health.CurrentHealth;
        target.ReceiveDamage(new DamageInfo(wei, 20, Vector2.Right, DamageKind.Attack));
        Check(targetHp - target.Health.CurrentHealth == 29 && wei.Health.CurrentHealth == hp + 7, "狂骨强化伤害及按实际伤害吸血");
        Check(wei.TryEquipSkill(CombatAction.Sweep, slow) && wei.Status.EmpowerRemaining > 0
            && target.Status.EmpowerRemaining == 0, "换装不清除已生效状态，也不修改另一单位");
        await Frames(245);
        Check(Mathf.IsEqualApprox(wei.Status.DamageMultiplier, 1) && wei.Status.Lifesteal == 0, "强化到期恢复原伤害及吸血");
        wei.TryEquipSkill(CombatAction.Sweep, GD.Load<CombatActionConfig>("res://Resources/Config/Skills/weiyan-sweep.tres"));
        await Frames(260); hp = wei.Health.CurrentHealth;
        Check(wei.TryAction(CombatAction.Sweep), "冷却结束可重新施放狂骨");
        wei.ReceiveDamage(new DamageInfo(null, 1, Vector2.Zero)); await Frames(65);
        Check(wei.Status.EmpowerRemaining == 0 && wei.Health.CurrentHealth == hp - 1
            && wei.Cooldown(CombatAction.Sweep) > 0, "前摇被打断不扣施法生命、不强化，也不退还冷却");
        wei.QueueFree(); target.QueueFree(); await Frames(2);
    }

    /// <summary>触发真实选择按钮和开始回调，验证暂停、取消、身份重建及重开保持选择。</summary>
    private async Task CheckRoster()
    {
        var hud = _arena.GetNode<BattleHud>(NodeNames.Hud); hud.Show();
        var choose = Descendants(hud).OfType<Button>().Single(b => b.Text == "选武将");
        choose.EmitSignal(BaseButton.SignalName.Pressed); await Frames(2);
        Check(GetTree().Paused, "打开选武将会暂停当前战斗");
        var roster = Descendants(hud).OfType<Control>().Single(n => n.Name == "武将选择");
        Descendants(roster).OfType<Button>().Single(b => b.Text == "返回").EmitSignal(BaseButton.SignalName.Pressed);
        Check(!GetTree().Paused && !roster.Visible, "返回恢复原来的战斗状态");
        choose.EmitSignal(BaseButton.SignalName.Pressed);
        var picks = Descendants(roster).OfType<OptionButton>().ToArray();
        Check(picks.Length == 2 && picks.All(p => p.ItemCount == 4), "我方和对手均可选择全部四名武将");
        picks[0].Select(2); picks[1].Select(3);
        picks[0].EmitSignal(OptionButton.SignalName.ItemSelected, 2L); await Frames(2);
        if (Rendered) { await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); using var shot = GetViewport().GetTexture().GetImage(); shot.SavePng("res://Build/four-heroes/roster.png"); }
        Descendants(roster).OfType<Button>().Single(b => b.Text == "开始对战").EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(5); _arena = (Arena)GetTree().CurrentScene;
        Check(!GetTree().Paused && _arena.Player.Ui.CharacterId == "core:guanyu" && _arena.Enemy.Ui.CharacterId == "core:liubei"
            && _arena.Enemy.Rig.OffhandWeapon != null, "选择关羽对刘备后实际重建模型、双剑和身份");
        foreach (var actor in new[] { _arena.Player, _arena.Enemy }) actor.GetNode(NodeNames.Controller).ProcessMode = ProcessModeEnum.Disabled;
        if (Rendered) { await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); using var shot = GetViewport().GetTexture().GetImage(); shot.SavePng("res://Build/four-heroes/guanyu-liubei-arena.png"); }
        var instance = _arena.GetInstanceId();
        using var input = new InputEventAction { Action = InputActions.Restart, Pressed = true };
        _arena.GetNode<BattleHud>(NodeNames.Hud)._UnhandledInput(input); await Frames(5);
        _arena = (Arena)GetTree().CurrentScene;
        Check(_arena.GetInstanceId() != instance && _arena.Player.Ui.CharacterId == "core:guanyu"
            && _arena.Enemy.Ui.CharacterId == "core:liubei", "正常重开保留本进程选择的双方武将");
        HeroCatalog.PlayerIndex = 0; HeroCatalog.EnemyIndex = 1;
    }
    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren()) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_subject != null)
            {
                var rig = _subject.Rig;
                foreach (int bone in new[] { 4, 7 })
                {
                    float bend = Mathf.Abs(Mathf.RadToDeg(rig.Joints[bone].Rotation));
                    if (bend > _maxElbow) _worst = $"{_subject.CurrentState}/{_subject.CurrentMotion} t={_subject.ActionTime:F3} 臂={bone} 屈肘={bend:F1}";
                    _minElbow = Mathf.Min(_minElbow, bend); _maxElbow = Mathf.Max(_maxElbow, bend);
                }
                float primaryError = rig.Skeleton.ToLocal(rig.Joints[5].GlobalPosition).DistanceTo(rig.ReferencePrimaryGrip);
                if (primaryError > _gripError) _gripError = primaryError;
                if (_subject.Presentation.Model.DualWeapons || rig.ReferenceGripWeight >= .9999f)
                {
                    float secondaryError = rig.Skeleton.ToLocal(rig.Joints[8].GlobalPosition).DistanceTo(rig.ReferenceSecondaryGrip);
                    if (secondaryError > _gripError) _gripError = secondaryError;
                }
            }
        }
    }
    private async Task Capture(Combatant actor, string name)
    {
        if (!Rendered) return;
        var viewport = new SubViewport { Size = new Vector2I(760, 620), TransparentBg = true, Disable3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(viewport);
        var root = new Node2D { Position = new Vector2(330, 510), Scale = new Vector2(4, 3.68f) }; viewport.AddChild(root);
        foreach (var part in actor.Rig.DrawOrderedParts)
        {
            if (!part.Visible) continue;
            var copy = new Sprite2D { Texture = part.Texture, Material = part.Material, Centered = part.Centered, FlipH = part.FlipH, FlipV = part.FlipV, TextureFilter = part.TextureFilter };
            root.AddChild(copy); copy.Transform = actor.Rig.Skeleton.GlobalTransform.AffineInverse() * part.GlobalTransform;
        }
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = viewport.GetTexture().GetImage(); image.SavePng($"res://Build/four-heroes/{actor.Ui.CharacterId.Replace("core:", "")}-{name}.png");
        viewport.QueueFree();
    }
    private void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); _checks++; GD.Print("通过：" + message); }
}
