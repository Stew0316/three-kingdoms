using Godot;
using System;

/// <summary>单张立绘的二维骨骼网格原型。只读取战斗状态，不参与碰撞或伤害。</summary>
public partial class CharacterRig : Node2D
{
    // 原图像素到世界单位的比例；0.05 是之前 0.074 的约 68%。
    [Export] public float ArtScale { get; set; } = .05f;
    // 小幅动作强度。整张图尚未拆层，不宜放大到翻身、抡戟的幅度。
    [Export(PropertyHint.Range, GodotResourceNames.MotionStrengthRange)] public float MotionStrength { get; set; } = 1f;
    // 是否在人物上方绘制骨骼连线，由 HUD 的 F4 开关控制。
    public bool ShowBones { get; set; }
    // 骨骼根节点，统一管理骨骼索引和静止姿态。
    public Skeleton2D Skeleton { get; private set; }
    // 带原图 UV 与骨骼权重的网格，实际显示变形后的人物。
    public Polygon2D Skin { get; private set; }
    // 手动采样的动画播放器，不自行推进时间，避免与战斗时钟漂移。
    public AnimationPlayer Animator { get; private set; }
    private Combatant _actor; // 提供只读状态、朝向和施法时间。
    private Sprite2D _source; // 保留原图节点作资源入口，运行时隐藏其绘制。
    private readonly Bone2D[] _bones = new Bone2D[CharacterBoneNames.Count]; // 腰、胸、头、双臂、双腿、披风。
    // 各骨骼的父骨索引，与 _bones 顺序对应；-1 表示直接挂在 Skeleton 下。
    private readonly int[] _parents = { -1, 0, 1, 1, 1, 0, 0, 1 };
    private string _clip = ""; // 当前语义动作对应的动画名。
    private Node2D _boneOverlay; // 位于蒙皮上方的骨骼调试绘制层。

    /// <summary>依据宿主原图建立骨骼、蒙皮、动画与调试层；成功后隐藏原静态 Sprite。</summary>
    public override void _Ready()
    {
        _actor = GetParent<Combatant>();
        _source = _actor.GetNode<Sprite2D>(NodeNames.Visual);
        if (_source.Texture == null) return;
        BuildSkeleton();
        BuildSkin();
        BuildAnimations();
        _boneOverlay = new Node2D { Name = NodeNames.BoneOverlay, ZIndex = PresentationZIndexes.BoneOverlay };
        AddChild(_boneOverlay);
        _boneOverlay.Draw += DrawBones;
        _source.Visible = false;
    }

    /// <summary>按原图关节坐标建立父子链，并将初始变换保存为静止姿态。</summary>
    private void BuildSkeleton()
    {
        Skeleton = new Skeleton2D { Name = NodeNames.Skeleton };
        AddChild(Skeleton);
        // 各原图独立的关节位置（归一化坐标），不能用同一套坐标硬套两张立绘。
        Vector2[] anchors = _actor.IsEnemy
            ? new[] { new Vector2(.56f,.62f), new(.56f,.42f), new(.51f,.25f), new(.40f,.40f), new(.76f,.41f), new(.40f,.72f), new(.68f,.73f), new(.77f,.49f) }
            : new[] { new Vector2(.48f,.62f), new(.49f,.43f), new(.56f,.28f), new(.31f,.42f), new(.65f,.45f), new(.35f,.73f), new(.62f,.76f), new(.28f,.51f) };
        string[] names = CharacterBoneNames.CreateOrderedNames();
        Vector2 textureSize = _source.Texture.GetSize();
        for (int i = 0; i < _bones.Length; i++)
        {
            // 原图中的 (512,1450) 是脚底锚点；这里得到的是 Rig 局部世界单位坐标，并非 GlobalPosition。
            Vector2 worldPoint = (anchors[i] * textureSize - new Vector2(512, 1450)) * ArtScale;
            // 子骨骼的位置必须相对父骨骼，不能直接使用整张图的坐标。
            Vector2 parentPoint = _parents[i] < 0 ? Vector2.Zero
                : (anchors[_parents[i]] * textureSize - new Vector2(512, 1450)) * ArtScale;
            var bone = new Bone2D { Name = names[i], Position = worldPoint - parentPoint };
            bone.SetAutocalculateLengthAndAngle(false);
            bone.SetLength(i == 0 ? 14 : 9);
            // 蒙皮依赖非零 Rest 变换；后续旋转以这套静止姿态为基准。
            bone.Rest = bone.Transform;
            Node parent = _parents[i] < 0 ? Skeleton : _bones[_parents[i]];
            parent.AddChild(bone);
            _bones[i] = bone;
        }
    }

