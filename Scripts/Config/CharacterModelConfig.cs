using Godot;

/// <summary>武将独立的模型、骨架绑定和贴图显示参数；不依赖技能或输入槽位。</summary>
[GlobalClass]
public partial class CharacterModelConfig : Resource
{
    [Export] public Texture2D PartsAtlas { get; set; } // 4×4 拆件图集，顺序见资源 README。
    [Export] public Godot.Collections.Array<Rect2> AtlasRegions { get; set; } = new();
    [Export] public Rect2 CrestRect { get; set; } = new(-7,-27,19,30); // 冠饰相对冠顶骨的显示范围，适配上扬雉翎或下垂盔缨。
    [Export] public bool FlipCrestVertical { get; set; } // 图集冠翎根部位于上端时翻转采样，让根部靠近冠顶。
    [Export] public Texture2D ReferenceAccessories { get; set; } // 参考三姿态原画生成的长戟和双翎配件。
    [Export] public Rect2 ReferenceWeaponRegion { get; set; } // 配件图中的完整武器区域。
    [Export] public Rect2 ReferenceCrestRegion { get; set; } // 配件图中的双翎区域。
    [Export] public Vector2 ReferenceCrestRoot { get; set; } // 双翎根部在采样区域内的像素坐标。
    [Export] public bool CalibratedLegProportions { get; set; } // 使用独立校准的髋点与固定腿长；只对选定角色启用。
    [Export(PropertyHint.Range, "28,44,0.5")] public float PelvisHeight { get; set; } = 36; // 腰骨离角色脚点的高度，骨架单位。
    [Export(PropertyHint.Range, "3,7,0.25")] public float HipHalfWidth { get; set; } = 4.5f; // 左右髋关节离骨盆中线的距离，不等于脚距。
    [Export(PropertyHint.Range, "12,22,0.5")] public float ThighLength { get; set; } = 17; // 固定髋到膝长度，动作中禁止拉伸。
    [Export(PropertyHint.Range, "12,22,0.5")] public float LowerLegLength { get; set; } = 18; // 固定膝到靴底长度，动作中禁止压缩。
    [Export] public bool AlignBootsForward { get; set; } // 校正近侧小腿贴图：双脚同向，整个人物镜像时一起转向。
    [Export] public bool FlipFarBootHorizontal { get; set; } // 魏延 v2 远靴原图朝左，单独翻转远小腿和靴掌使其朝前。
    [Export(PropertyHint.Range, "0.025,0.06,0.001")] public float CharacterScale { get; set; } = .038f; // 二维世界中的角色整体尺寸，不影响碰撞和技能范围。
    [Export(PropertyHint.Range, "0.75,1,0.01")] public float DepthScale { get; set; } = .92f; // 轻微压缩纵向比例，使正面拆件更接近俯视小人而非卡牌立绘。
    [Export(PropertyHint.Range, "6,16,0.5")] public float SupportGripDistance { get; set; } = 10; // 远手沿武器向上握持的距离，骨架单位；避免两只手重叠成一团。
    [Export] public bool CartoonFilter { get; set; } = true; // 对现有写实拆件执行轻量色阶化与描边，替换美术时可关闭。
    [Export(PropertyHint.Range, "3,12,1")] public float CartoonColorSteps { get; set; } = 7; // 每个颜色通道保留的近似色阶数。
    [Export(PropertyHint.Range, "0.8,1.5,0.05")] public float CartoonSaturation { get; set; } = 1.15f; // 小尺寸下的色彩区分度。
    [Export] public Color CartoonOutlineColor { get; set; } = new(.10f,.08f,.07f,.92f); // 拆件透明边缘的一像素深色轮廓。
    [Export] public Godot.Collections.Array<Vector2> JointPositions { get; set; } = new(); // 16个业务关节的父节点局部绑定位置。
    [Export] public Godot.Collections.Array<Rect2> PartRects { get; set; } = new(); // 16个拆件在各自关节坐标中的显示范围。
    [Export] public Godot.Collections.Array<int> DrawOrder { get; set; } = new(); // 从后到前绘制的业务关节编号。
    [Export] public bool CrestVisible { get; set; } = true; // 头图已自带完整盔缨时关闭重复冠饰。
    [Export] public Vector2 AccessoryWeaponSize { get; set; } = new(13, 86); // 独立配件武器显示尺寸。
    [Export] public Vector2 AccessoryWeaponOffset { get; set; } = new(-7.4f, -42); // 独立武器相对主握点偏移。
    [Export] public Vector2 AccessoryCrestSize { get; set; } = new(35, 30); // 独立冠饰显示尺寸。
}
