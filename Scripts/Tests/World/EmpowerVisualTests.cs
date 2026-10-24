using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>真实图形验收：默认 R 爆气、红色奋起、暂停冻结和到期清理；由隔离状态回归入口启动。</summary>
public partial class EmpowerVisualTests : Node
{
    private const string Folder = "res://Build/EmpowerVisual";
    private int _checks;
    private readonly List<float> _frameTimes = new();
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        CallDeferred(MethodName.Run);
    }
    private async void Run()
    {
        try
        {
            if (DisplayServer.GetName() == "headless") throw new InvalidOperationException("此验收需要真实图形渲染。");
            DirAccess.MakeDirRecursiveAbsolute(Folder);
            var city = (FirstCity)GetTree().CurrentScene;
            var player = city.Player;
            player.Position = new(340, 300);
            city.Camera.PositionSmoothingEnabled = false;
            AdventureSession.Instance.Clock.SetPosition(3);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = new(5, 100), GlobalPosition = new(5, 100) });
            var red = GD.Load<PackedScene>(WorldScenePaths.Combatant).Instantiate<Combatant>();
            HeroCatalog.Configure(red, 2);
            red.Name = "RedAuraSubject";
            red.Position = new(440, 300);
            city.Map.Actors.AddChild(red);
            red.GetNode<Node2D>(NodeNames.GroundEffects).Hide();
            await Frames(8);
            var goldFx = player.GetNode<EmpowerEffects2D>("EmpowerEffects2D");
            var redFx = red.GetNode<EmpowerEffects2D>("EmpowerEffects2D");
            Check(!goldFx.AuraVisible && !redFx.AuraVisible, "未强化时无全身气焰");
            await Capture("FullHealth");
            player.Health.ApplyDamage(new DamageInfo(null, player.Health.MaxHealth / 2, Vector2.Zero));
            await Frames(3); await Capture("HalfHealth");
            player.Health.Heal(player.Health.MaxHealth);
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.R, Keycode = Key.R, Pressed = true });
            await Frames(1);
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.R, Keycode = Key.R, Pressed = false });
            Check(red.TryAction(CombatAction.Skill3), "关羽真实施放红色奋起");
            await Frames(20);
            Check(player.Status.EmpowerRemaining > 0 && goldFx.AuraVisible, "R 输入实际触发金色爆气");
            Check(red.Status.EmpowerRemaining > 0 && redFx.AuraVisible, "红色奋起包裹全身");
            var goldBack = player.GetNode<ColorRect>("AuraBack");
            var redBack = red.GetNode<ColorRect>("AuraBack");
            Check(goldBack.Position.Y < player.ToLocal(player.Rig.Joints[2].GlobalPosition).Y
                && goldBack.Position.Y + goldBack.Size.Y >= 0, "金焰从脚点覆盖到头顶上方");
            Check(redBack.Position.Y < red.ToLocal(red.Rig.Joints[2].GlobalPosition).Y
                && redBack.Position.Y + redBack.Size.Y >= 0, "红焰从脚点覆盖到头顶上方");
            Check(goldBack.GetIndex() < player.Rig.GetIndex(), "背层位于人物后面且仍在地图之上");
            Check(((ShaderMaterial)goldBack.Material).Shader.Code.Contains("unshaded")
                && ((ShaderMaterial)redBack.Material).Shader.Code.Contains("unshaded"),"两种气焰保持自发光材质");
            Check(player.GetNode<WeaponTrail>("WeaponTrail").Material is CanvasItemMaterial trail
                && trail.LightMode==CanvasItemMaterial.LightModeEnum.Unshaded,"技能刀光不受昼夜压暗");
            // 冻结同一姿态和气流时刻，只改变环境，用实机截图对照人物压暗、气焰保持亮度。
            GetTree().Paused=true;
            AdventureSession.Instance.Clock.SetPosition(3); await Frames(2); await Capture("LightingDay");
            AdventureSession.Instance.Clock.SetPosition(6); await Frames(2); await Capture("LightingDusk");
            AdventureSession.Instance.Clock.SetPosition(9); await Frames(2); await Capture("LightingNight");
            AdventureSession.Instance.Clock.SetPosition(3); GetTree().Paused=false;
            for (int frame = 0; frame < 40; frame++)
            {
                await Frames(4);
                await Capture($"Aura{frame:000}");
                _frameTimes.Add(player.VisualTime);
            }
            using (var timings = FileAccess.Open(Folder + "/FrameTimes.json", FileAccess.ModeFlags.Write))
                timings.StoreString(System.Text.Json.JsonSerializer.Serialize(_frameTimes));
            float time = goldFx.SampleTime, remaining = player.Status.EmpowerRemaining;
            city.Navigation.Open(); await Frames(10);
            Check(goldFx.SampleTime == time && player.Status.EmpowerRemaining == remaining, "菜单暂停冻结气流和状态时钟");
            city.Navigation.Close();
            await Frames(210);
            Check(!goldFx.AuraVisible && !redFx.AuraVisible, "状态到期后两种气焰全部消失");
            await Capture("Expired");
            GD.Print($"EMPOWER_VISUAL_PASS: {_checks} 项，40 帧金黄/赤红全身气焰，满血与半血真实渲染。");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"EMPOWER_VISUAL_FAIL: {error}"); GetTree().Paused = false; GetTree().Quit(1);
        }
    }
    private void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        _checks++; GD.Print("通过：" + message);
    }
    private async Task Frames(int count)
    {
        for (int frame = 0; frame < count; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
    private async Task Capture(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var screenshot = GetViewport().GetTexture().GetImage();
        screenshot.SavePng($"{Folder}/{name}.png");
    }
}