    /// <summary>生成规则三角网格，分配纹理坐标及最多四骨骼影响的归一化权重。</summary>
    private void BuildSkin()
    {
        // 网格分为 12×18 个矩形单元，每格拆成两个三角形。
        const int columns = 12, rows = 18;
        // 各顶点在 Rig 局部空间中的位置，单位与场景世界像素一致。
        var points = new Vector2[(columns + 1) * (rows + 1)];
        // UV 使用原图像素坐标，而非 0～1 坐标；每个顶点对应一个纹理采样位置。
        var uv = new Vector2[points.Length];
        // weights[骨骼索引][顶点索引]：该骨骼对该顶点的影响比例。
        var weights = new float[CharacterBoneNames.Count][];
        for (int i = 0; i < CharacterBoneNames.Count; i++) weights[i] = new float[points.Length];
        for (int y = 0; y <= rows; y++)
        for (int x = 0; x <= columns; x++)
        {
            int vertex = y * (columns + 1) + x;
            uv[vertex] = new Vector2(x / (float)columns, y / (float)rows) * _source.Texture.GetSize();
            points[vertex] = (uv[vertex] - new Vector2(512, 1450)) * ArtScale;
            // 每顶点只取最近四根骨骼，归一化后送入 Godot GPU 蒙皮。
            // 距离排序后同时保留原骨骼索引，选最近四根而不打乱骨骼与权重的对应关系。
            var distance = new float[CharacterBoneNames.Count];
            var indices = new int[CharacterBoneNames.Count];
            for (int b = 0; b < CharacterBoneNames.Count; b++)
            {
                Vector2 bonePoint = Skeleton.ToLocal(_bones[b].GlobalPosition);
                distance[b] = points[vertex].DistanceSquaredTo(bonePoint);
                indices[b] = b;
            }
            Array.Sort(distance, indices);
            // 累加未归一化权重；距离项加 5 防止顶点恰好落在关节上时除零或权重突变。
            float total = 0;
            for (int n = 0; n < 4; n++) { float weight = 1f / Mathf.Pow(distance[n] + 5, 2); weights[indices[n]][vertex] = weight; total += weight; }
            for (int n = 0; n < 4; n++) weights[indices[n]][vertex] /= total;
        }
        Skin = new Polygon2D { Name = NodeNames.Skin, Texture = _source.Texture, Polygon = points, UV = uv,
            TextureFilter = TextureFilterEnum.Linear, Skeleton = new NodePath(SceneNodePaths.SkinToSkeleton) };
        // 每组三个索引引用 points 中的顶点，共同指定网格拓扑。
        var triangles = new Godot.Collections.Array();
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < columns; x++)
        {
            // a/b 是单元上方两点，c/d 是下方两点；沿 a-d 对角线拆成两个三角形。
            int a = y * (columns + 1) + x, b = a + 1, c = a + columns + 1, d = c + 1;
            triangles.Add(new[] { a, b, d }); triangles.Add(new[] { a, d, c });
        }
        Skin.Polygons = triangles;
        AddChild(Skin);
        for (int b = 0; b < CharacterBoneNames.Count; b++) Skin.AddBone(Skeleton.GetPathTo(_bones[b]), weights[b]);
    }

    /// <summary>建立各语义动作的旋转轨道；当前小幅曲线适用于尚未拆层的整张立绘。</summary>
    private void BuildAnimations()
    {
        Animator = new AnimationPlayer { Name = NodeNames.AnimationPlayer, CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual };
        AddChild(Animator);
        var library = new AnimationLibrary();
        AddClip(library, CharacterAnimations.Idle, new[] { 0f,.025f,-.02f,.015f,-.015f,0f,0f,.07f });
        AddClip(library, CharacterAnimations.Move, new[] { .025f,-.045f,.03f,.09f,-.09f,.13f,-.13f,.11f });
        AddClip(library, CharacterAnimations.Attack, new[] { -.06f,-.13f,.05f,-.19f,-.22f,.04f,-.03f,.16f });
        AddClip(library, CharacterAnimations.Dash, new[] { .035f,.08f,-.025f,.10f,.10f,-.07f,.07f,-.18f });
        AddClip(library, CharacterAnimations.Hurt, new[] { -.07f,-.11f,.07f,.10f,-.08f,.03f,-.03f,.12f });
        AddClip(library, CharacterAnimations.Dead, new float[CharacterBoneNames.Count]);
        Animator.AddAnimationLibrary(GodotResourceNames.DefaultAnimationLibrary, library);
    }

    /// <summary>为八根骨骼分别生成四个旋转关键帧，并添加到动画库。</summary>
    /// <param name="library">存放动画资源的库。</param>
    /// <param name="name">动作名称，供状态到动画的映射使用。</param>
    /// <param name="angles">与 _bones 顺序一致的八个最大偏转角，单位为弧度。</param>
    private void AddClip(AnimationLibrary library, string name, float[] angles)
    {
        // 用 1 秒作为归一化时间轴；实际播放位置由战斗阶段映射，不表示所有动作都持续 1 秒。
        var animation = new Animation { Length = 1 };
        for (int i = 0; i < CharacterBoneNames.Count; i++)
        {
            int track = animation.AddTrack(Animation.TrackType.Value);
            animation.TrackSetPath(track, new NodePath($"{GetPathTo(_bones[i])}:{GodotPropertyNames.Rotation}"));
            float angle = angles[i] * MotionStrength;
            animation.TrackInsertKey(track, 0, 0f);
            animation.TrackInsertKey(track, .28, angle);
            animation.TrackInsertKey(track, .57, -angle * .7f);
            animation.TrackInsertKey(track, 1, 0f);
        }
        library.AddAnimation(name, animation);
    }

    /// <summary>读取状态并采样骨骼姿态；delta 不用于计时，统一使用角色的动作时钟。</summary>
    public override void _Process(double delta)
    {
        if (Animator == null) return;
        // 语义状态映射到动画名称；闪避暂时复用突进姿态。
        string next = _actor.CurrentState switch
        {
            Combatant.State.Move => CharacterAnimations.Move,
            Combatant.State.Hurt => CharacterAnimations.Hurt,
            Combatant.State.Dead => CharacterAnimations.Dead,
            Combatant.State.Dodge => CharacterAnimations.Dash,
            Combatant.State.Attack => _actor.CurrentAction == CombatAction.Dash ? CharacterAnimations.Dash : CharacterAnimations.Attack,
            _ => CharacterAnimations.Idle
        };
        if (_clip != next) { _clip = next; Animator.Play(next); }
        // 归一化采样位置；行走周期为 0.52 秒，其余循环姿态当前使用 1.8 秒。
        float phase = (_actor.VisualTime / (_clip == CharacterAnimations.Move ? .52f : 1.8f)) % 1;
        if (_actor.CurrentState is Combatant.State.Attack or Combatant.State.Dodge)
        {
            // 将三段规则时间映射到相应关键帧，魏延较长的蓄力不会提前挥完。
            float time = _actor.ActionTime;
            var spec = _actor.Spec;
            phase = time < spec.Prepare ? .28f * time / Mathf.Max(spec.Prepare, .001f)
                : time < spec.Prepare + spec.Active ? .28f + .29f * (time - spec.Prepare) / spec.Active
                : .57f + .43f * (time - spec.Prepare - spec.Active) / spec.Recover;
            phase = Mathf.Clamp(phase, 0, 1);
        }
        Animator.Seek(phase, true);
        Scale = new Vector2(_source.FlipH ? -1 : 1, 1);
        Modulate = _source.Modulate;
        Rotation = _actor.IsDead ? _source.Rotation : 0;
        Position = _actor.IsDead ? new Vector2(0, -8) : Vector2.Zero;
        _boneOverlay.QueueRedraw();
    }

    /// <summary>在独立上层绘制关节点和父子连线，仅用于查看绑定关系。</summary>
    private void DrawBones()
    {
        if (!ShowBones || Skeleton == null) return;
        for (int i = 0; i < _bones.Length; i++)
        {
            Vector2 point = ToLocal(_bones[i].GlobalPosition);
            if (_parents[i] >= 0) _boneOverlay.DrawLine(ToLocal(_bones[_parents[i]].GlobalPosition), point, new Color(PresentationColors.BoneDebug), 1, true);
            _boneOverlay.DrawCircle(point, 1.6f, Colors.White);
        }
    }
}
