using Godot;

/// <summary>每匹马独立外观，不包含步态和移速，避免换装改变移动规则。</summary>
[GlobalClass]
public partial class HorseAppearance : Resource
{
    [Export] public Texture2D PartsAtlas { get; set; } // 独立彩绘拆件原图，只读绑定，不承载运动参数。
    [Export] public Color Coat { get; set; } = new("98583e"); // 历史配色参考；正式毛皮颜色来自PartsAtlas。
    [Export] public Color Mane { get; set; } = new("302b29"); // 历史配色参考；正式鬃尾和蹄色来自PartsAtlas。
    [Export] public Color Blanket { get; set; } = new("824333"); // 历史配色参考；正式鞍毯颜色来自PartsAtlas。
    [Export] public Color Trim { get; set; } = new("c49d60"); // 鞍毯包边。
    [Export] public float BodyWidth { get; set; } = 1; // 躯干宽度，不缩放四肢或碰撞。
    [Export] public bool Dappled { get; set; } // 臀部少量大块斑纹。
    [Export] public bool LongMane { get; set; } = true; // 长披鬃或短竖鬃。
    [Export] public bool FaceBlaze { get; set; } = true; // 原图白额白袜特征记录；不对彩绘纹理二次着色。
    [Export] public bool SaddleBags { get; set; } = true; // 行旅皮囊或轻装尖角鞍毯。
}
