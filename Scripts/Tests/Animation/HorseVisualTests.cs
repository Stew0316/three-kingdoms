using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>三拍跑步独立回归：真实八方向位移、起停、碰墙、换马与连续帧，不读写正式存档。</summary>
public partial class HorseVisualTests : Node
{
    private FirstCity City => (FirstCity)GetTree().CurrentScene;
    private CityHorse Horse => City.Player.GetNode<CityHorse>("RidingHorse");
    private int _checks;
    // 在截图完成的同一帧采样游戏时钟；后续制作正常速度动画使用实测时间差。
    private readonly List<CapturedFrame> _capturedFrames = new();
    public override void _Ready() => CallDeferred(MethodName.Run);
    private async void Run()
    {
        try
        {
            await Frames(5);
            City.Camera.PositionSmoothingEnabled = false;
            var stable = City.Map.Actors.GetChildren().OfType<CityHorse>().ToArray();
            Check(stable.Length == 2 && stable[0].Appearance != stable[1].Appearance, "两匹展示马绑定独立外观资源");
            Check(stable[0].Appearance.LongMane && stable[0].Appearance.SaddleBags && stable[1].Appearance.Dappled && !stable[1].Appearance.LongMane, "鬃毛、斑纹和马具差异实际接入");
            // 先保留完整可视证据，几何约束失败时也能核对对应相位的实际形态。
            await HorsePosePreview.Capture(GetTree());
            if(DisplayServer.GetName()!="headless")
            {
                var connection=await HorsePosePreview.CaptureLimbConnections(GetTree());
                Check(connection.Ratio>.995f,$"移除遮挡后，八条渲染腿从上段到蹄连成一片：最小主区域占比{connection.Ratio:P3}；{connection.Context}");
            }
            VerifyPaintedRig(stable);
            VerifyJointChains(stable.Select(h => h.Appearance.BodyWidth).ToArray());
            // 单腿局部相位：前30%承重，随后屈腿回收；不能沿用两拍小跑的落蹄窗口。
            Check(CityHorse.SampleHoof(.1f).Y == 0 && CityHorse.SampleHoof(.29f).Y == 0 && CityHorse.SampleHoof(.65f).Y < -9, "三拍跑步支撑蹄落地、摆动蹄明确抬起");
            Check(CityHorse.SampleHoof(.99999f).DistanceTo(CityHorse.SampleHoof(0)) < .01f, "步态周期首尾连续");
            float[] speeds = new float[2];
            for (int mount = 1; mount <= 2; mount++)
            {
                City.Mount(mount);
                Check(Horse.Appearance == stable[mount-1].Appearance && !stable[mount-1].Visible, "上马使用对应外观并隐藏栏内同一匹马");
                Check(Horse.GaitPhase == 0 && Horse.GaitWeight == 0, "换马上马从中立姿态重新开始");
                for(int direction=0; direction<8; direction++)
                {
                    City.Player.Position = new(356,340);
                    await Frames(10);
                    Vector2 movement = Vector2.Right.Rotated(direction * Mathf.Pi / 4);
                    Vector2 start = City.Player.Position;
                    float distance = City.Player.WalkDistance, phase = Horse.GaitPhase;
                    InputVector(movement);
                    await Frames(12);
                    Check(City.Player.Position.DistanceTo(start)>20, $"马{mount}方向{direction}真实移动");
                    float expected = Mathf.PosMod(phase+(City.Player.WalkDistance-distance)/CityHorse.CycleDistance,1);
                    Check(Mathf.Abs(expected-Horse.GaitPhase)<.001f, "步态随实际路程推进");
                    Check(City.Player.Rig.Position.DistanceTo(Horse.SaddleOffset+new Vector2(0,-24))<.001f, "骑手与鞍点同帧平移，包含马身俯仰造成的前后位移");
                    Check(Mathf.Abs(Horse.BodyPitch-HorseLocomotion.BodyPitch(Horse.GaitPhase,Horse.GaitWeight))<.0001f, "坐骑绘制与腿链读取同一马身俯仰");
                    if(direction==0) speeds[mount-1] = City.Player.Position.DistanceTo(start);
                    await Capture($"horse-{mount}-direction-{direction}");
                    InputVector(Vector2.Zero);
                    await Frames(12);
                    Check(Horse.GaitWeight==0 && Horse.BodyBob==0 && Horse.BodyPitch==0 && Horse.SaddleOffset.Length()<.001f, "停步平滑收回中立站姿和鞍点");
                    float stopped = Horse.GaitPhase;
                    await Frames(4);
                    Check(Horse.GaitPhase==stopped, "静止不原地踏步");
                }
                // 在横街连续跑过一轮；保存真实运动帧供检查前后腿遮挡。
                City.Player.Position = new(360,310);
                InputVector(Vector2.Right);
                for(int frame=0; frame<48; frame++) { await Frames(1); await Capture($"horse-{mount}-canter-{frame:00}"); }
                InputVector(Vector2.Zero);
                City.Player.Position = new(917,294);
                InputVector(Vector2.Right);
                await Frames(25);
                float wallPhase = Horse.GaitPhase;
                await Frames(12);
                Check(City.Player.Position.X<922 && Mathf.Abs(Horse.GaitPhase-wallPhase)<.001f && Horse.GaitWeight==0, "顶墙保留碰撞且停止踏步");
                InputVector(Vector2.Zero);
                City.Dismount(); await Frames(3);
                Check(!City.Player.IsMounted && !Horse.Visible && stable.All(h=>h.Visible), "下马恢复徒步和栏内马匹");
            }
            Check(speeds[1]/speeds[0]>1.22f && speeds[1]/speeds[0]<1.42f, "共用三拍跑步下保留190与250的不同实际移速");
            City.Player.Position = new(710,570); await Frames(3); await Capture("stable-both");
            SaveCapturedFrames();
            GD.Print($"HORSE_TEST_PASS: {_checks} 项；真实渲染 {DisplayServer.GetName()!="headless"}");
            GetTree().Quit();
        }
        catch(Exception error)
        {
            InputVector(Vector2.Zero);
            GD.PushError("HORSE_TEST_FAIL: "+error);
            // 失败退出仍保存已经生成的截图时间，避免丢失定位问题所需的部分证据。
            try { SaveCapturedFrames(); }
            catch(Exception saveError) { GD.PushError("坐骑截图时间记录保存失败："+saveError); }
            GetTree().Quit(1);
        }
    }

