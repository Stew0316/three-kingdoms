using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>跨场景首城验收：真实UI输入、碰撞、训练、马速、任务结算及持久化快照。</summary>
public partial class FirstCityTests : Node
{
    private int _checks;
    private FirstCity City => (FirstCity)GetTree().CurrentScene;
    private AdventureSession Session => AdventureSession.Instance;
    public override void _Ready() { ProcessMode=ProcessModeEnum.Always; CallDeferred(MethodName.Run); }
    private async void Run()
    {
        try
        {
            await Frames(5);
            var config=Session.Config;
            var isolated=new AdventureProgress();
            Check(!isolated.Smash() && !isolated.ClaimCache(),"未学崩击不能破石或领隐藏奖励");
            Check(!isolated.EncounterCleared("core:encounter/weiyan_trial") && !isolated.ClaimTrial(config),"未接任务不能提前完成或领奖");
            isolated.AcceptTrial(); Check(!isolated.EncounterCleared("other"),"其他遭遇不推进魏延试炼");
            isolated.Coins=0; Check(!isolated.Forge(config),"铜钱不足拒绝开锋");
            Check(City.Interactions.Count(i=>i.IsNpc)==5 && City.Map.Actors.GetChildren().OfType<CityBuilding>().Count()==5,"五名NPC、五栋功能建筑");
            Check(City.Map.GetNode<TileMapLayer>("Ground").GetUsedCells().Count==2400,"真实TileMap铺设60×40地面");
            await Capture("00-city-entry");
            // 相同入口状态下输出全景，便于查看五间建筑与三条街的空间关系。
            Vector2 normalZoom=City.Camera.Zoom;
            City.Camera.PositionSmoothingEnabled=false; City.Camera.Zoom=new(.46f,.46f);
            City.Player.Position=new(480,422); await Frames(3); await Capture("00-city-overview");
            City.Camera.Zoom=normalZoom; City.Camera.PositionSmoothingEnabled=true;
            PressKey(City,Key.M); await Frames(2); await Capture("01-city-map"); PressKey(City,Key.M);
            await Visit("boulder",new(864,317));
            Check(City.Dialogue.IsOpen,"E打开巨石调查对话"); Choose("试着击打"); await Frames(65);
            Check(!Session.Progress.BoulderBroken && City.Map.BoulderBody.CollisionLayer==1,"未习得能力时击打巨石不解除碰撞");
            City.Player.Position=new(864,315); await Move("move_down",35);
            Check(City.Player.Position.Y<340,"巨石实际阻挡角色进入隐藏小路");
            await Visit("captain",new(466,302));
            await Capture("02-dialogue");
            Vector2 before=City.Player.Position; await Move("move_right",8);
            Check(City.Player.Position.DistanceTo(before)<1,"对话打开时移动输入被拦截");
            Choose("接下演武任务"); Check(Session.Progress.CanSmash,"接任务立即获得地形崩击能力");
            for(int i=0;i<3;i++)
            {
                await Visit("captain",new(466,302)); Choose("锻炼第");
                Check(City.TrainingRemaining>0,"真实对话按钮开始有时长的锻炼");
                await Frames(270);
                Check(Session.Progress.TrainingCount==i+1,"训练结束才发放第"+(i+1)+"次成长");
            }
            Check(Session.Progress.HealthBonus(config)==190 && Math.Abs(Session.Progress.PowerMultiplier(config)-1.12f)<.001,"三次训练递减累计190生命及12%伤害");
            City.BeginTraining(); Check(City.TrainingRemaining==0,"达到三次上限不能继续刷数值");
            await Visit("smith",new(748,278)); Choose("开锋");
            Check(Session.Progress.WeaponForged && Session.Progress.Coins==60,"铁匠扣60铜钱并记录一次性开锋");
            Check(GD.Load<CombatantConfig>("res://Resources/Config/Characters/lubu/lubu.tres").MaxHealth==1200,"成长不污染共享武将资源");
            await Visit("inn",new(169,270)); Choose("歇息"); Check(City.Player.Health.CurrentHealth==1390,"驿站恢复实际成长后的生命上限");
            City.Player.Position=new(710,570); await Frames(3); await Capture("03-stable");
            await Visit("horse-brown",new(671,519)); Choose("上马"); await Frames(4);
            Check(City.MountedHorse==1 && City.Player.IsMounted && City.Player.ExplorationSpeedOverride==190,"枣红马使用独立190速度");
            City.Player.Position=new(360,307); await Frames(3); float start=City.Player.Position.X;
            await Move("move_right",24); float brown=City.Player.Position.X-start;
            await Capture("03-brown-horse"); PressKey(City,Key.X); Check(!City.Player.IsMounted,"X下马恢复徒步模式");
            await Visit("horse-white",new(748,519)); Choose("上马");
            City.Player.Position=new(360,307); await Frames(3); start=City.Player.Position.X;
            await Move("move_right",24); float white=City.Player.Position.X-start;
            Check(white>brown*1.22f && white<brown*1.42f,$"真实碰撞位移体现不同马速：枣红{brown:0.0} / 青骢{white:0.0}");
            Check(City.Player.Rig.Parts.Where(p=>p.Name.ToString().Contains("武器")).All(p=>!p.Visible),"骑乘隐藏手持武器，改为执缰姿态");
            await Capture("04-white-horse");
            City.Player.Position=new(917,294); await Move("move_right",25); Check(City.Player.Position.X<922,"高速骑马仍受城墙碰撞限制");
            PressKey(City,Key.X);
            await Visit("boulder",new(864,317)); Choose("施展崩击"); await Frames(65);
            Check(Session.Progress.BoulderBroken && City.Map.Broken,"崩击在动作后解除巨石及碰撞");
            City.Player.Position=new(864,318); await Move("move_down",105);
            Check(City.Player.Position.Y>490,"碎石后可实际沿隐藏小路行走");
            await Visit("cache",new(864,530));
            Check(Session.Progress.CacheClaimed && Session.Progress.Coins==140,"隐藏木匣发放80铜钱");
            City.Dialogue.Close(); Check(!Session.Progress.ClaimCache(),"隐藏奖励不能重复领取"); await Capture("05-hidden-path");
            await Visit("gate",new(866,294)); Choose("出城"); await Frames(7);
            var arena=(Arena)GetTree().CurrentScene; DisableAi(arena);
            Check(arena.IsGroupPractice && arena.Fighters.Count==4 && arena.Fighters.Select(actor=>actor.Ui.CharacterId).Distinct().Count()==4,"从城门默认进入四人演武场，关羽和刘备实际在场");
            Check(arena.Player.Health.MaxHealth==1390 && arena.Enemy.Health.MaxHealth==1000,"训练成长仅作用于冒险玩家");
            arena.Resolver.PassivesEnabled=false; int hp=arena.Enemy.Health.CurrentHealth;
            arena.Enemy.ReceiveDamage(new DamageInfo(arena.Player,100,Vector2.Zero,DamageKind.Attack));
            Check(hp-arena.Enemy.Health.CurrentHealth==122,"军营与锻造加成进入真实伤害结算：100→122");
            arena.Enemy.ReceiveDamage(new DamageInfo(arena.Player,99999,Vector2.Zero,DamageKind.Attack));
            Check(!arena.Finished && Session.Progress.Trial==AdventureProgress.QuestStage.Completed,"击败魏延完成任务，关羽和刘备尚存时演武继续且不自动领奖");
            Check(arena.Enemies.Where(actor=>actor!=arena.Enemy).All(actor=>!actor.IsDead && actor.Health.MaxHealth==1000),"关羽与刘备保持1000生命上限且继续在场");
            ReturnButton(arena); await Frames(8);
            Check(City.Map.Broken && Session.Progress.CacheClaimed && Session.Progress.TrainingCount==3,"往返切图保留碎石、奖励与成长");
            await Visit("captain",new(466,302)); Choose("交付任务");
            Check(Session.Progress.HasBook && Session.Progress.Coins==240,"回城交任务获得兵书与100铜钱");
            Check(!Session.Progress.ClaimTrial(config),"任务奖励防重复领取");
            Check(Math.Abs(Session.Progress.PowerMultiplier(config)-1.32f)<.001,"兵书已研习并增加真实伤害倍率");
            await Capture("06-quest-complete");
            string json=JsonSerializer.Serialize(Session.Progress);
            var restored=JsonSerializer.Deserialize<AdventureProgress>(json);
            Check(restored.HasBook && restored.BoulderBroken && restored.CacheClaimed && restored.TrainingCount==3 && restored.WeaponForged && restored.Coins==240,"JSON快照完整恢复任务、地图、成长与货币");
            DirAccess.MakeDirRecursiveAbsolute("res://Build/first-city");
            const string testSave="res://Build/first-city/isolated-save.json";
            Check(AdventureSaveStore.Write(testSave,new AdventureProgress())==Error.Ok && AdventureSaveStore.Write(testSave,Session.Progress)==Error.Ok,"存档临时写入及覆盖旧版本均成功");
            var disk=AdventureSaveStore.Read(testSave,config);
            Check(disk.HasBook && disk.TrainingCount==3 && disk.BoulderBroken && disk.Coins==240,"实际文件读回已完成任务与成长，而非仅保留内存");
            await Visit("gate",new(866,294)); Choose("出城"); await Frames(7);
            arena=(Arena)GetTree().CurrentScene; DisableAi(arena);
            arena.Player.ReceiveDamage(new DamageInfo(null,99999,Vector2.Zero)); ReturnButton(arena); await Frames(7);
            Check(City.Player.Health.CurrentHealth==348 && Session.Progress.HasBook,"战败返回驿站附近，保留成长与任务并恢复四分之一生命");
            await Visit("inn",new(169,270)); Choose("歇息"); Check(City.Player.Health.CurrentHealth==1390,"战败后驿站可以完整恢复并继续出城");
            GD.Print($"FIRST_CITY_TEST_PASS: {_checks} 项；真实渲染 {DisplayServer.GetName()!="headless"}");
            GetTree().Quit();
        }
        catch(Exception error) { GD.PushError("FIRST_CITY_TEST_FAIL: "+error); GetTree().Quit(1); }
    }
    private async Task Visit(string id,Vector2 position)
    {
        City.Dialogue.Close(); City.Player.Position=position; City.Player.SetMoveInput(Vector2.Zero); await Frames(4);
        Check(City.FindNearest()?.ContentId==id,"可接近并选中交互："+id); PressKey(City,Key.G); await Frames(1);
    }
    private static void PressKey(Node node,Key key)
    { using var input=new InputEventKey { Keycode=key,PhysicalKeycode=key,Pressed=true }; node._UnhandledInput(input); }
    private void Choose(string prefix)
    {
        var button=Buttons(City.Dialogue).First(b=>b.Text.StartsWith(prefix)); button.EmitSignal(BaseButton.SignalName.Pressed);
    }
    private System.Collections.Generic.IEnumerable<Button> Buttons(Node root)
    {
        foreach(var child in root.GetChildren()) { if(child is Button b) yield return b; foreach(var nested in Buttons(child)) yield return nested; }
    }
    private void ReturnButton(Arena arena) => Buttons(arena.GetNode("HUD")).First(b=>b.Text=="返回槐安").EmitSignal(BaseButton.SignalName.Pressed);
    private static void DisableAi(Arena arena)
    { foreach(var actor in arena.Fighters) { actor.GetNode(NodeNames.Controller).ProcessMode=ProcessModeEnum.Disabled; actor.SetMoveInput(Vector2.Zero); } }
    private async Task Move(string action,int frames) { Input.ActionPress(action); await Frames(frames); Input.ActionRelease(action); await Frames(2); }
    private async Task Frames(int count)
    { for(int i=0;i<count;i++) { await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); } }
    private async Task Capture(string name)
    {
        if(DisplayServer.GetName()=="headless") return;
        DirAccess.MakeDirRecursiveAbsolute("res://Build/first-city");
        // 测试会瞬移到交互点；截图前结束镜头插值，避免捕获上一地点。
        City.Camera.ResetSmoothing();
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage(); image.SavePng("res://Build/first-city/"+name+".png");
    }
    private void Check(bool condition,string text)
    { if(!condition) throw new InvalidOperationException(text); _checks++; GD.Print("通过："+text); }
}
