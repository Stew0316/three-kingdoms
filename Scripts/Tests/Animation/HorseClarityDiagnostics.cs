using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>独立画质诊断：固定姿态的小数像素位移对照，以及正式城市的物理/渲染节拍记录。不改正式资源。</summary>
public partial class HorseClarityDiagnostics : Node
{
    private static string Output => OS.GetCmdlineUserArgs().Contains("--horse-component-regression") ? "res://Build/horse-components/cadence-240" :
        OS.GetCmdlineUserArgs().Contains("--horse-skin-regression") ? "res://Build/horse-skin-join/cadence-240" : "res://Build/horse-clarity" +
        (OS.GetCmdlineUserArgs().Contains("--after-fix") ? "/after-fix" : "") +
        (OS.GetCmdlineUserArgs().Contains("--rate-144") ? "/144" :
            OS.GetCmdlineUserArgs().Contains("--cadence-only") ? "/realtime" : "");
    public override void _Ready() => CallDeferred(MethodName.Run);

    private async void Run()
    {
        try
        {
            // 与其他专项一样使用测试会话，防止误读写玩家存档。
            if (!OS.GetCmdlineUserArgs().Contains("--horse-clarity-test"))
                throw new InvalidOperationException("必须附带 --horse-clarity-test 参数。");
            DirAccess.MakeDirRecursiveAbsolute(Output);
            if (!OS.GetCmdlineUserArgs().Contains("--cadence-only")) await CaptureSampling();
            await CaptureCadence();
            GD.Print("HORSE_CLARITY_DONE: " + Output);
            GetTree().Quit();
        }
        catch (Exception error)
        {
            Input.ActionRelease("move_right");
            GD.PushError("HORSE_CLARITY_FAIL: " + error);
            GetTree().Quit(1);
        }
    }

    /// <summary>用正式绘制器固定站姿。1.7是1280×720窗口中2倍内容缩放乘0.85镜头；参考图为8倍超采样。</summary>
    private async Task CaptureSampling()
    {
        foreach (string coat in new[] { "chestnut", "dapple" })
        {
            var appearance = GD.Load<HorseAppearance>($"res://Resources/Config/World/horse-{coat}.tres");
            using var source = appearance.PartsAtlas.GetImage();
            if (source.IsCompressed()) source.Decompress();
            source.GenerateMipmaps();
            using var mipTexture = ImageTexture.CreateFromImage(source);
            foreach (string variant in new[] { "linear", "nearest", "mipmap", "mipmap_crisp", "reference8x" })
            {
                int supersample = variant == "reference8x" ? 8 : 1;
                var viewport = new SubViewport
                {
                    Size = new Vector2I(256,160) * supersample,
                    TransparentBg = false,
                    RenderTargetUpdateMode = SubViewport.UpdateMode.Always
                };
                AddChild(viewport);
                viewport.AddChild(new ColorRect { Color = new Color("e8e1d2"), Size = viewport.Size });
                var horse = HorseActor.Create(appearance);
                horse.Scale = Vector2.One * (1.7f * supersample);
                viewport.AddChild(horse);
                horse.SetProcess(false);
                horse.SetPreviewPose(0,0);
                var surfaces = new List<Polygon2D> { horse.PaintedRig.BodySkin, horse.PaintedRig.TailSkin, horse.PaintedRig.TackSkin };
                surfaces.AddRange(horse.PaintedRig.LimbSkins);
                foreach (var mesh in surfaces)
                {
                    // 显式还原无偏置材质，保证正式材质修改后基准仍代表原先的线性采样。
                    mesh.Material = new ShaderMaterial { Shader = new Shader { Code =
                        "shader_type canvas_item; void fragment(){if(COLOR.a<0.035)discard;}" } };
                    mesh.TextureFilter = variant == "nearest" ? CanvasItem.TextureFilterEnum.Nearest :
                        variant.StartsWith("mipmap") ? CanvasItem.TextureFilterEnum.LinearWithMipmaps : CanvasItem.TextureFilterEnum.Linear;
                    if (variant.StartsWith("mipmap")) mesh.Texture = mipTexture;
                    if (variant == "mipmap_crisp") mesh.Material = HorseSurfaceMaterial.Shared;
                }
                string directory = $"{Output}/sampling/{coat}/{variant}";
                DirAccess.MakeDirRecursiveAbsolute(directory);
                // 整段只向右移动不足1个屏幕像素，骨骼姿态、光照、相机完全不变。
                for (int frame = 0; frame < 16; frame++)
                {
                    horse.Position = new Vector2(105 + frame / 16f,135) * supersample;
                    await DrawFrame();
                    using var image = viewport.GetTexture().GetImage();
                    CheckSave(image.SavePng($"{directory}/{frame:00}.png"));
                }
                viewport.QueueFree();
                await DrawFrame();
            }
        }
    }