    /// <summary>密采样直接检验关节几何与接地，不用帧数或已有通过项数代替姿势验收。</summary>
    private void VerifyJointChains(float[] bodyWidths)
    {
        const int samples = 2048;
        const float phaseStep = 1f / samples;
        const float lengthTolerance = .001f;
        var lengths = Enumerable.Range(0,4).Select(_ => new WorstSample()).ToArray();
        var jump = new WorstSample();
        var cycle = new WorstSample();
        var standing = new WorstSample();
        var weightJump = new WorstSample();
        var penetration = new WorstSample();
        var supportHeight = new WorstSample();
        var supportAngle = new WorstSample();
        var slipping = new WorstSample();
        float minBend = float.PositiveInfinity, minReachMargin = float.PositiveInfinity;
        string bendContext = "", reachContext = "";
        int sampleCount = 0;

        foreach(float width in bodyWidths)
        foreach(bool front in new[]{false,true})
        foreach(bool near in new[]{false,true})
        foreach(float weight in new[]{0f,.25f,.5f,1f})
        {
            Vector4 expected = HorseLocomotion.Lengths(front);
            var neutral = HorseLocomotion.Sample(front,near,0,0,width);
            for(int frame = 0; frame < samples; frame++)
            {
                float phase = frame * phaseStep;
                var pose = HorseLocomotion.Sample(front,near,phase,weight,width);
                string context = $"体宽{width:F2}，{(front ? "前" : "后")}{(near ? "近" : "远")}腿，权重{weight:F2}，相位{phase:F6}";
                sampleCount++;
                lengths[0].Record(Mathf.Abs(pose.Root.DistanceTo(pose.Proximal)-expected.X),context);
                lengths[1].Record(Mathf.Abs(pose.Proximal.DistanceTo(pose.Joint)-expected.Y),context);
                lengths[2].Record(Mathf.Abs(pose.Joint.DistanceTo(pose.Fetlock)-expected.Z),context);
                lengths[3].Record(Mathf.Abs(pose.Fetlock.DistanceTo(pose.Hoof)-expected.W),context);

                // 有符号叉积验证腕/飞节选在各自正确一侧，零面积表示关节退化成直线。
                float bend = (pose.Fetlock-pose.Proximal).Cross(pose.Joint-pose.Proximal) * (front ? -1 : 1);
                if(!float.IsFinite(bend) || bend < minBend) { minBend = float.IsFinite(bend) ? bend : float.NegativeInfinity; bendContext = context; }
                float reachMargin = expected.Y + expected.Z - pose.Proximal.DistanceTo(pose.Fetlock);
                if(!float.IsFinite(reachMargin) || reachMargin < minReachMargin) { minReachMargin = float.IsFinite(reachMargin) ? reachMargin : float.NegativeInfinity; reachContext = context; }

                // 腿链端点与蹄接地点不能低于这一侧的地面；近远腿的地面投影不同。
                float ground = near ? 2.5f : -3.5f;
                foreach(Vector2 point in Points(pose)) penetration.Record(Mathf.Max(0,point.Y-ground),context);
                if(pose.Supporting)
                {
                    supportHeight.Record(Mathf.Abs(pose.Hoof.Y-ground),context);
                    supportAngle.Record(Mathf.Abs(pose.HoofAngle),context);
                }
                if(weight == 0) standing.Record(PoseDifference(pose,neutral),context);

                var next = HorseLocomotion.Sample(front,near,phase+phaseStep,weight,width);
                jump.Record(PoseDifference(pose,next),context);
                cycle.Record(PoseDifference(pose,HorseLocomotion.Sample(front,near,phase+1,weight,width)),context);

                // 不只检查四个离散权重：在这些权重邻域检查起停是否触发IK换支。
                float nextWeight = weight < 1 ? weight + .001f : weight - .001f;
                weightJump.Record(PoseDifference(pose,HorseLocomotion.Sample(front,near,phase,nextWeight,width)),context);
                if(weight != 1) continue;
                foreach(float speed in new[]{190f,250f})
                {
                    const float delta = 1f / 240;
                    float travel = speed * delta;
                    var moving = HorseLocomotion.Sample(front,near,phase+travel/HorseLocomotion.CycleDistance,1,width);
                    if(pose.Supporting && moving.Supporting && moving.Phase > pose.Phase)
                        slipping.Record((moving.Hoof-pose.Hoof+Vector2.Right*travel).Length(),context+$"，速度{speed:F0}");
                }
            }
        }
        string[] segmentNames = {"上臂/股骨","前臂/胫骨","管骨","系部"};
        for(int i=0;i<lengths.Length;i++)
            Check(lengths[i].Value<=lengthTolerance,$"{segmentNames[i]}固定骨长：{lengths[i].Describe()}");
        Check(minBend>.05f,$"腕/飞节保持正确弯曲分支：最小有符号面积{minBend:F6}，{bendContext}");
        Check(minReachMargin>.01f,$"IK保留伸展余量：最小余量{minReachMargin:F6}像素，{reachContext}");
        Check(jump.Value<.35f,$"全周期无关节跳变（每1/{samples}周期）：{jump.Describe()}");
        Check(cycle.Value<.001f,$"完整周期相位一致：{cycle.Describe()}");
        Check(standing.Value<.001f,$"零权重静止骨架不随相位漂移：{standing.Describe()}");
        Check(weightJump.Value<.2f,$"起停权重连续变化不跳膝：{weightJump.Describe()}");
        Check(penetration.Value<.001f,$"四条腿和蹄接地点不穿地：{penetration.Describe()}");
        Check(supportHeight.Value<.001f,$"承重蹄贴地：{supportHeight.Describe()}");
        Check(supportAngle.Value<.001f,$"承重蹄底平放：{supportAngle.Describe()}");
        Check(slipping.Value<.001f,$"190/250移速下承重蹄锁定水平世界位置：{slipping.Describe()}");

        VerifyCanterTiming(samples);
        VerifyBodyMotion(bodyWidths,samples);
        GD.Print($"HORSE_JOINT_SAMPLES: {sampleCount} 个骨架样本，{samples} 个四腿时序样本；纯几何验收仍需配合连续实机画面审查。");
    }

