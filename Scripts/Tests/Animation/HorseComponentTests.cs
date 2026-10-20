using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>直接运行组件场景的集成检查，不创建FirstCity或Combatant。</summary>
public partial class HorseComponentTests : Node
{
    private int _checks;
    private sealed class MotionSource : IHorseMotionSource { public HorseMotionSample HorseMotion { get; set; } }
    public override void _Ready() => CallDeferred(MethodName.Run);
    private async void Run()
    {
        try
        {
            if(!OS.GetCmdlineUserArgs().Contains("--horse-component-test")) throw new InvalidOperationException("需要 --horse-component-test 隔离存档参数");
            await VerifySceneLifetime();
            var brown=HorseActor.Create(); brown.Position=new(150,180); AddChild(brown);
            var grey=GD.Load<PackedScene>("res://Components/Mounts/DappleHorse.tscn").Instantiate<HorseActor>(); grey.Position=new(440,180); AddChild(grey);
            Check(brown.Animation!=null && brown.Visual!=null && brown.Attachments.Horse==brown.Visual,"主场景装配动作、外观与骑乘连接子组件");
            Check(brown.Position==new Vector2(150,180) && grey.Position==new Vector2(440,180),"无运动来源时保留场景摆放位置");
            Check(brown.PaintedRig.Bones.Count==32 && grey.PaintedRig.Bones.Count==32,"两种预设均独立建立32骨架");
            Check(!ReferenceEquals(brown.Animation,grey.Animation) && !ReferenceEquals(brown.PaintedRig,grey.PaintedRig),"两匹马不共享动作状态或骨骼实例");
            var greyTexture=grey.PaintedRig.BodySkin.Texture;
            brown.SetPreviewPose(.5f,1,-1);
            Check(brown.RenderGaitPhase==.5f && brown.Facing==-1 && grey.GaitPhase==0,"单马改相位和镜像不影响另一实例");
            brown.Hide(); brown.SetPreviewPose(.75f,1); brown.Show(); brown.SetPreviewPose(.75f,1);
            Check(brown.PaintedRig.UpperMuscleEnd(true).DistanceTo(HorseLocomotion.Sample(true,true,.75f,1,brown.Appearance.BodyWidth).Proximal)<.0001f,"隐藏再显示同步最新肩胯姿态");
            var brownAppearance=brown.Appearance;
            brown.Appearance=grey.Appearance; brown.SetPreviewPose(.25f,1);
            Check(brown.PaintedRig.BodySkin.Texture==greyTexture && brown.Visual.GetChildCount()==1,"换外观替换唯一骨架，没有叠加旧马皮肤");
            brown.Appearance=null; brown.SetPreviewPose(0,0);
            Check(brown.PaintedRig==null,"移除外观清理旧显示，不保留残影");
            brown.Appearance=brownAppearance; brown.SetPreviewPose(0,0);
            Check(brown.PaintedRig!=null,"重新设置外观可恢复组件");

            var source=new MotionSource {HorseMotion=new(new(215,170),0,Vector2.Zero,Vector2.Right)};
            var parent=new Node2D {Position=new(17,23),Scale=new(1.25f,1.25f)}; AddChild(parent);
            var following=HorseActor.Create(); following.MotionSource=source; parent.AddChild(following);
            Check(following.GlobalPosition.DistanceTo(source.HorseMotion.WorldPosition)<.0001f,"通用运动来源可挂在有位移缩放的父节点下");
            following.SetProcess(false); following.SetPhysicsProcess(false);
            source.HorseMotion=source.HorseMotion with {WorldPosition=new(715,170)};
            following._PhysicsProcess(1d/60); following._Process(1d/60);
            Check(following.GlobalPosition.DistanceTo(new(715,170))<.0001f && following.GaitPhase==0,"宿主传送立即跟随，不制造额外步长");
            following.MotionSource=null; following.ResetGait();
            Check(following.GlobalPosition.DistanceTo(new(715,170))<.0001f,"解除运动来源后保留马的摆放位置");
            VerifyClock();
            brown.QueueFree(); grey.QueueFree(); parent.QueueFree();
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);

            var rig=GD.Load<PackedScene>("res://Components/Mounts/HorseRig.tscn").Instantiate<HorsePaintedRig>(); AddChild(rig);
            rig.ApplyPose(.5f,1);
            Check(rig.Bones.Count==32 && rig.TackSkin!=null,"纯骨架场景可独立采样全身动画与鞍具");
            rig.QueueFree();
            var demo=GD.Load<PackedScene>("res://Scene/Examples/HorseComponentDemo.tscn").Instantiate<HorseComponentDemo>(); AddChild(demo);
            var demos=demo.GetChildren().OfType<HorseActor>().ToArray();
            for(int i=0;i<30;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Check(demos.Length==4 && demos.All(h=>h.GaitWeight==1) && demos[0].GaitPhase!=demos[1].GaitPhase,"独立预览四匹马按各自190/250速度运行");
            demo._UnhandledKeyInput(new InputEventKey {Pressed=true,Keycode=Key.R});
            Check(demos.All(h=>!h.AutoPlayPreview && h.GaitWeight==0),"预览站立操作立即收回中立姿态");
            demo._UnhandledKeyInput(new InputEventKey {Pressed=true,Keycode=Key.Left});
            Check(demos.All(h=>h.Facing==-1),"预览左右朝向操作可单独驱动组件");
            demo._UnhandledKeyInput(new InputEventKey {Pressed=true,Keycode=Key.Right});
            demo._UnhandledKeyInput(new InputEventKey {Pressed=true,Keycode=Key.Space});
            for(int i=0;i<12;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(DisplayServer.GetName()!="headless")
            {
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                DirAccess.MakeDirRecursiveAbsolute("res://Build/horse-components");
                using var image=GetViewport().GetTexture().GetImage();
                Check(image.SavePng("res://Build/horse-components/standalone-demo.png")==Error.Ok,"保存独立场景实际渲染证据");
            }
            GD.Print($"HORSE_COMPONENT_TEST_PASS: {_checks} 项"); GetTree().Quit();
        }
        catch(Exception error) { GD.PushError("HORSE_COMPONENT_TEST_FAIL: "+error); GetTree().Quit(1); }
    }
    private void VerifyClock()
    {
        var clock=new HorseAnimationController(); AddChild(clock);
        var start=new HorseMotionSample(new(200,100),100,Vector2.Zero,Vector2.Right);
        clock.ResetMotion(start);
        var moved=start with {WorldPosition=new(204,100),Distance=104,Velocity=new(240,0)};
        clock.CapturePhysics(moved); clock.AdvanceMotion(moved,1f/60,.5f);
        Check(Mathf.Abs(clock.GaitPhase-4f/128)<.00001f && Mathf.Abs(clock.RenderGaitPhase-2f/128)<.00001f,"逻辑相位按真实路程，显示相位按半物理帧插值");
        Check(clock.RenderedWorldPosition==new Vector2(202,100),"显示位置与相位使用同一插值比例");
        float phase=clock.GaitPhase;
        var stopped=moved with {Velocity=Vector2.Zero};
        clock.CapturePhysics(stopped); clock.AdvanceMotion(stopped,.2f,1);
        Check(clock.GaitWeight==0 && clock.GaitPhase==phase,"停止只衰减动作权重，不凭空推进步态");
        var reset=stopped with {Distance=0,WorldPosition=new(20,30)};
        clock.CapturePhysics(reset); clock.AdvanceMotion(reset,1f/60,.5f);
        Check(clock.GaitPhase==0 && clock.RenderGaitPhase==0 && clock.RenderedWorldPosition==new Vector2(20,30),"复用时重置累计路程不留下反向相位或旧位置插值");
        clock.QueueFree();
    }
    private async Task VerifySceneLifetime()
    {
        // 强制回收只用于检查反复切图的资源生命周期，正式组件不主动触发GC。
        for(int round=0;round<6;round++)
        {
            CreateThenReleaseHorse();
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        }
        var restored=HorseActor.Create(); AddChild(restored);
        Check(restored.PaintedRig.Bones.Count==32 && restored.Appearance.PartsAtlas!=null,"反复释放与垃圾回收后仍能重新装配完整马组件");
        restored.QueueFree();
    }
    private void CreateThenReleaseHorse()
    {
        // 直接加载场景，避免工厂提前持有外观而掩盖编辑器拖入场景时的回收问题。
        foreach(string path in new[]{"res://Components/Mounts/ChestnutHorse.tscn","res://Components/Mounts/DappleHorse.tscn"})
        {
            var horse=GD.Load<PackedScene>(path).Instantiate<HorseActor>(); AddChild(horse);
            horse.SetPreviewPose(.25f,1); horse.QueueFree();
        }
    }
    private void Check(bool value,string description)
    {
        if(!value) throw new InvalidOperationException(description);
        _checks++; GD.Print("通过："+description);
    }
}
