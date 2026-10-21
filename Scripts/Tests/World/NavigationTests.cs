using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>全局人物、变速、菜单输入、地图与跨图回归；使用隔离存档，图形模式输出实际画面。</summary>
public partial class NavigationTests : Node
{
    private int _checks;
    private AdventureSession Session => AdventureSession.Instance;
    private FirstCity City => (FirstCity)GetTree().CurrentScene;
    public override void _Ready() { ProcessMode=ProcessModeEnum.Always; CallDeferred(MethodName.Run); }
    private async void Run()
    {
        try
        {
            await Frames(8);
            Check(City.Navigation.Minimap.Visible && City.Navigation.Minimap.Position.X==10,"左下角小地图常驻");
            Check(City.Camera.Zoom.X<.85f,"城市镜头拉远，人物画面占比减小");
            Check(ReferenceEquals(City.Player.Config,City.Player.Attributes.Config),"角色配置直接引用唯一人物数据");
            await Capture("01-city");
            KeyPress(Key.Escape); await Frames(3);
            Check(City.Navigation.IsOpen && GetTree().Paused && City.Navigation.Page=="","Esc 打开侧栏并暂停");
            Vector2 before=City.Player.Position;
            Input.ActionPress(InputActions.MoveRight); await Frames(8); Input.ActionRelease(InputActions.MoveRight);
            Check(City.Player.Position==before,"菜单暂停时角色不移动");
            Check(new[]{"地图","物品栏","人物","背包","坐骑"}.All(p=>Buttons(City.Navigation).Any(b=>b.Text==p)),"侧栏包含指定五项");
            await Capture("02-sidebar");
            KeyPress(Key.Down); await Frames(2); KeyPress(Key.Enter); await Frames(2);
            Check(City.Navigation.Page=="物品栏","方向键与 Enter 可选择侧栏项目");
            KeyPress(Key.M); await Frames(3);
            Check(City.Navigation.Page=="地图","M 从侧栏直接打开天下图");
            Check(Session.Atlas.Cities.Length==42,"登记参考图 42 座城池");
            var reached=new HashSet<string>{Session.Atlas.Cities[0].Id};
            for(int i=0;i<42;i++) foreach(var route in Session.Atlas.Routes)
            { if(reached.Contains(route.From)) reached.Add(route.To); if(reached.Contains(route.To)) reached.Add(route.From); }
            Check(reached.Count==42 && Session.Atlas.Routes.Any(r=>r.Kind=="water"),"陆路水路连接全部城池且无孤点");
            await Capture("03-world-map");
            City.Navigation.WorldMap.SelectCity("jiangxia",true); await Frames(3);
            Check(City.Navigation.WorldMap.Zoom>2,"选城可放大查看路线");
            await Capture("04-world-map-zoom");
            Click("本地导览"); await Frames(3);
            Check(City.Navigation.Page=="本地导览","天下图可切换本地全景导览");
            await Capture("11-local-overview"); Click("天下图"); await Frames(2);
            KeyPress(Key.M); await Frames(3);
            Check(!City.Navigation.IsOpen && !GetTree().Paused,"M 关闭地图恢复游戏");
            GetTree().Paused=true; City.Navigation.Open(); City.Navigation.Close();
            Check(GetTree().Paused,"关闭菜单保留外部原有暂停状态"); GetTree().Paused=false;
            KeyPress(Key.Escape); await Frames(2); Click("人物"); await Frames(2); Click("快行");
            Check(Math.Abs(City.Player.MoveRate-1.5f)<.001f && Math.Abs(City.Player.TargetMoveSpeed-187.5f)<.01f,"人物页修改倍率立即影响目标速度");
            Check(Session.Progress.CharacterMoveRates["core:lubu"]==1.5f,"倍率保存到同一人物 ID");
            Check(GD.Load<CombatantConfig>("res://Resources/Config/Characters/lubu/lubu.tres").MoveSpeed==145,"临时速度不污染只读武将资源");
            await Capture("05-character");
            KeyPress(Key.Escape); await Frames(2); Check(City.Navigation.Page=="" && GetTree().Paused,"Esc 从分页返回侧栏");
            KeyPress(Key.Escape); await Frames(2);
            Check(!GetTree().Paused,"再按 Esc 恢复游戏");
            City.Player.Position=new(370,290); City.Player.StopMoving();
            Input.ActionPress(InputActions.MoveRight); await PhysicsFrames(2);
            Check(City.Player.Velocity.X>0 && City.Player.Velocity.X<187.5f,"起步通过加速度逐渐达到目标速度");
            await PhysicsFrames(18);
            Check(Math.Abs(City.Player.Velocity.X-187.5f)<1,"稳定移动达到设定倍率速度");
            Input.ActionRelease(InputActions.MoveRight); await PhysicsFrames(1);
            Check(City.Player.Velocity.X<187.5f,"松键开始制动");
            await PhysicsFrames(10); Check(City.Player.Velocity.IsZeroApprox(),"制动后完全停下");
            City.Mount(2); await Frames(15);
            Check(City.Player.TargetMoveSpeed==375,"骑乘速度同样乘以人物倍率");
            Check(City.Navigation.Minimap.WorldUnitsPerPixel>3.2f,"骑乘小地图平滑扩大可视范围");
            City.Dismount();
            Session.Progress.ReceiveTorch(); Session.Progress.AcceptTrial(); Session.Progress.EncounterCleared(AdventureIds.WeiYanTrial); Session.Progress.ClaimTrial(Session.Config);
            KeyPress(Key.I); await Frames(2);
            Check(City.Navigation.Page=="背包","I 打开统一背包");
            Click("使用火把"); Check(Session.TorchInUse,"背包使用火把实际生效");
            Click("物品栏"); Check(Buttons(City.Navigation).Any(b=>b.Text=="收起火把"),"物品栏与背包共用物品状态");
            await Capture("06-items"); Click("坐骑"); await Frames(2); await Capture("07-mounts");
            City.Navigation.Close();
            City.Player.Health.ApplyDamage(new DamageInfo(null,99,Vector2.Zero));
            var attributes=City.Player.Attributes; int health=attributes.CurrentHealth;
            Check(health==City.Player.Health.CurrentHealth,"全局人物实时持有当前生命");
            DirAccess.MakeDirRecursiveAbsolute("res://Build/navigation");
            string save="res://Build/navigation/isolated-save.json";
            Check(AdventureSaveStore.Write(save,Session.Progress)==Error.Ok,"隔离存档写入成功");
            var restored=AdventureSaveStore.Read(save,Session.Config);
            Check(restored.CharacterMoveRates["core:lubu"]==1.5f && restored.HasTorch,"读档恢复倍率与物品");
            const string legacy="res://Build/navigation/legacy-save.json";
            using(var file=FileAccess.Open(legacy,FileAccess.ModeFlags.Write)) file.StoreString("{\"Version\":1,\"Coins\":77,\"HealthRatio\":0.5}");
            var old=AdventureSaveStore.Read(legacy,Session.Config);
            Check(old.Coins==77 && old.CharacterMoveRates.Count==0 && old.HealthRatio==.5f,"旧档兼容保留货币与生命，默认无倍率覆盖");
            Session.EnterCave(GetTree(),City.Player); await Frames(10);
            var cave=(OldRoadCave)GetTree().CurrentScene;
            Check(ReferenceEquals(attributes,cave.Player.Attributes) && cave.Player.Health.CurrentHealth==health,"城市到洞窟复用同一人物及生命");
            Check(cave.Player.TargetMoveSpeed==187.5f && cave.Navigation.Minimap.Visible,"洞窟保留速度倍率和常驻地图");
            KeyPress(Key.Escape); await Frames(2); Click("坐骑");
            Check(Buttons(cave.Navigation).Where(b=>b.Text=="骑乘").All(b=>b.Disabled),"洞窟不允许骑乘");
            cave.Navigation.Close(); await Capture("08-cave");
            Session.EnterTrial(GetTree()); await Frames(12);
            var arena=(Arena)GetTree().CurrentScene;
            foreach(var actor in arena.Fighters) actor.GetNode(NodeNames.Controller).ProcessMode=ProcessModeEnum.Disabled;
            Check(ReferenceEquals(attributes,arena.Player.Attributes) && arena.Player.Config.MoveSpeed==125,"进入试炼仍复用唯一属性，不偷偷切换基础移速");
            KeyPress(Key.Escape); await Frames(3); double seconds=arena.BattleSeconds;
            await Frames(6);
            Check(arena.Navigation.IsOpen && GetTree().Paused && arena.BattleSeconds==seconds,"战斗 Esc 菜单冻结战斗时间");
            await Capture("09-arena-menu");
            Input.ActionPress(InputActions.Attack);
            arena.Navigation.Close();
            arena.Player.GetNode(NodeNames.Controller).ProcessMode=ProcessModeEnum.Inherit;
            await PhysicsFrames(3);
            Check(arena.Player.CurrentState!=Combatant.State.Attack && arena.Player.SuppressActionsUntilReleased,"关闭菜单时按住的攻击输入不穿透");
            Input.ActionRelease(InputActions.Attack); await PhysicsFrames(2);
            Check(!arena.Player.SuppressActionsUntilReleased,"松键后恢复出招输入");
            arena.Player.GetNode(NodeNames.Controller).ProcessMode=ProcessModeEnum.Disabled;
            await Frames(2); await Capture("10-arena");
            arena.Player.ReceiveDamage(new DamageInfo(null,99999,Vector2.Zero));
            KeyPress(Key.F5); await Frames(12);
            arena=(Arena)GetTree().CurrentScene;
            foreach(var actor in arena.Fighters) actor.GetNode(NodeNames.Controller).ProcessMode=ProcessModeEnum.Disabled;
            Check(arena.Player.Health.CurrentHealth==health && !arena.Player.IsDead && ReferenceEquals(attributes,arena.Player.Attributes),"战败重试恢复入场生命并保留同一人物数据");
            Session.ReturnToCity(GetTree(),arena.Player); await Frames(10);
            Check(ReferenceEquals(attributes,City.Player.Attributes) && City.Player.MoveRate==1.5f,"返回城市保持数据身份及倍率");
            Session.SetMoveRate(City.Player,1);
            GD.Print($"NAVIGATION_TEST_PASS: {_checks} 项；真实渲染 {DisplayServer.GetName()!="headless"}");
            GetTree().Quit();
        }
        catch(Exception error) { GD.PushError("NAVIGATION_TEST_FAIL: "+error); GetTree().Paused=false; GetTree().Quit(1); }
    }
    private IEnumerable<Button> Buttons(Node root)
    { foreach(var child in root.GetChildren()) { if(child is Button b) yield return b; foreach(var b2 in Buttons(child)) yield return b2; } }
    private void Click(string prefix)
    {
        var root=GetTree().CurrentScene;
        Buttons(root).First(b=>b.Text.StartsWith(prefix) && b.IsVisibleInTree() && !b.Disabled).EmitSignal(BaseButton.SignalName.Pressed);
    }
    private static void KeyPress(Key key)
    {
        using var press=new InputEventKey { Keycode=key,PhysicalKeycode=key,Pressed=true };
        using var release=new InputEventKey { Keycode=key,PhysicalKeycode=key,Pressed=false };
        Input.ParseInputEvent(press); Input.ParseInputEvent(release);
    }
    private async Task Frames(int count)
    { for(int i=0;i<count;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    private async Task PhysicsFrames(int count)
    { for(int i=0;i<count;i++) { await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); } }
    private async Task Capture(string name)
    {
        if(DisplayServer.GetName()=="headless") return;
        DirAccess.MakeDirRecursiveAbsolute("res://Build/navigation");
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage(); image.SavePng("res://Build/navigation/"+name+".png");
    }
    private void Check(bool condition,string text)
    { if(!condition) throw new InvalidOperationException(text); _checks++; GD.Print("通过："+text); }
}