    /// <summary>独立写出右领三拍事件表，避免把实现的相位函数再次调用当作正确答案。</summary>
    private void VerifyCanterTiming(int samples)
    {
        Check(Mathf.Abs(HorseLocomotion.CycleDistance-128)<.0001f && Mathf.Abs(HorseLocomotion.SupportFraction-.30f)<.0001f,"全新步长128像素、单腿承重占周期30%");
        bool[] front={false,false,true,true}, near={false,true,false,true};
        float[] touchdown={0,.22f,.22f,.46f};
        int[] expectedEvents={1,6,8};
        float[] expectedTimes={0,.22f,.46f};
        var phaseError=new WorstSample();
        var events=new System.Collections.Generic.List<(int Mask,float Phase)>();
        int supportErrors=0, airborneFrames=0, airborneEntries=0, airborneExits=0;
        int singleSupport=0, diagonalSupport=0;
        int previous=SupportMask((samples-1f)/samples);
        for(int frame=0;frame<samples;frame++)
        {
            float phase=frame/(float)samples;
            int mask=SupportMask(phase), expectedMask=0;
            for(int leg=0;leg<4;leg++)
            {
                float expectedPhase=Mathf.PosMod(phase-touchdown[leg],1);
                float actual=HorseLocomotion.Sample(front[leg],near[leg],phase,1).Phase;
                float error=Mathf.Abs(Mathf.Wrap(actual-expectedPhase,-.5f,.5f));
                phaseError.Record(error,$"腿{leg}，总相位{phase:F6}");
                if(expectedPhase<.30f) expectedMask|=1<<leg;
            }
            if(mask!=expectedMask) supportErrors++;
            int landing=mask&~previous;
            if(landing!=0) events.Add((landing,phase));
            if(mask==0) airborneFrames++;
            if(mask==0 && previous!=0) airborneEntries++;
            if(mask!=0 && previous==0) airborneExits++;
            if(mask==1 || mask==8) singleSupport++;
            if(mask==6) diagonalSupport++;
            previous=mask;
        }
        Check(phaseError.Value<.0001f,$"远后0、近后/远前0.22、近前0.46的独立腿相位：{phaseError.Describe()}");
        Check(supportErrors==0,$"四蹄接地窗口符合独立三拍时序表：错误{supportErrors}帧");
        Check(events.Count==3,$"每周期准确出现三组落蹄事件：实际{events.Count}组");
        for(int beat=0;beat<expectedEvents.Length;beat++)
            Check(events[beat].Mask==expectedEvents[beat] && Mathf.Abs(events[beat].Phase-expectedTimes[beat])<=1f/samples,
                $"第{beat+1}拍落蹄顺序与时点：蹄组{events[beat].Mask}，相位{events[beat].Phase:F6}");
        Check(airborneEntries==1 && airborneExits==1 && Mathf.Abs(airborneFrames/(float)samples-.24f)<2f/samples,
            $"每周期恰有一次末段腾空：进入{airborneEntries}次、退出{airborneExits}次，时长{airborneFrames/(float)samples:F4}");
        Check(singleSupport>0 && diagonalSupport>0,"存在独立后肢/领前肢承重及中间对角承重，未退回两拍小跑");
    }

