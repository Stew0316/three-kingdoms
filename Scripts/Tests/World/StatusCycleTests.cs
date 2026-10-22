using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>真实输入、跨图和昼夜边界回归；独立文件与测试进度不触碰玩家存档。</summary>
public partial class StatusCycleTests : Node
{
    private int _checks;
    private AdventureSession Session=>AdventureSession.Instance;
    private FirstCity City=>(FirstCity)GetTree().CurrentScene;
    public override void _Ready() { ProcessMode=ProcessModeEnum.Always; CallDeferred(MethodName.Run); }
    private async void Run()
    {
        try
        {
            await Frames(10);
            DirAccess.MakeDirRecursiveAbsolute("res://Build/status-cycle");
            if(OS.GetCmdlineUserArgs().Contains("--halo-preview"))
            {
                // 仅采集真实渲染，用于观察细光丝的连续性；不修改正式存档或时长配置。
                foreach(var phase in new[]{(3.0,"Day"),(9.0,"Night")})
                {
                    Session.Clock.SetPosition(phase.Item1);
                    string folder="res://Build/DayNightHalo/"+phase.Item2;
                    DirAccess.MakeDirRecursiveAbsolute(folder);
                    for(int frame=0;frame<36;frame++)
                    {
                        await Frames(8);
                        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                        using var screenshot=GetViewport().GetTexture().GetImage();
                        int scale=screenshot.GetWidth()/640;
                        using var crop=screenshot.GetRegion(new Rect2I(260*scale,0,120*scale,72*scale));
                        crop.SavePng($"{folder}/{frame:000}.png");
                    }
                }
                GD.Print("HALO_PREVIEW_CAPTURED: 日光与月光各 36 帧实际渲染"); GetTree().Quit(); return;
            }
            Check(Session.Settings.DayNightSecondsPerSegment==300,"全局每格默认五分钟");
            var clock=new DayNightClock(Session.Settings); clock.SetPosition(0); clock.Advance(299);
            Check(clock.Segment==0,"不足五分钟不越格"); clock.Advance(1);
            Check(clock.Segment==1,"五分钟推进一格"); clock.Advance(1500);
            Check(clock.Segment==6 && !clock.IsDay,"六格日光后进入月光"); clock.Advance(1800);
            Check(clock.Segment==0 && clock.IsDay,"完整十二格循环");
            clock.SetPosition(5.99999); var dusk=clock.Ambient; clock.Advance(.006);
            Check(Math.Abs(dusk.R-clock.Ambient.R)+Math.Abs(dusk.G-clock.Ambient.G)+Math.Abs(dusk.B-clock.Ambient.B)<.001f,"日夜交界平滑无跳色");
            var menu=City.Navigation; var bar=menu.StatusBar;
            Check(bar.Skills.Length==4 && bar.Items.Length==4,"四技能槽和四物品槽");
            Check(bar.Skills[3].Text=="" && bar.Skills[3].Disabled && !bar.Skills[3].GetNode<TextureRect>("SkillIcon").Visible,"未装备第四槽只留快捷键");
            Check(menu.Minimap.Circular && menu.Minimap.Size==new Vector2(58,58) && menu.Minimap.Position.X==10,"小地图为底部最左的小圆形");
            Check(bar.Skills[1].Position.X==bar.Skills[0].Position.X+bar.Skills[0].Size.X
                && bar.Items[1].Position.X==bar.Items[0].Position.X+bar.Items[0].Size.X
                && bar.Skills[0].Size==new Vector2(30,30),"技能与物品槽缩小且相邻无间隙");
            var sword=bar.GetNode<VerticalResourceGauge>("HealthSword");
            var gourd=bar.GetNode<VerticalResourceGauge>("ManaGourd");
            City.Player.Health.ApplyDamage(new DamageInfo(null,City.Player.Health.MaxHealth/2,Vector2.Zero)); await Frames(2);
            Check(Math.Abs(sword.Ratio-.5f)<.01f && sword.Size.Y>sword.Size.X && gourd.IsMana && gourd.Size.Y>gourd.Size.X,"竖剑按真实半血填充，右侧为竖葫芦");
            await Capture("00-half-health"); City.Player.Health.Heal(City.Player.Health.MaxHealth); await Frames(2);
            Check(Enumerable.Range(0,3).All(i=>bar.Skills[i].GetNode<TextureRect>("SkillIcon").Texture!=null
                && bar.Skills[i].GetNode<TextureRect>("SkillIcon").Visible && bar.Skills[i].Text==""),"已装备三技能实际显示图标");
            var originalIconSkill=City.Player.GetSkill(CombatAction.Dash);
            var alternateIconSkill=(CombatActionConfig)originalIconSkill.Duplicate();
            alternateIconSkill.SkillId="test:icon-swap";
            alternateIconSkill.Icon=City.Player.GetSkill(CombatAction.Sweep).Icon;
            Check(City.Player.TryEquipSkill(CombatAction.Dash,alternateIconSkill),"可替换带不同图标的技能");
            await Frames(2);
            Check(bar.Skills[0].GetNode<TextureRect>("SkillIcon").Texture==alternateIconSkill.Icon,"换装立即刷新实际图标");
            Check(City.Player.TryEquipSkill(CombatAction.Dash,originalIconSkill),"图标验证后恢复原技能");
            await Frames(2);
            Check(!bar.UseItem(0),"没有火把时不能使用"); Session.Progress.ReceiveTorch();
            KeyPress(Key.Key1); await Frames(3);
            Check(Session.TorchInUse && Session.Progress.Items[AdventureProgress.TorchId]==1,"数字一使用火把且不消耗库存");
            Check(bar.Items[0].GetNode<TextureRect>("ItemIcon").Texture!=null && bar.Items[0].Text==""
                && bar.Items[0].GetNode<Label>("Quantity").Text=="1" && bar.Items[0].GetNode<ColorRect>("InUse").Visible,"火把图标、库存与使用标记真实显示");
            await Capture("00-toolbar-ready");
            Session.Progress.Items[AdventureProgress.TorchId]=3; await Frames(2);
            Check(bar.Items[0].GetNode<Label>("Quantity").Text=="3","物品数量跟随库存更新");
            Session.Progress.Items[AdventureProgress.TorchId]=1;
            Check(Session.Bindings.TrySet(InputActions.ItemSlots[0],Key.T,out _),"可修改物品快捷键"); await Frames(2);
            KeyPress(Key.T); await Frames(2);
            Check(!Session.TorchInUse && bar.Items[0].GetNode<Label>("Shortcut").Text=="T","物品图标键帽及实际输入跟随配置");
            Session.Bindings.TrySet(InputActions.ItemSlots[0],Key.Key1,out _);
            KeyPress(Key.Key1); await Frames(2);
            KeyPress(Key.Key1); await Frames(2); Check(!Session.TorchInUse,"重复快捷键收起火把");
            KeyPress(Key.Q); await Frames(2);
            Check(City.Player.Cooldown(CombatAction.Dash)>0,"Q 实际施放第一技能");
            Check(bar.Skills[0].Disabled,"冷却槽禁止重复使用");
            Check(bar.Skills[0].GetNode<TextureRect>("SkillIcon").Visible,"冷却时保留技能图标");
            await Capture("01-city-cooldown");
            menu.Open("设置"); await Frames(3); double position=Session.Clock.Position;
            await Frames(8); Check(Session.Clock.Position==position,"打开菜单冻结全局时钟");
            Check(!bar.UseItem(0),"菜单暂停时不能使用快捷物品");
            var buttons=Buttons(menu).Where(b=>b.IsVisibleInTree()).ToArray();
            var up=buttons.First(b=>b.Text=="W"); var left=buttons.First(b=>b.Text=="A");
            Check(up.Position.Y+up.Size.Y<=left.Position.Y,"设置按键按钮无纵向重叠");
            float pausedCooldown=City.Player.Cooldown(CombatAction.Dash); await Frames(3);
            Check(City.Player.Cooldown(CombatAction.Dash)==pausedCooldown,"菜单暂停时技能冷却也冻结");
            buttons.First(b=>b.Text=="Q").EmitSignal(BaseButton.SignalName.Pressed);
            KeyPress(Key.Escape); await Frames(2);
            Check(menu.IsOpen && menu.Page=="设置","Esc 取消改键且保留设置页面");
            buttons.First(b=>b.Text=="Q").EmitSignal(BaseButton.SignalName.Pressed);
            KeyPress(Key.W); await Frames(2);
            Check(Session.Bindings.Label(InputActions.Skill1)=="Q","冲突按键不覆盖既有映射");
            KeyPress(Key.T); await Frames(2);
            Check(Session.Bindings.Label(InputActions.Skill1)=="T","设置页面实际捕获并应用新键");
            Check(bar.Skills[0].GetNode<Label>("Shortcut").Text=="T","图标快捷键随设置更新");
            await Capture("02-settings");
            menu.Close(); await Frames(70);
            City.Player.Skills.Tick(100); KeyPress(Key.Q); await Frames(2);
            Check(City.Player.Cooldown(CombatAction.Dash)==0,"原按键不再触发技能");
            KeyPress(Key.T); await Frames(2);
            Check(City.Player.Cooldown(CombatAction.Dash)>0,"新按键实际触发技能");
            menu.Open("退出确认"); await Frames(2); await Capture("03-quit");
            Buttons(menu).First(b=>b.Text=="取消" && b.IsVisibleInTree()).EmitSignal(BaseButton.SignalName.Pressed);
            Check(menu.IsOpen && menu.Page=="" && GetTree().Paused,"取消退出返回菜单并维持暂停");
            menu.ShowPage("设置"); Buttons(menu).First(b=>b.Text=="恢复默认按键").EmitSignal(BaseButton.SignalName.Pressed);
            Check(Session.Bindings.Label(InputActions.Skill1)=="Q","恢复默认立即生效"); menu.Close();
            const string path="res://Build/status-cycle/bindings.cfg";
            var saved=new KeyBindings(false,path); saved.Reset();
            Check(saved.TrySet(InputActions.MoveUp,Key.T,out _),"可修改并保存移动键");
            var loaded=new KeyBindings(false,path);
            Check(loaded.Label(InputActions.MoveUp)=="T","重建映射可恢复已保存设置"); loaded.Reset();
            City.Player.Position=new(370,290); City.Player.StopMoving();
            Check(Session.Bindings.TrySet(InputActions.MoveRight,Key.Y,out _),"可重新映射向右移动");
            KeyDown(Key.Y,true); await Frames(12); KeyDown(Key.Y,false);
            Check(City.Player.Position.X>370,"重映射后移动实际生效"); Session.Bindings.Reset();
            Session.Clock.SetPosition(3); await Frames(3); await Capture("04-day");
            Session.Clock.SetPosition(6); await Frames(3); await Capture("05-dusk");
            Session.Clock.SetPosition(9); await Frames(3); await Capture("06-night");
            Check(City.Navigation.Daylight.Color.B>City.Navigation.Daylight.Color.R,"夜间实际应用蓝灰月光");
            double before=Session.Clock.Position;
            Session.EnterCave(GetTree(),City.Player); await Frames(12);
            var cave=(OldRoadCave)GetTree().CurrentScene;
            Check(Session.Clock.Position>=before && Session.Clock.Position<before+.1,"切换洞窟不重置时钟");
            Check(cave.Navigation.Daylight==null && cave.Darkness.Color.R<.15,"洞窟保留自身暗度与局部照明");
            await Capture("07-cave");
            Session.EnterTrial(GetTree()); await Frames(12); var arena=(Arena)GetTree().CurrentScene;
            foreach(var actor in arena.Fighters) actor.GetNode(NodeNames.Controller).ProcessMode=ProcessModeEnum.Disabled;
            arena.Player.GetNode(NodeNames.Controller).ProcessMode=ProcessModeEnum.Inherit;
            var third=(CombatActionConfig)arena.Player.GetSkill(CombatAction.Sweep).Duplicate(); third.SkillId="test:third"; third.DisplayName="第三招";
            Check(arena.Player.TryEquipSkill(CombatAction.Skill3,third),"第三槽接受独立技能装备");
            var fourth=(CombatActionConfig)third.Duplicate(); fourth.SkillId="test:fourth"; fourth.DisplayName="第四招";
            Check(arena.Player.TryEquipSkill(CombatAction.Skill4,fourth),"第四槽接受独立技能装备");
            KeyDown(Key.R,true); await Frames(4); KeyDown(Key.R,false);
            Check(ReferenceEquals(arena,GetTree().CurrentScene) && arena.Player.Cooldown(CombatAction.Skill3)>0,"R 施放第三槽而非重开场景");
            await Frames(75); KeyDown(Key.F,true); await Frames(4); KeyDown(Key.F,false);
            Check(arena.Player.Cooldown(CombatAction.Skill4)>0,"F 实际施放第四槽");
            Session.Clock.SetPosition(3); await Frames(2); await Capture("08-arena");
            await Frames(75);
            bool torchBefore=Session.TorchInUse;
            var item=arena.Navigation.StatusBar.Items[0];
            // ParseInputEvent 接收窗口像素；先应用画布和窗口拉伸变换。
            Vector2 pointer=GetViewport().GetFinalTransform()*item.GetGlobalTransformWithCanvas()*(item.Size*.5f);
            using(var motion=new InputEventMouseMotion { Position=pointer,GlobalPosition=pointer }) Input.ParseInputEvent(motion);
            await Frames(2);
            using(var click=new InputEventMouseButton { Position=pointer,GlobalPosition=pointer,ButtonIndex=MouseButton.Left,Pressed=true }) Input.ParseInputEvent(click);
            await Frames(3);
            using(var release=new InputEventMouseButton { Position=pointer,GlobalPosition=pointer,ButtonIndex=MouseButton.Left,Pressed=false }) Input.ParseInputEvent(release);
            await Frames(3);
            Check(Session.TorchInUse!=torchBefore && arena.Player.Cooldown(CombatAction.Basic)==0,"真实点击快捷物品生效且不会误触普攻");
            arena.Navigation.Open("设置"); await Frames(2); await Capture("09-arena-settings"); arena.Navigation.Close();
            if(OS.GetCmdlineUserArgs().Contains("--status-quit-test"))
            {
                arena.Navigation.Open("退出确认");
                GD.Print($"STATUS_CYCLE_TEST_PASS: {_checks} 项；即将通过确认按钮退出进程");
                Buttons(arena.Navigation).First(b=>b.Text=="确认退出").EmitSignal(BaseButton.SignalName.Pressed); return;
            }
            GD.Print($"STATUS_CYCLE_TEST_PASS: {_checks} 项；真实渲染 {DisplayServer.GetName()!="headless"}"); GetTree().Quit();
        }
        catch(Exception error) { GD.PushError("STATUS_CYCLE_TEST_FAIL: "+error); GetTree().Paused=false; GetTree().Quit(1); }
    }
    private static IEnumerable<Button> Buttons(Node node)
    { foreach(var child in node.GetChildren()) { if(child is Button b) yield return b; foreach(var next in Buttons(child)) yield return next; } }
    private static void KeyDown(Key key,bool down)
    { using var input=new InputEventKey { Keycode=key,PhysicalKeycode=key,Pressed=down }; Input.ParseInputEvent(input); }
    private static void KeyPress(Key key) { KeyDown(key,true); KeyDown(key,false); }
    private async Task Frames(int count) { for(int i=0;i<count;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    private async Task Capture(string name)
    {
        if(DisplayServer.GetName()=="headless") return;
        // 截图前移出提示控件，避免系统鼠标的初始位置弹出悬浮说明遮住底栏。
        using(var motion=new InputEventMouseMotion { Position=new(4,4),GlobalPosition=new(4,4) }) Input.ParseInputEvent(motion);
        await Frames(2);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage(); image.SavePng("res://Build/status-cycle/"+name+".png");
    }
    private void Check(bool condition,string description)
    { if(!condition) throw new InvalidOperationException(description); _checks++; GD.Print("通过："+description); }
}
