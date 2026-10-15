using Godot;

/// <summary>每匹马独立外观，不包含步态和移速，避免换装改变移动规则。</summary>
[GlobalClass]
public partial class HorseAppearance : Resource
{
    [Export] public Color Coat { get; set; } = new("98583e"); // 主毛色。
    [Export] public Color Mane { get; set; } = new("302b29"); // 鬃尾和蹄色。
    [Export] public Color Blanket { get; set; } = new("824333"); // 鞍毯主色。
    [Export] public Color Trim { get; set; } = new("c49d60"); // 鞍毯包边。
    [Export] public float BodyWidth { get; set; } = 1; // 躯干宽度，不缩放四肢或碰撞。
    [Export] public bool Dappled { get; set; } // 臀部少量大块斑纹。
    [Export] public bool LongMane { get; set; } = true; // 长披鬃或短竖鬃。
    [Export] public bool FaceBlaze { get; set; } = true; // 额鼻白流星及近侧白袜。
    [Export] public bool SaddleBags { get; set; } = true; // 行旅皮囊或轻装尖角鞍毯。
}
