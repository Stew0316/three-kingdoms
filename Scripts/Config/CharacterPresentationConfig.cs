using Godot;

/// <summary>角色表现参数，特效关闭或修改不影响战斗判定。</summary>
[GlobalClass]
public partial class CharacterPresentationConfig : Resource
{
    [Export] public Texture2D PartsAtlas { get; set; } // 4×4 拆件图集，顺序见资源 README。
    // 按图集从左至右、从上至下的 16 格顺序指定采样区域；空数组时使用等分网格。
    [Export] public Godot.Collections.Array<Rect2> AtlasRegions { get; set; } = new();
    [Export] public Rect2 CrestRect { get; set; } = new(-7,-27,19,30); // 冠饰相对冠顶骨的显示范围，适配上扬雉翎或下垂盔缨。
    [Export] public bool FlipCrestVertical { get; set; } // 图集冠翎根部位于上端时翻转采样，让根部靠近冠顶。
    [Export] public Color AuraColor { get; set; } = new("ffb34f");
    [Export] public Color ParticleColor { get; set; } = new("ffe5ad");
    [Export] public bool OrbitParticles { get; set; } // true 为环绕叶片，false 为上升火星。
    [Export] public bool Afterimages { get; set; } // 移动时采样当前骨骼姿态。
    [Export] public Color AfterimageColor { get; set; } = new(.2f, .55f, 1f, .42f);
    [Export] public float AfterimageInterval { get; set; } = .075f;
    [Export] public float AfterimageLifetime { get; set; } = .32f;
    [Export] public float AuraRadius { get; set; } = 21;
    [Export] public int ParticleCount { get; set; } = 24;
    [Export] public float MotionStrength { get; set; } = 1;
    [ExportGroup("参考图关键姿态")]
    [Export] public bool ReferencePoseSet { get; set; } // 使用长兵器关键姿态和独立持械约束，替代通用角度公式。
    [Export] public CharacterPoseConfig StandingPose { get; set; } // 双手在胸腰之间低持长戟警戒。
    [Export] public CharacterPoseConfig MovingPose { get; set; } // 腰侧单手近水平拖戟行走。
    [Export] public CharacterPoseConfig WindupPose { get; set; } // 双手收戟至身后，沉胯蓄势。
    [Export] public CharacterPoseConfig StrikeHighPose { get; set; } // 挥击弧线经过上方的关键姿态。
    [Export] public CharacterPoseConfig StrikeContactPose { get; set; } // 戟刃挥到前方的接触关键姿态。
    [Export] public CharacterPoseConfig StrikeFollowPose { get; set; } // 扫击后的下压随势，不向前投掷武器。
    [Export] public Texture2D ReferenceAccessories { get; set; } // 参考三姿态原画生成的长戟和双翎配件。
    [Export] public Rect2 ReferenceWeaponRegion { get; set; } // 配件图中的完整武器区域。
    [Export] public Rect2 ReferenceCrestRegion { get; set; } // 配件图中的双翎区域。
    [Export] public Vector2 ReferenceCrestRoot { get; set; } // 双翎根部在采样区域内的像素坐标。
    [ExportGroup("人体比例")]
    [Export] public bool CalibratedLegProportions { get; set; } // 使用独立校准的髋点与固定腿长；只对选定角色启用。
    [Export(PropertyHint.Range, "28,44,0.5")] public float PelvisHeight { get; set; } = 36; // 腰骨离角色脚点的高度，骨架单位。
    [Export(PropertyHint.Range, "3,7,0.25")] public float HipHalfWidth { get; set; } = 4.5f; // 左右髋关节离骨盆中线的距离，不等于脚距。
    [Export(PropertyHint.Range, "12,22,0.5")] public float ThighLength { get; set; } = 17; // 固定髋到膝长度，动作中禁止拉伸。
    [Export(PropertyHint.Range, "12,22,0.5")] public float LowerLegLength { get; set; } = 18; // 固定膝到靴底长度，动作中禁止压缩。
    [ExportGroup("武将步态")]
    [Export] public bool MartialWalk { get; set; } // 开启路程驱动的武将步态，参考姿态分支由承重腿决定腰高。
    [Export(PropertyHint.Range, "4,16,0.5")] public float StandStanceWidth { get; set; } = 10; // 站立警戒时的半脚距，独立于移动步幅。
    [Export(PropertyHint.Range, "0,5,0.25")] public float StandCrouch { get; set; } = .75f; // 站立时轻微屈膝，移动时再降低重心。
    [Export(PropertyHint.Range, "4,16,0.5")] public float WalkStanceWidth { get; set; } = 12; // 两脚距身体中线的距离，骨架单位。
    [Export(PropertyHint.Range, "0,5,0.25")] public float WalkCrouch { get; set; } = 2.5f; // 腰胯下沉量，脚底仍停留在地面。
    [Export(PropertyHint.Range, "32,100,1")] public float WalkCycleDistance { get; set; } = 72; // 完成左右各一步所需的实际移动距离，世界像素。
    [Export(PropertyHint.Range, "1,16,0.25")] public float WalkStride { get; set; } = 4.5f; // 单脚前后移动的半幅，骨架单位。
    [Export(PropertyHint.Range, "0,5,0.25")] public float WalkFootLift { get; set; } = 2; // 摆动脚离地高度，避免高抬腿和蹦跳。
    [Export(PropertyHint.Range, "10,35,1")] public float WalkSupportKneeDegrees { get; set; } = 24; // 参考步态的承重膝弯角，0 为完全伸直；摆动腿由足点单独求解。
    [Export] public bool AlignBootsForward { get; set; } // 校正近侧小腿贴图：双脚同向，整个人物镜像时一起转向。
    [ExportGroup("外观")]
    [Export(PropertyHint.Range, "0.025,0.06,0.001")] public float CharacterScale { get; set; } = .038f; // 二维世界中的角色整体尺寸，不影响碰撞和技能范围。
    [Export(PropertyHint.Range, "0.75,1,0.01")] public float DepthScale { get; set; } = .92f; // 轻微压缩纵向比例，使正面拆件更接近俯视小人而非卡牌立绘。
    [Export(PropertyHint.Range, "6,16,0.5")] public float SupportGripDistance { get; set; } = 10; // 远手沿武器向上握持的距离，骨架单位；避免两只手重叠成一团。
    [Export] public bool CartoonFilter { get; set; } = true; // 对现有写实拆件执行轻量色阶化与描边，替换美术时可关闭。
    [Export(PropertyHint.Range, "3,12,1")] public float CartoonColorSteps { get; set; } = 7; // 每个颜色通道保留的近似色阶数。
    [Export(PropertyHint.Range, "0.8,1.5,0.05")] public float CartoonSaturation { get; set; } = 1.15f; // 小尺寸下的色彩区分度。
    [Export] public Color CartoonOutlineColor { get; set; } = new(.10f,.08f,.07f,.92f); // 拆件透明边缘的一像素深色轮廓。
}