    private static int SupportMask(float phase)
    {
        int mask=0;
        if(HorseLocomotion.Sample(false,false,phase,1).Supporting) mask|=1;
        if(HorseLocomotion.Sample(false,true,phase,1).Supporting) mask|=2;
        if(HorseLocomotion.Sample(true,false,phase,1).Supporting) mask|=4;
        if(HorseLocomotion.Sample(true,true,phase,1).Supporting) mask|=8;
        return mask;
    }

    /// <summary>全身各段有相对运动，颈链定长，周期和起停连续；不再要求肩髋锁成同一刚体。</summary>
    private void VerifyBodyMotion(float[] widths,int samples)
    {
        var rootError=new WorstSample(); var neckLength=new WorstSample();
        var positionJump=new WorstSample(); var cycle=new WorstSample();
        float pelvisRange=0,neckRange=0,shoulderRange=0;
        foreach(float width in widths)
        foreach(float weight in new[]{0f,.25f,.5f,1f})
        for(int frame=0;frame<samples;frame++)
        {
            float phase=frame/(float)samples;
            var p=HorseBodyMotion.Sample(phase,weight,width);
            var next=HorseBodyMotion.Sample(phase+1f/samples,weight,width);
            var end=HorseBodyMotion.Sample(phase+1,weight,width);
            var rest=HorseBodyMotion.Sample(0,0,width);
            string context=$"体宽{width:F2}，权重{weight:F2}，相位{phase:F6}";
            foreach(bool front in new[]{false,true}) foreach(bool near in new[]{false,true})
                rootError.Record(p.LegRoot(front,near).DistanceTo(HorseLocomotion.Sample(front,near,phase,weight,width).Root),context);
            neckLength.Record(Mathf.Abs(p.NeckRoot.DistanceTo(p.NeckMiddle)-rest.NeckRoot.DistanceTo(rest.NeckMiddle)),context);
            neckLength.Record(Mathf.Abs(p.NeckMiddle.DistanceTo(p.Poll)-rest.NeckMiddle.DistanceTo(rest.Poll)),context);
            Vector2[] points={p.Poll,p.NeckMiddle,p.Saddle,p.TailRoot,p.SkinPoint(new(-10,-30))};
            Vector2[] following={next.Poll,next.NeckMiddle,next.Saddle,next.TailRoot,next.SkinPoint(new(-10,-30))};
            Vector2[] final={end.Poll,end.NeckMiddle,end.Saddle,end.TailRoot,end.SkinPoint(new(-10,-30))};
            for(int i=0;i<points.Length;i++) { positionJump.Record(points[i].DistanceTo(following[i]),context); cycle.Record(points[i].DistanceTo(final[i]),context); }
            if(weight==0) cycle.Record(p.Saddle.DistanceTo(new(0,-43))+p.Poll.DistanceTo(new(33,-59)),"静止挂点归位");
            pelvisRange=Mathf.Max(pelvisRange,Mathf.Abs(p.Pelvis.Rotation-p.ChestAngle));
            neckRange=Mathf.Max(neckRange,Mathf.Abs(p.LowerNeck.Rotation-p.ChestAngle));
            shoulderRange=Mathf.Max(shoulderRange,Mathf.Abs(p.NearShoulder.Rotation-p.FarShoulder.Rotation));
        }
        Check(rootError.Value<.0001f,$"腿根与肩胛/骨盆挂点一致：{rootError.Describe()}");
        Check(neckLength.Value<.0001f,$"两段颈骨不伸缩：{neckLength.Describe()}");
        Check(positionJump.Value<.15f && cycle.Value<.001f,$"整身挂点连续：相邻{positionJump.Describe()}；周期{cycle.Describe()}");
        Check(pelvisRange>Mathf.DegToRad(3) && neckRange>Mathf.DegToRad(3) && shoulderRange>Mathf.DegToRad(5),"骨盆、下颈和左右肩胛具有独立相对运动");
    }

