using Godot;
using System;
using System.Threading.Tasks;

/// <summary>真实图形回归：逐个隐藏手脚与原帧比较，验证部件确实贡献屏幕像素，而非仅存在节点。</summary>
public partial class RigVisualTests : Node
{
    private Arena _arena;
    private int _checks;

    public override void _Ready()
    {
        ProcessMode=ProcessModeEnum.Always;
        CallDeferred(MethodName.Run);
    }

    private async void Run()
    {
        try
        {
            if(DisplayServer.GetName()=="headless") throw new InvalidOperationException("手脚可见性检查需要图形渲染器。");
            _arena=(Arena)GetTree().CurrentScene;
            // 冻结场景用于像素比对时，不让暂停面板盖住待测角色。
            _arena.GetNode(NodeNames.Hud).ProcessMode=ProcessModeEnum.Disabled;
            _arena.Resolver.PassivesEnabled=false;
            foreach(var actor in Actors()) actor.GetNode(NodeNames.Controller).ProcessMode=ProcessModeEnum.Disabled;
            await Frames(20);
            await Capture("limbs-idle-fixed");
            await VerifyVisibleLimbs("待机");
            await Capture("limbs-idle-fixed");

            foreach(var actor in Actors()) actor.SetMoveInput(actor.IsEnemy ? Vector2.Left : Vector2.Right);
            await Frames(8);
            await VerifyVisibleLimbs("迈步");
            await Capture("limbs-walk-fixed");
            foreach(var actor in Actors()) actor.SetMoveInput(Vector2.Zero);
            await Frames(15);
            foreach(var actor in Actors()) actor.TryAction(CombatAction.Sweep);
            await Frames(18);
            await Capture("limbs-windup-fixed");
            await Frames(12);
            await Capture("limbs-swing-fixed");
            await Frames(70);

            _arena.Player.ReceiveDamage(new DamageInfo(_arena.Enemy,1,Vector2.Left));
            _arena.Enemy.ReceiveDamage(new DamageInfo(_arena.Player,1,Vector2.Right));
            await Frames(7);
            await VerifyVisibleLimbs("受击");
            await Capture("limbs-hurt-fixed");
            await Frames(25);

            _arena.Enemy.PlayCounterSpin(68,.5f);
            await Frames(10);
            await Capture("limbs-counter-fixed");
            await Frames(35);

            foreach(var actor in Actors()) actor.Face(actor.IsEnemy ? Vector2.Right : Vector2.Left);
            await Frames(10);
            await VerifyVisibleLimbs("镜像朝向");

            // 仅诊断图临时放大，便于肉眼逐一辨认手、肘、脚，不修改游戏配置。
            _arena.Player.Face(Vector2.Right); _arena.Enemy.Face(Vector2.Left);
            foreach(var actor in Actors()) actor.Rig.ArtScale*=2;
            await Frames(10);
            await Capture("limbs-detail-fixed");
            GD.Print($"RIG_VISUAL_TEST_PASS: {_checks} 项可见性和双手握点检查，截图在 Build/limbs-*-fixed.png。");
            GetTree().Quit();
        }
        catch(Exception ex)
        {
            GD.PushError($"RIG_VISUAL_TEST_FAIL: {ex}");
            GetTree().Paused=false;
            GetTree().Quit(1);
        }
    }

    private Combatant[] Actors() => new[]{_arena.Player,_arena.Enemy};

    /// <summary>冻结动画后对比隐藏单个部件前后的真实帧，捕获地图遮挡、握点重叠和空图问题。</summary>
    private async Task VerifyVisibleLimbs(string pose)
    {
        GetTree().Paused=true;
        using var visible=await ReadFrame();
        byte[] before=visible.GetData();
        foreach(var actor in Actors())
        {
            foreach(int index in new[]{5,8,10,12})
            {
                var part=actor.Rig.Parts[index];
                part.Visible=false;
                using var hidden=await ReadFrame();
                part.Visible=true;
                byte[] after=hidden.GetData();
                int changed=0;
                for(int p=0;p<before.Length;p+=4)
                    if(Math.Abs(before[p]-after[p])+Math.Abs(before[p+1]-after[p+1])+Math.Abs(before[p+2]-after[p+2])>12) changed++;
                string label=$"{actor.Name} {pose} {part.Name}：{changed} 个可见像素";
                GD.Print(label);
                if(changed<12)
                {
                    visible.SavePng("res://Build/limbs-failed-visible.png");
                    hidden.SavePng("res://Build/limbs-failed-hidden.png");
                    GD.Print($"手骨：{actor.Rig.Joints[5].GlobalPosition} / {actor.Rig.Joints[8].GlobalPosition}；部件：{part.GlobalTransform}；可见：{part.IsVisibleInTree()}");
                    GetTree().Paused=false;
                    foreach(var unit in Actors()) unit.Rig.ArtScale*=2;
                    await Frames(3);
                    await Capture("limbs-failed-detail");
                    throw new InvalidOperationException(label+"，部件仍被遮挡。");
                }
                _checks++;
                await ReadAndDisposeFrame();
            }
            var rig=actor.Rig;
            float hands=rig.Joints[5].GlobalPosition.DistanceTo(rig.Joints[8].GlobalPosition);
            if(hands<5) throw new InvalidOperationException($"{actor.Name} {pose}双手握点过近：{hands}");
            _checks++;
        }
        GetTree().Paused=false;
    }

    private async Task<Godot.Image> ReadFrame()
    {
        for(int i=0;i<2;i++) await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        var image=GetViewport().GetTexture().GetImage();
        image.Convert(Godot.Image.Format.Rgba8);
        return image;
    }

    private async Task ReadAndDisposeFrame() { using var image=await ReadFrame(); }

    private async Task Capture(string name)
    {
        GetTree().Paused=true;
        using var image=await ReadFrame();
        Error result=image.SavePng($"res://Build/{name}.png");
        if(result!=Error.Ok) throw new InvalidOperationException($"截图失败：{result}");
        GetTree().Paused=false;
    }

    private async Task Frames(int count)
    {
        for(int i=0;i<count;i++) await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
    }
}
