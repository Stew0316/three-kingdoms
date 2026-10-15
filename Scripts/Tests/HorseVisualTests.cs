using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>独立坐骑回归：真实八方向位移、起停、碰墙、换马与连续帧，不读写正式存档。</summary>
public partial class HorseVisualTests : Node
{
    private FirstCity City => (FirstCity)GetTree().CurrentScene;
    private CityHorse Horse => City.Player.GetNode<CityHorse>("RidingHorse");
    private int _checks;
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
            VerifyJointChains(stable.Select(h => h.Appearance.BodyWidth).ToArray());
            // 支撑期保持蹄高，回摆期抬蹄，首尾连续。
            Check(CityHorse.SampleHoof(.1f).Y == 0 && CityHorse.SampleHoof(.4f).Y == 0 && CityHorse.SampleHoof(.71f).Y < -9, "支撑蹄落地、摆动蹄抬起");
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
                    Check(Mathf.Abs(City.Player.Rig.Position.Y-(-24+Horse.BodyBob))<.001f, "骑手与马背同帧起伏");
                    if(direction==0) speeds[mount-1] = City.Player.Position.DistanceTo(start);
                    await Capture($"horse-{mount}-direction-{direction}");
                    InputVector(Vector2.Zero);
                    await Frames(12);
                    Check(Horse.GaitWeight==0 && Horse.BodyBob==0, "停步平滑收回中立站姿");
                    float stopped = Horse.GaitPhase;
                    await Frames(4);
                    Check(Horse.GaitPhase==stopped, "静止不原地踏步");
                }
                // 在横街连续跑过一轮；保存真实运动帧供检查前后腿遮挡。
                City.Player.Position = new(360,310);
                InputVector(Vector2.Right);
                for(int frame=0; frame<12; frame++) { await Frames(2); await Capture($"horse-{mount}-trot-{frame:00}"); }
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
            Check(speeds[1]/speeds[0]>1.22f && speeds[1]/speeds[0]<1.42f, "共用步态下保留190与250的不同实际移速");
            City.Player.Position = new(710,570); await Frames(3); await Capture("stable-both");
            await HorsePosePreview.Capture(GetTree());
            GD.Print($"HORSE_TEST_PASS: {_checks} 项；真实渲染 {DisplayServer.GetName()!="headless"}");
            GetTree().Quit();
        }
        catch(Exception error) { InputVector(Vector2.Zero); GD.PushError("HORSE_TEST_FAIL: "+error); GetTree().Quit(1); }
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

        int pairedSupport = 0, suspended = 0, invalidTiming = 0;
        float invalidPhase = 0;
        var diagonalPhase = new WorstSample();
        var sidePhase = new WorstSample();
        for(int frame=0;frame<samples;frame++)
        {
            float phase=frame*phaseStep;
            var frontNear=HorseLocomotion.Sample(true,true,phase,1);
            var rearFar=HorseLocomotion.Sample(false,false,phase,1);
            var frontFar=HorseLocomotion.Sample(true,false,phase,1);
            var rearNear=HorseLocomotion.Sample(false,true,phase,1);
            diagonalPhase.Record(Mathf.Abs(frontNear.Phase-rearFar.Phase),$"相位{phase:F6}");
            diagonalPhase.Record(Mathf.Abs(frontFar.Phase-rearNear.Phase),$"相位{phase:F6}");
            sidePhase.Record(Mathf.Abs(Mathf.Abs(frontNear.Phase-rearNear.Phase)-.5f),$"相位{phase:F6}");
            int contacts = (frontNear.Supporting ? 1:0)+(rearFar.Supporting ? 1:0)+(frontFar.Supporting ? 1:0)+(rearNear.Supporting ? 1:0);
            if(frontNear.Supporting!=rearFar.Supporting || frontFar.Supporting!=rearNear.Supporting || (contacts!=0 && contacts!=2))
            { invalidTiming++; invalidPhase=phase; }
            if(contacts==2) pairedSupport++;
            if(contacts==0) suspended++;
        }
        Check(diagonalPhase.Value<.0001f,$"对角前后腿同相：{diagonalPhase.Describe()}");
        Check(sidePhase.Value<.0001f,$"同侧前后腿错开半周期：{sidePhase.Describe()}");
        Check(invalidTiming==0 && pairedSupport>0 && suspended>0,$"两拍对角承重与短腾空：双蹄{pairedSupport}帧，腾空{suspended}帧，非法{invalidTiming}帧（末次相位{invalidPhase:F6}）");
        GD.Print($"HORSE_JOINT_SAMPLES: {sampleCount} 个骨架样本，{samples} 个四腿时序样本；纯几何验收仍需配合连续实机画面审查。");
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
        DirAccess.MakeDirRecursiveAbsolute("res://Build/horse-joints");
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage(); image.SavePng("res://Build/horse-joints/"+name+".png");
    }
    private void Check(bool condition,string text)
    { if(!condition) throw new InvalidOperationException(text); _checks++; GD.Print("通过："+text); }
}

