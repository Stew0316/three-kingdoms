using Godot;

/// <summary>武将独立的模型、骨架绑定和贴图显示参数；不依赖技能或输入槽位。</summary>
[GlobalClass]
public partial class CharacterModelConfig : Resource
{
    [Export] public Texture2D PartsAtlas { get; set; } // 4×4 拆件图集，顺序见资源 README。
    [Export] public bool DualWeapons { get; set; } // 双持模型启用独立副武器，不使用长柄双手握点约束。
    [Export] public Rect2 OffhandWeaponRegion { get; set; } // 副武器在自身 PartsAtlas 内的采样区。
    [Export] public Vector2 OffhandWeaponSize { get; set; } = new(5, 37); // 副武器显示尺寸，单位为骨架坐标。
    [Export] public float OffhandBladeDistance { get; set; } = 30; // 副手握心到剑尖的距离。
    [Export] public Texture2D HeadTexture { get; set; } // 可选独立头部原图；绑定到头部 PartRects，未设置时使用图集头部。
    [Export] public Rect2 HeadRegion { get; set; } // 独立头图的有效采样区域；空矩形时自动取透明包围盒，显式设置可排除近透明噪点。
    [Export] public Godot.Collections.Array<Rect2> AtlasRegions { get; set; } = new();
    [Export] public Vector2[] WeaponAtlasClipPolygon { get; set; } = System.Array.Empty<Vector2>(); // 原图像素坐标中的顺时针凸多边形；3～16点，仅武器采样保留内部，空数组不裁剪。
    [Export] public Rect2 CrestRect { get; set; } = new(-7,-27,19,30); // 冠饰相对冠顶骨的显示范围，适配上扬雉翎或下垂盔缨。
    [Export] public bool FlipCrestVertical { get; set; } // 图集冠翎根部位于上端时翻转采样，让根部靠近冠顶。
    [Export] public Texture2D ReferenceAccessories { get; set; } // 参考三姿态原画生成的长戟和双翎配件。
    [Export] public Rect2 ReferenceWeaponRegion { get; set; } // 配件图中的完整武器区域。
    [Export] public Rect2 ReferenceCrestRegion { get; set; } // 配件图中的双翎区域。
    [Export] public Vector2 ReferenceCrestRoot { get; set; } // 双翎根部在采样区域内的像素坐标。
    [Export] public bool CalibratedLegProportions { get; set; } // 使用独立校准的髋点与固定腿长；只对选定角色启用。
    [Export(PropertyHint.Range, "28,52,0.5")] public float PelvisHeight { get; set; } = 36; // 腰骨离角色脚点的高度，骨架单位。
    [Export(PropertyHint.Range, "3,7,0.25")] public float HipHalfWidth { get; set; } = 4.5f; // 左右髋关节离骨盆中线的距离，不等于脚距。
    [Export(PropertyHint.Range, "12,30,0.5")] public float ThighLength { get; set; } = 17; // 固定髋到膝长度，动作中禁止拉伸。
    [Export(PropertyHint.Range, "12,28,0.5")] public float LowerLegLength { get; set; } = 18; // 固定膝到靴底长度，动作中禁止压缩。
    [Export(PropertyHint.Enum, "反向:-1,正向:1")] public int FarKneeBend { get; set; } = 1; // 后侧腿的固定屈膝支路，匹配拆件的膝面朝向；整体镜像不改变此值。
    [Export] public bool AlignBootsForward { get; set; } // 校正近侧小腿贴图：双脚同向，整个人物镜像时一起转向。
    [Export] public bool FlipFarBootHorizontal { get; set; } // 魏延 v2 远靴原图朝左，单独翻转远小腿和靴掌使其朝前。
    [Export(PropertyHint.Range, "0.025,0.06,0.001")] public float CharacterScale { get; set; } = .038f; // 旧世界尺度兼容值；正式角色改由 TargetScreenPixels 锁定屏幕显示尺寸。
    [Export(PropertyHint.Range, "120,260,1")] public float TargetScreenPixels { get; set; } = 200; // 人物连同武器的目标屏幕高度；镜头缩放、窗口缩放时反向补偿，不影响碰撞与技能范围。
    [Export(PropertyHint.Range, "0.75,1,0.01")] public float DepthScale { get; set; } = .92f; // 轻微压缩纵向比例，使正面拆件更接近俯视小人而非卡牌立绘。
    [Export(PropertyHint.Range, "6,16,0.5")] public float SupportGripDistance { get; set; } = 10; // 远手沿武器向上握持的距离，骨架单位；避免两只手重叠成一团。
    [Export] public bool CartoonFilter { get; set; } = true; // 对现有写实拆件执行轻量色阶化与描边，替换美术时可关闭。
    [Export(PropertyHint.Range, "3,12,1")] public float CartoonColorSteps { get; set; } = 7; // 每个颜色通道保留的近似色阶数。
    [Export(PropertyHint.Range, "0.8,1.5,0.05")] public float CartoonSaturation { get; set; } = 1.15f; // 小尺寸下的色彩区分度。
    [Export] public Color CartoonOutlineColor { get; set; } = new(.10f,.08f,.07f,.92f); // 拆件透明边缘的一像素深色轮廓。
    [Export] public Godot.Collections.Array<Vector2> JointPositions { get; set; } = new(); // 16个业务关节的父节点局部绑定位置。
    [Export] public Godot.Collections.Array<Rect2> PartRects { get; set; } = new(); // 16个拆件在各自关节坐标中的显示范围。
    [Export] public Vector2[] ArmAtlasAnchors { get; set; } = System.Array.Empty<Vector2>(); // 远上臂/前臂/手、近上臂/前臂/手各两点，原图中的肩肘、肘腕、腕掌轴线；共12点，空数组沿用矩形绑定。
    [Export] public Vector2[] ThighAtlasAnchors { get; set; } = System.Array.Empty<Vector2>(); // 远、近大腿各两个髋膝锚点；对齐原图倾斜的腿甲，保持等比采样。
    [Export] public float PalmInset { get; set; } = 2.5f; // 握点向腕部回退的骨架距离；前臂到握点的骨链包含这一段手掌。
    [Export] public Godot.Collections.Array<int> DrawOrder { get; set; } = new(); // 从后到前绘制的业务关节编号。
    [Export] public bool CrestVisible { get; set; } = true; // 头图已自带完整盔缨时关闭重复冠饰。
    [Export] public Vector2 AccessoryWeaponSize { get; set; } = new(13, 86); // 独立配件武器显示尺寸。
    [Export] public Vector2 AccessoryWeaponOffset { get; set; } = new(-7.4f, -42); // 独立武器相对主握点偏移。
    [Export] public Vector2 AccessoryCrestSize { get; set; } = new(35, 30); // 独立冠饰显示尺寸。
    [Export] public bool MovementCloth { get; set; } // 仅选定模型启用移动驱动的冠翎、披风 Shader；静止收稳后完全停止。
    [Export] public Vector2 ClothAmplitudes { get; set; } = new(3, 4); // 冠翎、披风自由端位移上限，骨架单位；固定根部不变形。
    [Export] public Vector2[] ClothCapeClipPolygon { get; set; } = System.Array.Empty<Vector2>(); // 可选披风原图凸采样区，排除邻格；只在布料材质启用时使用，3～16点。
    [Export] public Color ArmorTint { get; set; } = Colors.White; // 衣甲专用材质色调，不染脸、手、冠翎或武器。
    [Export] public float BootWidth { get; set; } = 10; // 独立靴掌宽度，与小腿美术宽度一并校准。
    [Export] public bool ProceduralCrests { get; set; } // 使用代码生成的独立羽带网格，不采样冠翎图片。
    [Export] public Godot.Collections.Array<Vector2> CrestCurves { get; set; } = new(); // 按冠翎等分，每条 3n+1 点：根部后每三点为一段贝塞尔控制点与末点，冠骨局部坐标。
    [Export] public Godot.Collections.Array<Vector4> CrestProfiles { get; set; } = new(); // 每条的宽度、频率、摆幅、相位；各自独立。
    [Export] public float FootHeight { get; set; } = 4; // 踝至靴底高度，必须小于小腿长度。
    [Export] public float BootSplit { get; set; } = .8f; // 小腿图中的踝部纵向比例，以下为独立靴掌。
}