    private void VerifyPaintedRig(CityHorse[] horses)
    {
        foreach(var horse in horses)
        {
            Check(horse.Appearance.PartsAtlas!=null && horse.PaintedRig?.Bones.Count==32,"正式图集绑定32骨全身马架");
            Check(horse.PaintedRig.Parts.Count==15 && horse.PaintedRig.LimbSkins.Count==4,"胸腹、四条连续腿皮肤与15个彩绘层接入；肩臀不再叠独立覆盖片");
            Check(horse.PaintedRig.LimbSkins.All(s=>s.MinimumSampleAlpha>.9f),"四腿材质横截面位于原图实色区，避免透明裂口");
            float error=0,skinGap=0,rootSkinError=0;
            for(int frame=0;frame<96;frame++)
            {
                float phase=frame/96f; horse.SetPreviewPose(phase,1);
                foreach(var skin in horse.PaintedRig.LimbSkins) skinGap=Mathf.Max(skinGap,skin.MaximumJointGap);
                var body=HorseBodyMotion.Sample(phase,1,horse.Appearance.BodyWidth);
                for(int leg=0;leg<4;leg++) rootSkinError=Mathf.Max(rootSkinError,
                    horse.PaintedRig.LimbSkins[leg].RootSurfaceCenter.DistanceTo(body.LegSkinRoot(leg>=2,leg%2==1)));
                foreach(bool front in new[]{false,true}) foreach(bool near in new[]{false,true})
                {
                    var pose=HorseLocomotion.Sample(front,near,phase,1,horse.Appearance.BodyWidth);
                    Vector2 rendered=horse.PaintedRig.ToLocal(horse.PaintedRig.HoofWorld(front,near));
                    error=Mathf.Max(error,rendered.DistanceTo(pose.Hoof));
                }
            }
            Check(error<.001f,$"实际蹄纹理的接地点与求解链对齐：{error:F6}像素");
            Check(skinGap<.0001f,$"四腿上下骨段共用关节边界，全周期表面接缝：{skinGap:F6}像素");
            Check(rootSkinError<.0001f,$"腿根皮肤固定胸臀表面，肩胛活动不拖着根片滑动：{rootSkinError:F6}像素");
            horse.SetPreviewPose(0,0);
        }
    }

