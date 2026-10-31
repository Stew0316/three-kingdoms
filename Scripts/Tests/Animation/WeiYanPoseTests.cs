using Godot;
using System;
using System.Threading.Tasks;

/// <summary>通过真实移动和施法入口验收魏延长刀；覆盖最终握点、固定骨长、八向步态、暂停与受击。</summary>
public partial class WeiYanPoseTests : Node
{
    private Arena _arena;
    private Combatant Actor => _arena.Enemy;
    private CharacterRig Rig => Actor.Rig;
    private bool Rendered => DisplayServer.GetName() != "headless";
    private int _checks;
    private float _primaryError, _secondaryError;
    private bool _fixedLengths = true;
    private float _armBindingError, _wristBend;
    private float _minElbow = 180, _maxElbow;
    private string _tightElbowContext;
    private bool _elbowSidesCorrect = true;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        CallDeferred(MethodName.Run);
    }

    private async void Run()
    {
        try
        {
            _arena = (Arena)GetTree().CurrentScene;
            foreach (var actor in new[] { _arena.Player, Actor })
            {
                actor.GetNode(NodeNames.Controller).ProcessMode = ProcessModeEnum.Disabled;
                actor.GetNode<GroundEffects>(NodeNames.GroundEffects).Hide();
            }
            _arena.Player.Position = new Vector2(90, 310);
            _arena.GetNode(NodeNames.Hud).ProcessMode = ProcessModeEnum.Disabled;
            _arena.Resolver.PassivesEnabled = false;
            Check(!_arena.IsSoloPractice && Actor.Visible, "魏延测试显式恢复对手");
            Check(Actor.Presentation.Animation.ReferencePoseSet && Rig.Joints.Count == 16, "魏延启用同一套16关节参考求解");
            Check(Actor.Presentation.Animation.StandingPose != _arena.Player.Presentation.Animation.StandingPose
                && Actor.Presentation.Animation.MovingPose != _arena.Player.Presentation.Animation.MovingPose, "两人使用独立姿态资源");
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://Build/weiyan-frames"));
            if (Rendered) await CheckWeaponPixels();
            Actor.Position = new Vector2(330, 245);
            Actor.Face(Vector2.Right);
            await Frames(30);
            CheckGuard("站立");
            Check(!Foot(0).FlipH && !Foot(1).FlipH, "新拆件双靴原生朝前，不再额外翻转小腿");
            Check(!Rig.Parts[15].Visible && Rig.CrestParts.Count == 0, "确认稿无凤冠或羽饰，运行时不生成额外冠翎");
            if (Rendered) await Capture("idle");
            for (int direction = 0; direction < 8; direction++)
                await CheckWalking(Vector2.Right.Rotated(direction * Mathf.Pi / 4), direction);
            Actor.SetMoveInput(Vector2.Zero);
            await Frames(35);
            CheckGuard("停步");
            float distance = Actor.WalkDistance, phase = Rig.WalkPhase;
            await Frames(12);
            Check(Mathf.IsEqualApprox(distance, Actor.WalkDistance) && Mathf.IsEqualApprox(phase, Rig.WalkPhase), "待机不继续迈步");
            Check(Rig.ClothWeight == 0, "停步后披风收稳，不持续无风摆动");

            foreach (var action in new[] { CombatAction.Basic, CombatAction.Dash, CombatAction.Sweep, CombatAction.Dodge })
                await CheckAction(action);
            Actor.Position = new Vector2(330, 245);
            Actor.PlayCounterSpin(68, .48f);
            for (int frame = 0; frame < 40; frame++)
            {
                await Frames(1);
                if (Rendered) await Capture($"counter-{frame:D3}");
            }
            Check(Actor.CounterRemaining <= 0, "狂骨回斩后正常结束");
            foreach (var direction in new[] { Vector2.Left, Vector2.Right })
            {
                Actor.ReceiveDamage(new DamageInfo(_arena.Player, 1, direction));
                await Frames(7);
                Check(Actor.CurrentState == Combatant.State.Hurt && Rig.Position.X * direction.X > 0, "受击位移遵循实际受力方向");
                await CheckPause();
                await Frames(35);
                CheckGuard("受击恢复");
            }
            Check(_fixedLengths, "全程手臂和大小腿骨长固定，骨骼不拉伸");
            GD.Print($"魏延手臂：屈肘 {_minElbow:F1}～{_maxElbow:F1}°，腕偏转 {_wristBend:F2}°，贴图关节误差 {_armBindingError:F3}。");
            GD.Print($"最大屈肘位置：{_tightElbowContext}");
            Check(_armBindingError < .05f && _wristBend < .1f, "全程肩肘腕贴图贴合，中立腕不跟刀身扭折");
            Check(_minElbow > 20 && _maxElbow < 150, "全程肘部保留弯曲余量，不锁死伸直或折叠挤肩");
            Check(_elbowSidesCorrect, "全程双臂沿各自持械路线屈肘，八方向移动和挥刀不突然翻肘");
            GD.Print($"魏延全程主握误差 {_primaryError:F3}，副握误差 {_secondaryError:F3} 骨架单位。");
            Check(_primaryError < .1f && _secondaryError < .1f, "全程主手不脱柄，副手完全接柄时握点准确");
            if (Rendered) await WeiYanMotionCapture.Record(this, Actor);
            GD.Print($"魏延姿态回归通过：{_checks} 项；真实渲染：{Rendered}。");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"魏延姿态回归失败：{exception}");
            GetTree().Paused = false;
            GetTree().Quit(1);
        }
    }

    private async Task CheckWalking(Vector2 direction, int index)
    {
        Actor.Position = new Vector2(330, 205);
        Actor.SetMoveInput(direction);
        await Frames(12);
        float start = Actor.WalkDistance;
        float[] support = { 0, 0 }, swing = { 0, 0 }, contactError = { 0, 0 };
        int[] samples = { 0, 0 };
        for (int frame = 0; frame < 44; frame++)
        {
            await Frames(1);
            for (int leg = 0; leg < 2; leg++)
            {
                float phase = Mathf.PosMod(Rig.WalkPhase + leg * .5f, 1);
                float bend = Mathf.Abs(Mathf.RadToDeg(Rig.Joints[leg == 0 ? 10 : 12].Rotation));
                if (phase is >= .18f and <= .36f)
                {
                    support[leg] += bend;
                    samples[leg]++;
                    var foot = Foot(leg);
                    Vector2 size = foot.Texture.GetSize();
                    float heel = Rig.Skeleton.ToLocal(foot.ToGlobal(new Vector2(0, size.Y))).Y;
                    float toe = Rig.Skeleton.ToLocal(foot.ToGlobal(size)).Y;
                    contactError[leg] = Mathf.Max(contactError[leg], Mathf.Abs(Mathf.Max(heel, toe)));
                }
                if (phase > Rig.ReferenceSupportFraction + .08f && phase < .95f)
                    swing[leg] = Mathf.Max(swing[leg], bend);
            }
            if (Rendered && index is 0 or 4) await Capture($"walk-{index}-{frame:D3}");
        }
        Check(Actor.WalkDistance - start >= Actor.Presentation.Animation.WalkCycleDistance, $"方向{index}完成整周期实际移动");
        for (int leg = 0; leg < 2; leg++)
        {
            float mean = support[leg] / Mathf.Max(1, samples[leg]);
            GD.Print($"魏延方向{index} 腿{leg}：承重膝 {mean:F1}°，摆动膝 {swing[leg]:F1}°，接地误差 {contactError[leg]:F2}。");
            Check(samples[leg] > 1 && mean is > 8 and < 35 && swing[leg] > mean + 10,
                $"方向{index} 腿{leg}承重撑起、摆动屈膝，避免持续蹲行");
            if (Mathf.Abs(direction.Y) < .01f) Check(contactError[leg] < 1.25f, $"方向{index} 腿{leg}承重脚底接地");
        }
        Vector2 weapon = WeaponDirection();
        Check(weapon.Y < -.95f && Mathf.Abs(weapon.X) < .3f && Rig.ReferenceGripWeight < .01f, $"方向{index}单手立刀行走，副手自然垂下");
        Check(Rig.ClothWeight > .1f, $"方向{index}披风跟随真实移动");
        if (Mathf.Abs(direction.X) > .1f) Check(Rig.Scale.X * direction.X > 0, $"方向{index}人物整体镜像符合朝向");
        if (index == 0) await CheckPause();
    }

    private async Task CheckAction(CombatAction action)
    {
        Actor.Position = new Vector2(330, 245);
        Actor.Face(Vector2.Right);
        // 从移动中的实姿态起势，验证起停切换而非只测静帧。
        Actor.SetMoveInput(Vector2.Right);
        await Frames(12);
        Check(Actor.TryAction(action), $"移动中启动{action}");
        Actor.SetMoveInput(Vector2.Zero);
        float unwrapped = 0, min = 0, max = 0;
        Vector2 previous = Vector2.Zero;
        float activeGrip = 1;
        int active = 0;
        bool paused = false;
        int count = Mathf.CeilToInt(Actor.Spec.Duration * Engine.PhysicsTicksPerSecond) + 28;
        for (int frame = 0; frame < count; frame++)
        {
            await Frames(1);
            if (Actor.IsAttackActive)
            {
                Vector2 direction = WeaponDirection();
                if (active > 0) unwrapped += previous.AngleTo(direction);
                previous = direction;
                min = Mathf.Min(min, unwrapped); max = Mathf.Max(max, unwrapped); active++;
                activeGrip = Mathf.Min(activeGrip, Rig.ReferenceGripWeight);
                if (!paused) { await CheckPause(); paused = true; }
            }
            if (Rendered) await Capture($"{action}-{frame:D3}");
        }
        if (action != CombatAction.Dodge)
        {
            Check(active >= 3 && Mathf.RadToDeg(max - min) > (action == CombatAction.Dash ? 35 : 65), $"{action}生效期间有明确挥斩弧线");
            if (Actor.Presentation.Animation.GetAction(Actor.CurrentMotion)?.SpatialClip != null)
                Check(Rig.SpatialGripError < .02f, $"{action}空间片段主手持续握刀，副手按参考动作平衡身体");
            else Check(activeGrip > .99f, $"{action}生效前副手已完成接柄");
        }
        Check(Actor.CurrentState == Combatant.State.Idle, $"{action}结束正常收势");
        CheckGuard($"{action}恢复");
        if (Rendered && action == CombatAction.Sweep) await CheckWeaponAfterimage();
    }

    private void CheckGuard(string label)
    {
        Vector2 direction = WeaponDirection();
        Check(Mathf.Abs(direction.X) < .2f && direction.Y < -.98f && Rig.ReferenceGripWeight < .01f,
            $"{label}保持确认立绘的单手立刀");
        foreach (int upper in new[] { 3, 6 })
        {
            Vector2 shoulder = Rig.Skeleton.ToLocal(Rig.Joints[upper].GlobalPosition);
            Vector2 elbow = Rig.Skeleton.ToLocal(Rig.Joints[upper + 1].GlobalPosition);
            Vector2 hand = Rig.Skeleton.ToLocal(Rig.Joints[upper + 2].GlobalPosition);
            if (upper == 3)
                Check(elbow.Y > shoulder.Y + 5 && hand.X > elbow.X + 3, $"{label}后手肘部下沉、前臂前托");
            else
                Check(hand.X > shoulder.X + 2 && hand.X < shoulder.X + 12 && hand.Y > shoulder.Y + 27
                    && elbow.Y > shoulder.Y + 14 && Mathf.Abs((hand-elbow).AngleTo(Vector2.Down)) < Mathf.DegToRad(20),
                    $"{label}自由手靠近身侧，肘部下沉且前臂接近竖直");
        }
        foreach (int leg in new[] { 0,1 })
        {
            int bone = leg == 0 ? 10 : 12;
            Vector2 knee = Rig.Skeleton.ToLocal(Rig.Joints[bone].GlobalPosition);
            Vector2 ankle = Rig.Skeleton.ToLocal(Rig.Joints[bone].ToGlobal(new Vector2(0,Rig.LowerLegLength-Actor.Presentation.Model.FootHeight)));
            Check(Mathf.Abs((ankle-knee).AngleTo(Vector2.Down)) < Mathf.DegToRad(15), $"{label}腿{leg}小腿自然承重，膝踝不横折");
            var foot = Foot(leg);
            Vector2 size = foot.Texture.GetSize();
            Vector2 heel = Rig.Skeleton.ToLocal(foot.ToGlobal(new Vector2(0,size.Y)));
            Vector2 toe = Rig.Skeleton.ToLocal(foot.ToGlobal(size));
            Check(Mathf.Abs(heel.Y-toe.Y) < .15f && Mathf.Abs(heel.Y) < .15f, $"{label}腿{leg}脚底放平贴地");
        }
    }

    /// <summary>直接渲染武器拆件，验证邻格衣摆透明、刀尖与刀柄保留；避免仅检查配置而漏掉着色器故障。</summary>
    private async Task CheckWeaponPixels()
    {
        var weapon = Rig.Parts[14];
        var atlas = (AtlasTexture)weapon.Texture;
        var viewport = new SubViewport { Size = new Vector2I(96, 360), TransparentBg = true,
            Disable3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(viewport);
        var sprite = new Sprite2D { Texture = weapon.Texture, Material = weapon.Material, Centered = false,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest };
        viewport.AddChild(sprite);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var result = viewport.GetTexture().GetImage();
        result.SavePng("res://Build/weiyan-weapon-isolated.png");
        float Alpha(int x, int y)
        {
            float sourceY = (y - atlas.Region.Position.Y) / atlas.Region.Size.Y;
            var model = Actor.Presentation.Model;
            float sourceSplit = (model.WeaponShaftSourceY - atlas.Region.Position.Y) / atlas.Region.Size.Y;
            float displaySplit = model.WeaponShaftDisplayY / (atlas.Region.Size.Y * weapon.Scale.Y);
            float displayY = model.WeaponShaftDisplayY <= 0 ? sourceY : sourceY < sourceSplit
                ? sourceY * displaySplit / sourceSplit
                : displaySplit + (sourceY - sourceSplit) * (1 - displaySplit) / (1 - sourceSplit);
            return result.GetPixel(x - (int)atlas.Region.Position.X, Mathf.RoundToInt(displayY * atlas.Region.Size.Y)).A;
        }
        Check(result.GetPixel(0,0).A < .01f && result.GetPixel(90,180).A < .01f, "长刀独立窗口没有邻格碎片或背景");
        Check(Alpha(792, 930) > .5f && Alpha(763, 989) > .5f, "实际采样保留刀尖与刀刃");
        Check(Alpha(790, 1150) > .5f && Alpha(791, 1230) > .5f, "实际采样保留完整刀杆与杆尾");
        var standing = Actor.Presentation.Animation.StandingPose;
        var poses = new[] { Actor.Presentation.Animation.Basic, Actor.Presentation.Animation.Dash,
            Actor.Presentation.Animation.Sweep, Actor.Presentation.Animation.Counter };
        bool shaftGrips = standing.BladeDistance > Actor.Presentation.Model.WeaponShaftDisplayY;
        foreach (var action in poses)
            foreach (var pose in new[] { action.PrepareVia, action.Windup, action.High, action.Contact, action.Follow })
                if (pose != null && pose.TwoHandWeight > .99f)
                    shaftGrips &= pose.BladeDistance - pose.GripSeparation > Actor.Presentation.Model.WeaponShaftDisplayY + 4;
        Check(shaftGrips, "全部双手姿态的握点位于刀头连接处下方的实际长杆");
        Check(_arena.Player.Rig.Parts[14].Material != weapon.Material, "两人武器使用独立实例材质");
        viewport.QueueFree();
    }

    /// <summary>强化移动实际生成残影后比较刀的透明轮廓，防止残影丢失分段采样而重新变成长刀头、短杆。</summary>
    private async Task CheckWeaponAfterimage()
    {
        Actor.Position = new Vector2(330,245);
        Actor.SetMoveInput(Vector2.Right);
        await Frames(12);
        Sprite2D ghostWeapon = null;
        foreach (var node in Actor.GetParent().GetChildren())
            if (node is Node2D ghost && ghost.Name.ToString().StartsWith($"{Actor.Name}残影"))
                foreach (var child in ghost.GetChildren())
                    if (child is Sprite2D part && part.Texture == Rig.WeaponPart.Texture) ghostWeapon = part;
        Check(ghostWeapon != null, "强化移动实际生成长刀残影");
        var viewport = new SubViewport { Size = new Vector2I(96,360), TransparentBg = true,
            Disable3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(viewport);
        var sprite = new Sprite2D { Texture = Rig.WeaponPart.Texture, Material = Rig.WeaponPart.Material,
            Centered = false, TextureFilter = CanvasItem.TextureFilterEnum.Nearest };
        viewport.AddChild(sprite);
        bool paused = GetTree().Paused;
        GetTree().Paused = true;
        try
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var normal = viewport.GetTexture().GetImage();
            sprite.Material = ghostWeapon.Material;
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var trail = viewport.GetTexture().GetImage();
            int visible = 0;
            float alphaError = 0;
            for (int y=0; y<normal.GetHeight(); y++)
                for (int x=0; x<normal.GetWidth(); x++)
                {
                    float body = normal.GetPixel(x,y).A, ghost = trail.GetPixel(x,y).A;
                    if (body > .1f || ghost > .1f) visible++;
                    // 透明目标上的 blend_add 用源 Alpha 加权 Alpha，读回值为 A²；先统一混合规则再比较轮廓。
                    alphaError = Mathf.Max(alphaError,Mathf.Abs(body * body - ghost));
                }
            trail.SavePng("res://Build/WeiYanV3/WeaponAfterimage.png");
            Check(visible > 500 && alphaError < .005f, $"长刀与实际残影轮廓一致：最大 Alpha 误差 {alphaError:F4}");
        }
        finally
        {
            GetTree().Paused = paused;
            viewport.QueueFree();
            Actor.SetMoveInput(Vector2.Zero);
        }
        await Frames(35);
    }

    private async Task CheckPause()
    {
        float time = Actor.VisualTime;
        var weapon = Rig.Joints[14].GlobalTransform;
        var foot = Foot(0).GlobalTransform;
        GetTree().Paused = true;
        await Frames(5);
        Check(Mathf.IsEqualApprox(time, Actor.VisualTime) && weapon.IsEqualApprox(Rig.Joints[14].GlobalTransform)
            && foot.IsEqualApprox(Foot(0).GlobalTransform), "暂停冻结动作时钟、武器及独立靴掌");
        GetTree().Paused = false;
    }

    private Vector2 WeaponDirection() => Vector2.Up.Rotated(Rig.Joints[14].Rotation);
    private Sprite2D Foot(int leg)
    {
        foreach (var part in Rig.DrawOrderedParts)
            if (part.Name == (leg == 0 ? "远靴掌图层" : "近靴掌图层")) return part;
        throw new InvalidOperationException("缺少独立靴掌");
    }

    private async Task Frames(int count)
    {
        for (int frame = 0; frame < count; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            // ProcessFrame 信号先于节点 _Process；图形运行等到提交完成，才能读取本次脚点和关节。
            if (Rendered) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            if (Rig.SpatialMotionActive)
            {
                // 空间转身会缩短投影长度，不能用平面骨段位置长度判断是否拉伸；专用回归检查真实三维骨长。
                if (Rig.SpatialMotionWeight > .99f)
                {
                    _primaryError = Mathf.Max(_primaryError, Rig.SpatialGripError);
                    if (Rig.SpatialFootError > .02f) throw new InvalidOperationException("空间动作足点未到位");
                }
                continue;
            }
            _primaryError = Mathf.Max(_primaryError, Rig.Skeleton.ToLocal(Rig.Joints[5].GlobalPosition).DistanceTo(Rig.ReferencePrimaryGrip));
            if (Rig.ReferenceGripWeight >= .9999f)
                _secondaryError = Mathf.Max(_secondaryError, Rig.Skeleton.ToLocal(Rig.Joints[8].GlobalPosition).DistanceTo(Rig.ReferenceSecondaryGrip));
            _fixedLengths &= Mathf.IsEqualApprox(Rig.Joints[10].Position.Length(), Rig.ThighLength)
                && Mathf.IsEqualApprox(Rig.Joints[12].Position.Length(), Rig.ThighLength);
            foreach (int joint in new[] { 9, 10, 11, 12 }) _fixedLengths &= Rig.Joints[joint].Scale.IsEqualApprox(Vector2.One);
            foreach (int upper in new[] { 3, 6 })
            {
                int lower = upper + 1, hand = upper + 2;
                float bend = Mathf.Abs(Mathf.RadToDeg(Rig.Joints[lower].Rotation));
                _minElbow = Mathf.Min(_minElbow, bend);
                if (bend > _maxElbow) { _maxElbow = bend; _tightElbowContext = $"{Actor.CurrentState}/{Actor.CurrentMotion} t={Actor.ActionTime:F3} 反击={Actor.CounterRemaining:F3} 臂={upper}"; }
                _wristBend = Mathf.Max(_wristBend, Mathf.Abs(Mathf.RadToDeg(Rig.Joints[hand].Rotation)));
                Vector2 shoulderPoint = Rig.Skeleton.ToLocal(Rig.Joints[upper].GlobalPosition);
                Vector2 elbowPoint = Rig.Skeleton.ToLocal(Rig.Joints[lower].GlobalPosition);
                Vector2 handPoint = Rig.Skeleton.ToLocal(Rig.Joints[hand].GlobalPosition);
                float bendSide = (elbowPoint - shoulderPoint).Cross(handPoint - elbowPoint);
                int branch = upper == 3 ? Actor.Presentation.Animation.FarElbowBend : Actor.Presentation.Animation.NearElbowBend;
                _elbowSidesCorrect &= bendSide * branch > 1;
                _fixedLengths &= Mathf.IsEqualApprox(Rig.Joints[lower].Position.Length(), Actor.Presentation.Model.JointPositions[lower].Length())
                    && Mathf.IsEqualApprox(Rig.Joints[hand].Position.Length(), Actor.Presentation.Model.JointPositions[hand].Length());
                var model = Actor.Presentation.Model;
                Vector2 ArtPin(int bone, int endpoint)
                {
                    var part = Rig.Parts[bone];
                    var texture = (AtlasTexture)part.Texture;
                    return Rig.Skeleton.ToLocal(part.ToGlobal(model.ArmAtlasAnchors[(bone - 3) * 2 + endpoint] - texture.Region.Position));
                }
                _armBindingError = Mathf.Max(_armBindingError, ArtPin(upper, 0).DistanceTo(Rig.Skeleton.ToLocal(Rig.Joints[upper].GlobalPosition)));
                _armBindingError = Mathf.Max(_armBindingError, ArtPin(upper, 1).DistanceTo(ArtPin(lower, 0)));
                _armBindingError = Mathf.Max(_armBindingError, ArtPin(lower, 1).DistanceTo(ArtPin(hand, 0)));
                _armBindingError = Mathf.Max(_armBindingError, ArtPin(hand, 1).DistanceTo(Rig.Skeleton.ToLocal(Rig.Joints[hand].GlobalPosition)));
            }
        }
    }

    /// <summary>读取实际渲染帧，跟随裁区保留刀尖和脚底；仅将预览放大，不改变实机比例。</summary>
    private async Task Capture(string name)
    {
        // 等待绘制仅用于读取当前帧，不允许截图开销悄悄推进动作，跳过短生效段或承重采样。
        bool paused = GetTree().Paused;
        GetTree().Paused = true;
        try
        {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        float scale = image.GetWidth() / 640f;
        using var detail = image.GetRegion(new Rect2I((int)((Actor.Position.X - 80) * scale),
            (int)((Actor.Position.Y - 110) * scale), (int)(160 * scale), (int)(135 * scale)));
        detail.Resize(480, 405, Image.Interpolation.Nearest);
        detail.SavePng($"res://Build/weiyan-frames/{name}.png");
        if (name is "idle" or "walk-0-012" or "Basic-018" or "Basic-025" or "Basic-030"
            or "Sweep-033" or "Sweep-039" or "Sweep-044" or "Dash-020") await CaptureArmDetail(name);
        }
        finally { GetTree().Paused = paused; }
    }

    /// <summary>重绘当前真实拆件变换到放大视口，检查肩肘腕接缝；不放大低分辨率截图。</summary>
    private async Task CaptureArmDetail(string name)
    {
        var viewport = new SubViewport { Size = new Vector2I(720, 560), TransparentBg = true,
            Disable3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(viewport);
        var root = new Node2D { Position = new Vector2(330, 470), Scale = new Vector2(4, 3.68f) };
        viewport.AddChild(root);
        foreach (var part in Rig.DrawOrderedParts)
        {
            if (!part.Visible) continue;
            var copy = new Sprite2D { Texture = part.Texture, Material = part.Material, Centered = part.Centered,
                FlipH = part.FlipH, FlipV = part.FlipV, TextureFilter = part.TextureFilter };
            root.AddChild(copy);
            copy.Transform = Rig.Skeleton.GlobalTransform.AffineInverse() * part.GlobalTransform;
        }
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = viewport.GetTexture().GetImage();
        image.SavePng($"res://Build/weiyan-frames/detail-{name}.png");
        viewport.QueueFree();
    }

    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
        GD.Print($"通过：{message}");
    }
}