    private async Task CaptureCadence()
    {
        var city = GD.Load<PackedScene>(WorldScenePaths.FirstCity).Instantiate<FirstCity>();
        AddChild(city);
        await DrawFrame();
        city.Mount(1);
        var horse = city.Player.GetNode<CityHorse>("RidingHorse");
        using var atlasImage = horse.Appearance.PartsAtlas.GetImage();
        SaveJson("environment.json", new
        {
            engine = Engine.GetVersionInfo()["string"].AsString(),
            physicsTicks = Engine.PhysicsTicksPerSecond,
            physicsInterpolation = GetTree().PhysicsInterpolation,
            physicsJitterFix = Engine.PhysicsJitterFix,
            cameraCallback = city.Camera.ProcessCallback.ToString(),
            displayRefresh = DisplayServer.ScreenGetRefreshRate(),
            windowSize = new[] { GetWindow().Size.X, GetWindow().Size.Y },
            stretch = GetViewport().GetStretchTransform().X.X,
            cameraZoom = city.Camera.Zoom.X,
            bodySourceWidth = HorseArtAtlas.Body.Size.X,
            bodyWorldWidth = HorseArtAtlas.Body.Size.X * .073f * horse.Appearance.BodyWidth,
            textureMipmaps = atlasImage.HasMipmaps(),
            filter = horse.PaintedRig.BodySkin.TextureFilter.ToString(),
            commandArguments = OS.GetCmdlineArgs()
        });
        foreach (string mode in new[] { "original", "physics_camera", "no_smoothing" })
        {
            Input.ActionRelease("move_right");
            city.Camera.ProcessCallback = mode == "physics_camera" ? Camera2D.Camera2DProcessCallback.Physics : Camera2D.Camera2DProcessCallback.Idle;
            city.Camera.PositionSmoothingEnabled = mode != "no_smoothing";
            city.Player.Position = new Vector2(395,310);
            horse.ResetGait();
            city.Camera.Position = city.Player.Position + new Vector2(0,-95);
            city.Camera.ResetSmoothing();
            city.Camera.ForceUpdateScroll();
            for (int i = 0; i < 80; i++) await DrawFrame();
            using (var standing = GetViewport().GetTexture().GetImage()) CheckSave(standing.SavePng($"{Output}/{mode}-standing.png"));
            var frames = new List<object>();
            Input.ActionPress("move_right");
            // 0.65秒处于镜头可跟随的横街区间，避开建筑碰撞与镜头限位。
            double duration = 0;
            float lastScreenX = 0, lastRenderPhase = 0, minStep = float.PositiveInfinity, maxStep = float.NegativeInfinity;
            int steadyFrames = 0, repeatedPhases = 0;
            float maxSeatError = 0;
            while (duration < .65)
            {
                await DrawFrame();
                double dt = GetProcessDeltaTime();
                duration += dt;
                Vector2 screen = GetViewport().GetStretchTransform() * horse.GetGlobalTransformWithCanvas().Origin;
                float seatError = city.Player.Rig.Position.DistanceTo(horse.Position+horse.SaddleOffset+new Vector2(0,-24));
                maxSeatError = Mathf.Max(maxSeatError,seatError);
                if (duration > .32)
                {
                    minStep = Mathf.Min(minStep,screen.X-lastScreenX);
                    maxStep = Mathf.Max(maxStep,screen.X-lastScreenX);
                    steadyFrames++;
                    if (horse.RenderGaitPhase == lastRenderPhase) repeatedPhases++;
                }
                lastScreenX = screen.X;
                lastRenderPhase = horse.RenderGaitPhase;
                frames.Add(new
                {
                    dt,
                    physicsFrame = Engine.GetPhysicsFrames(),
                    x = city.Player.Position.X,
                    screenX = screen.X,
                    phase = horse.GaitPhase,
                    renderPhase = horse.RenderGaitPhase,
                    renderX = horse.RenderedGlobalPosition.X,
                    interpolationFraction = Engine.GetPhysicsInterpolationFraction(),
                    riderSeatError = seatError,
                    weight = horse.GaitWeight,
                    cameraX = city.Camera.Position.X,
                    cameraCenterX = city.Camera.GetScreenCenterPosition().X
                });
            }
            Input.ActionRelease("move_right");
            using (var moving = GetViewport().GetTexture().GetImage()) CheckSave(moving.SavePng($"{Output}/{mode}-moving.png"));
            SaveJson(mode + "-cadence.json",frames);
            if (mode == "original" && OS.GetCmdlineUserArgs().Contains("--assert-continuity"))
            {
                // 此断言只用于固定144/240绘制帧的诊断；实时帧率不恒定时单帧位移不能套固定阈值。
                if (steadyFrames < 40 || repeatedPhases != 0 || minStep < -.02f || maxStep > .3f || maxSeatError > .001f)
                    throw new InvalidOperationException($"连续性未通过：样本{steadyFrames}，重复相位{repeatedPhases}，位移{minStep}..{maxStep}，鞍点{maxSeatError}");
                GD.Print($"HORSE_CONTINUITY_PASS: 稳定段{steadyFrames}帧，无重复相位；屏幕步进{minStep:F4}..{maxStep:F4}；鞍点误差{maxSeatError:F6}");
            }
        }
    }

    private async Task DrawFrame()
    {
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
    }
    private static void CheckSave(Error error)
    {
        if (error != Error.Ok) throw new InvalidOperationException("诊断截图保存失败：" + error);
    }
    private static void SaveJson(string name,object data)
    {
        using var file = Godot.FileAccess.Open(Output + "/" + name,Godot.FileAccess.ModeFlags.Write);
        if (file == null) throw new InvalidOperationException("诊断数据保存失败：" + Godot.FileAccess.GetOpenError());
        file.StoreString(JsonSerializer.Serialize(data,new JsonSerializerOptions { WriteIndented = true }));
    }
}