    private static Vector2[] Points(HorseLocomotion.LegPose pose) => new[]{pose.Root,pose.Proximal,pose.Joint,pose.Fetlock,pose.Hoof};
    private static float PoseDifference(HorseLocomotion.LegPose first,HorseLocomotion.LegPose second)
    {
        float difference = Mathf.Abs(first.HoofAngle-second.HoofAngle)*5;
        Vector2[] a=Points(first), b=Points(second);
        for(int i=0;i<a.Length;i++) difference=Mathf.Max(difference,a[i].DistanceTo(b[i]));
        return difference;
    }
    /// <summary>只输出每类验收的最差样本，失败时仍可精确定位马型、腿、相位和权重。</summary>
    private sealed class WorstSample
    {
        public float Value { get; private set; }
        private string _context = "全部样本误差为零";
        public void Record(float value,string context)
        {
            if(!float.IsFinite(value)) value=float.PositiveInfinity;
            if(value<=Value) return;
            Value=value; _context=context;
        }
        public string Describe() => $"最大值{Value:F6}，{_context}";
    }
    private static void InputVector(Vector2 value)
    {
        foreach(string name in new[]{"move_left","move_right","move_up","move_down"}) Input.ActionRelease(name);
        if(value.X>.1f) Input.ActionPress("move_right"); if(value.X<-.1f) Input.ActionPress("move_left");
        if(value.Y>.1f) Input.ActionPress("move_down"); if(value.Y<-.1f) Input.ActionPress("move_up");
    }
    private async Task Frames(int count)
    { for(int i=0;i<count;i++) { await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree().CreateTimer(0),SceneTreeTimer.SignalName.Timeout); } }
    private async Task Capture(string name)
    {
        if(DisplayServer.GetName()=="headless") return;
        DirAccess.MakeDirRecursiveAbsolute("res://Build/horse-canter");
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        var player=City.Player;
        var horse=Horse;
        var frame=new CapturedFrame(name,player.VisualTime,player.WalkDistance,horse.GaitPhase,horse.GaitWeight,
            new(player.Position.X,player.Position.Y),horse.Facing,new(player.FacingDirection.X,player.FacingDirection.Y));
        Error saved=image.SavePng("res://Build/horse-canter/"+name+".png");
        if(saved!=Error.Ok) throw new InvalidOperationException($"截图{name}保存失败：{saved}");
        _capturedFrames.Add(frame);
    }

    private sealed record FramePoint(float X,float Y);
    private sealed record CapturedFrame(string Name,float VisualTime,float WalkDistance,float GaitPhase,float GaitWeight,
        FramePoint PlayerPosition,float Facing,FramePoint PlayerFacing);

    /// <summary>每项对应同名PNG；VisualTime单位为游戏秒，Position/WalkDistance单位为地图像素。</summary>
    private void SaveCapturedFrames()
    {
        if(_capturedFrames.Count==0) return;
        const string path="res://Build/horse-canter/frames.json";
        using var file=Godot.FileAccess.Open(path,Godot.FileAccess.ModeFlags.Write);
        if(file==null) throw new InvalidOperationException("无法写入坐骑截图时间记录："+Godot.FileAccess.GetOpenError());
        file.StoreString(JsonSerializer.Serialize(_capturedFrames,new JsonSerializerOptions
        {
            WriteIndented=true,
            PropertyNamingPolicy=JsonNamingPolicy.CamelCase
        }));
        file.Flush();
        if(file.GetError()!=Error.Ok) throw new InvalidOperationException("坐骑截图时间记录写入失败："+file.GetError());
    }
    private void Check(bool condition,string text)
    { if(!condition) throw new InvalidOperationException(text); _checks++; GD.Print("通过："+text); }
}

