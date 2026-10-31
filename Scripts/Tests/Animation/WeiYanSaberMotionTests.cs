using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>通过真实技能入口录制视频参考连招；空间骨长、握持、足底、暂停和恢复均独立验收。</summary>
public partial class WeiYanSaberMotionTests : Node
{
    private Combatant _actor;
    private int _checks;
    private const string Folder="res://Build/WeiYanSaberNoSpin";
    private bool Rendered=>DisplayServer.GetName()!="headless";
    public override void _Ready() { ProcessMode=ProcessModeEnum.Always; CallDeferred(MethodName.Run); }
    private async void Run()
    {
        try
        {
            var arena=(Arena)GetTree().CurrentScene;
            _actor=arena.Enemy;
            foreach(var actor in arena.Fighters)
            {
                actor.GetNode(NodeNames.Controller).ProcessMode=ProcessModeEnum.Disabled;
                actor.SetMoveInput(Vector2.Zero);
            }
            arena.Resolver.PassivesEnabled=false;
            arena.Player.Position=new Vector2(40,280);
            _actor.Position=new Vector2(350,250); _actor.Face(Vector2.Right);
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Folder+"/Frames"));
            ValidateClip();
            await Frames(25);
            await Record();
            Check(_actor.CurrentState==Combatant.State.Idle && !_actor.Rig.SpatialMotionActive,"连招结束恢复站姿、原始骨架绑定和贴图");
            await CheckCombatWindows(arena.Player);
            Check(_actor.TryAction(CombatAction.Basic),"普攻使用同一参考片段中的腾空劈斩");
            await Frames(15);
            Check(_actor.Rig.SpatialMotionActive,"普攻运行时实际进入空间骨架采样");
            float time=_actor.ActionTime;
            Transform2D weapon=_actor.Rig.WeaponPart.GlobalTransform;
            GetTree().Paused=true; await Frames(5);
            Check(Mathf.IsEqualApprox(time,_actor.ActionTime) && weapon.IsEqualApprox(_actor.Rig.WeaponPart.GlobalTransform),"暂停冻结刀、身体、衣物及动作时间");
            GetTree().Paused=false; await Frames(70);
            Check(_actor.CurrentState==Combatant.State.Idle && !_actor.Rig.SpatialMotionActive,"普攻片段结束正常收刀");
            _actor.Face(Vector2.Left);
            while(!_actor.CanAct) await Frames(1);
            _actor.TryAction(CombatAction.Basic); await Frames(23);
            Check(_actor.Rig.Scale.X<0 && _actor.Rig.SpatialGripError<.02f,"反向施法镜像整套动作且握点仍准确");
            _actor.ReceiveDamage(new DamageInfo(arena.Player,1,Vector2.Right)); await Frames(2);
            Check(_actor.CurrentState==Combatant.State.Hurt && !_actor.Rig.SpatialMotionActive,"受击打断连招并恢复常规图层");
            GD.Print($"魏延视频连斩验证通过：{_checks}项；真实渲染：{Rendered}。");
            GetTree().Quit();
        }
        catch(Exception error) { GD.PushError(error.ToString()); GetTree().Paused=false; GetTree().Quit(1); }
    }

    private void ValidateClip()
    {
        var model=_actor.Presentation.Model;
        var clip=_actor.Presentation.Animation.Combo.SpatialClip;
        Check(clip.Keys.Count>=30,"完整连招包含逐段参考关键帧");
        float lengthError=0,footError=0,gripError=0,maxElbow=0,maxKnee=0,maxJointStep=0;
        float maxTurn=0,minFootSeparation=float.MaxValue,minBladeElevation=float.MaxValue,maxBladeElevation=float.MinValue;
        Vector3[] previous=null;
        for(float t=0;t<=clip.Duration;t+=1f/120)
        {
            var sample=SpatialMotionSampler.Sample(clip,t);
            maxTurn=Mathf.Max(maxTurn,Mathf.Max(Mathf.Abs(sample.Yaw),Mathf.Abs(sample.Twist)));
            minFootSeparation=Mathf.Min(minFootSeparation,sample.NearFoot.X-sample.FarFoot.X);
            minBladeElevation=Mathf.Min(minBladeElevation,sample.BladeAngles.Y);
            maxBladeElevation=Mathf.Max(maxBladeElevation,sample.BladeAngles.Y);
            var skeleton=SpatialMotionSampler.Solve(sample,model);
            var p=skeleton.Points;
            if(previous!=null)
                for(int joint=0;joint<13;joint++) maxJointStep=Mathf.Max(maxJointStep,p[joint].DistanceTo(previous[joint]));
            previous=p;
            foreach(int upper in new[]{3,6})
            {
                lengthError=Mathf.Max(lengthError,Mathf.Abs(p[upper].DistanceTo(p[upper+1])-18));
                lengthError=Mathf.Max(lengthError,Mathf.Abs(p[upper+1].DistanceTo(p[upper+2])-18));
                maxElbow=Mathf.Max(maxElbow,Mathf.RadToDeg((p[upper+1]-p[upper]).AngleTo(p[upper+2]-p[upper+1])));
            }
            for(int side=0;side<2;side++)
            {
                int hip=side==0?9:11;
                lengthError=Mathf.Max(lengthError,Mathf.Abs(p[hip].DistanceTo(p[hip+1])-model.ThighLength));
                lengthError=Mathf.Max(lengthError,Mathf.Abs(p[hip+1].DistanceTo(p[16+side])-(model.LowerLegLength-model.FootHeight)));
                maxKnee=Mathf.Max(maxKnee,Mathf.RadToDeg((p[hip+1]-p[hip]).AngleTo(p[16+side]-p[hip+1])));
            }
            footError=Mathf.Max(footError,skeleton.FootError); gripError=Mathf.Max(gripError,skeleton.GripError);
        }
        GD.Print($"空间骨架：骨长误差{lengthError:F5}，握柄{gripError:F5}，足点{footError:F5}，肘最大{maxElbow:F1}°，膝最大{maxKnee:F1}°。");
        Check(lengthError<.002f,"全部源动作采样保持真实骨长，投影缩短不等于拉伸骨骼");
        Check(footError<.02f && gripError<.02f,"所有参考帧足底到位、主手握柄不分离");
        Check(maxElbow<145 && maxKnee<145,"肘膝弯曲保留合理余量");
        GD.Print($"连续采样最大关节位移：{maxJointStep:F3} 骨架单位/120Hz。");
        Check(maxJointStep<8,"挥斩中关节连续移动，没有求解支路突跳");
        Check(maxTurn<.001f,"完整连招与普攻源片段始终保持朝向，不再旋身或扭腰换面");
        Check(minFootSeparation>25,"双脚保持各自一侧，不再交叉换位绕圈");
        Check(maxBladeElevation-minBladeElevation<210,"大刀往复横斩与下劈，不再整圈轮转");
        var skill=GD.Load<CombatActionConfig>("res://Resources/Config/Skills/WeiYanSaberDance.tres");
        var spec=skill.ToSpec();
        for(int i=0;i<6;i++) Check(spec.HitPulseAt((skill.HitWindows[i].X+skill.HitWindows[i].Y)*.5f)==i,$"第{i+1}次挥斩独立对应命中窗口");
        Check(spec.HitPulseAt(.5f)==-1 && spec.HitPulseAt(1.8f)==-1 && spec.HitPulseAt(3.8f)==-1,"回架、蓄力与收势空隙不产生伤害");
        Check(!ReferenceEquals(spec.HitWindows,skill.HitWindows),"命中窗口为本次施法的独立只读快照");
    }

    /// <summary>让真实目标一直留在范围内，确认六个动作窗口各结算一次，回架空隙不扣血。</summary>
    private async Task CheckCombatWindows(Combatant target)
    {
        while(_actor.Cooldown(CombatAction.Skill4)>0) await Frames(1);
        target.Position=_actor.Position+Vector2.Right*25; await Frames(4);
        int initial=target.Health.CurrentHealth,previous=initial,hits=0;
        Check(_actor.TryAction(CombatAction.Skill4),"通过真实战斗入口验证连斩逐段命中");
        for(int frame=0;frame<500 && _actor.CurrentState==Combatant.State.Attack;frame++)
        {
            target.Position=_actor.Position+Vector2.Right*25;
            await Frames(1);
            int health=target.Health.CurrentHealth;
            if(health!=previous)
            {
                Check(_actor.Spec.HitPulseAt(_actor.ActionTime)>=0,"伤害只发生在对应挥斩的有效窗口");
                hits++; previous=health;
            }
        }
        Check(hits==6 && initial-target.Health.CurrentHealth==24,"真实目标恰好承受六段伤害，多帧重叠不重复扣血");
        target.Position=new Vector2(40,280); await Frames(35);
    }

    private async Task Record()
    {
        SubViewport viewport=null; Node2D root=null;
        var copies=new Dictionary<Node2D,Node2D>();
        if(Rendered)
        {
            viewport=new SubViewport { Size=new Vector2I(720,640),Disable3D=true,ProcessMode=ProcessModeEnum.Always,RenderTargetUpdateMode=SubViewport.UpdateMode.Always };
            AddChild(viewport);
            viewport.AddChild(new ColorRect { Size=new Vector2(720,640),Color=new Color("202621") });
            viewport.AddChild(new ColorRect { Position=new Vector2(45,553),Size=new Vector2(630,1),Color=new Color("50574c") });
            var label=new Label { Text="魏延 · 大刀连斩（固定朝向）",Position=new Vector2(24,18),Theme=UiStyles.ChineseTheme(23) };
            viewport.AddChild(label);
            root=new Node2D { Position=new Vector2(360,550),Scale=new Vector2(2.8f,2.8f*.96f) }; viewport.AddChild(root);
        }
        int active=0;
        for(int frame=0;frame<300;frame++)
        {
            if(frame==30) Check(_actor.TryAction(CombatAction.Skill4),"第四技能实际启动完整视频参考连斩");
            await Frames(1);
            if(_actor.Rig.SpatialMotionActive)
            {
                active++;
                if(_actor.Rig.SpatialMotionWeight>.99f)
                    CheckRuntimePose();
            }
            if(!Rendered || frame%2!=0) continue;
            foreach(Node2D source in _actor.Rig.ArtworkRoot.GetChildren())
            {
                if(!copies.TryGetValue(source,out var copy)) { copy=RigArtworkCapture.CopyLayer(source); copies[source]=copy; root.AddChild(copy); }
                copy.Visible=source.Visible;
                copy.Transform=_actor.Rig.Skeleton.GlobalTransform.AffineInverse()*source.GlobalTransform;
                copy.Modulate=source.Modulate;
                if(copy is Sprite2D sprite && source is Sprite2D original) { sprite.Texture=original.Texture; sprite.FlipH=original.FlipH; sprite.FlipV=original.FlipV; }
                if(copy is MeshInstance2D mesh && source is MeshInstance2D originalMesh) { mesh.Mesh=originalMesh.Mesh; mesh.Texture=originalMesh.Texture; }
                root.MoveChild(copy,root.GetChildCount()-1);
            }
            GetTree().Paused=true;
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using(var image=viewport.GetTexture().GetImage()) image.SavePng($"{Folder}/Frames/Frame{frame/2:D3}.png");
            GetTree().Paused=false;
        }
        viewport?.QueueFree();
        Check(active>220,"完整4秒动作持续采样，未被短普攻时钟截断");
    }
    private void CheckRuntimePose()
    {
        var rig=_actor.Rig;
        if(rig.SpatialFootError>.02f || rig.SpatialGripError>.02f) throw new InvalidOperationException("实机手脚约束失败");
        Vector2 point=rig.Skeleton.ToLocal(rig.Joints[5].GlobalPosition);
        if(point.DistanceTo(rig.ReferencePrimaryGrip)>.1f) throw new InvalidOperationException("最终绘制骨架与握点不同步");
        foreach(var part in rig.DrawOrderedParts)
            if(part.Visible && part.Texture is AtlasTexture atlas && atlas.Atlas==_actor.Presentation.Model.TurnAtlas)
                throw new InvalidOperationException("固定朝向连斩不应切换侧背面身体或脚掌贴图");
    }
    private async Task Frames(int count)
    {
        for(int i=0;i<count;i++)
        {
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(Rendered) await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        }
    }
    private void Check(bool value,string message) { if(!value) throw new InvalidOperationException(message); _checks++; GD.Print("通过："+message); }
}
