using Godot;

/// <summary>角色表现参数，特效关闭或修改不影响战斗判定。</summary>
[GlobalClass]
public partial class CharacterPresentationConfig : Resource
{
    [Export] public Texture2D PartsAtlas { get; set; } // 4×4 拆件图集，顺序见资源 README。
    // 按图集从左至右、从上至下的 16 格顺序指定采样区域；空数组时使用等分网格。
    [Export] public Godot.Collections.Array<Rect2> AtlasRegions { get; set; } = new();
    [Export] public Rect2 CrestRect { get; set; } = new(-7,-27,19,30); // 冠饰相对冠顶骨的显示范围，适配上扬雉翎或下垂盔缨。
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
    [Export(PropertyHint.Range, "0.025,0.06,0.001")] public float CharacterScale { get; set; } = .038f; // 二维世界中的角色整体尺寸，不影响碰撞和技能范围。
    [Export(PropertyHint.Range, "0.75,1,0.01")] public float DepthScale { get; set; } = .92f; // 轻微压缩纵向比例，使正面拆件更接近俯视小人而非卡牌立绘。
    [Export(PropertyHint.Range, "6,16,0.5")] public float SupportGripDistance { get; set; } = 10; // 远手沿武器向上握持的距离，骨架单位；避免两只手重叠成一团。
    [Export] public bool CartoonFilter { get; set; } = true; // 对现有写实拆件执行轻量色阶化与描边，替换美术时可关闭。
    [Export(PropertyHint.Range, "3,12,1")] public float CartoonColorSteps { get; set; } = 7; // 每个颜色通道保留的近似色阶数。
    [Export(PropertyHint.Range, "0.8,1.5,0.05")] public float CartoonSaturation { get; set; } = 1.15f; // 小尺寸下的色彩区分度。
    [Export] public Color CartoonOutlineColor { get; set; } = new(.10f,.08f,.07f,.92f); // 拆件透明边缘的一像素深色轮廓。
}
