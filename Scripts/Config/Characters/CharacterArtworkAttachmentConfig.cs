using Godot;

/// <summary>原图中的独立附属层；挂点、遮挡与柔性参数归模型，实例只保存变换和材质状态。</summary>
[GlobalClass]
public partial class CharacterArtworkAttachmentConfig : Resource
{
    [Export] public string LayerName { get; set; } = "附件";
    [Export] public int BoneIndex { get; set; } = 2; // 固定业务关节序号，不能使用Skeleton2D树序。
    [Export] public int BeforeBone { get; set; } = 2; // 在该业务部件之前绘制；-1放到最前层。
    [Export] public Texture2D Texture { get; set; } // 可省略，省略时采样模型主图集。
    [Export] public Rect2 Region { get; set; } // 原图像素裁区，排除文字标签与邻件。
    [Export] public Rect2 ExcludeRegion { get; set; } // 原图坐标中的邻件排除区；留空时不追加裁剪。
    [Export] public Rect2 DisplayRect { get; set; } // 相对挂点的骨架坐标范围。
    [Export] public float RotationDegrees { get; set; } // 附属层相对骨骼的静态倾角；配置位置时须保持原图根部对齐挂点。
    [Export] public int RotationReferenceBone { get; set; } = -1; // 可选旋转参考骨；肩甲可参考胸骨，肘部软组织可参考上臂，位置仍固定于BoneIndex。
    [Export(PropertyHint.Range, "0,1,0.05")] public float RotationFollowWeight { get; set; } = 1; // 在参考骨与挂点骨之间混合旋转；1保持旧绑定，肩甲小幅随臂，不随小臂整体翻转。
    [Export] public float ClothAmplitude { get; set; } // 0为刚性附件，其余为自由端位移幅度。
    [Export] public Vector2 ClothRoot { get; set; } // 相对Region的原图像素挂点，根部不变形。
    [Export] public float ClothRate { get; set; } = 1;
    [Export] public float ClothFlex { get; set; } = 1.7f; // 自由端柔性增长指数；默认保持既有附件的风动，迁移主冠翎时可沿用其原指数。
    [Export] public float ClothPhase { get; set; } // 多条冠翎使用不同相位，避免同步摆动。
    [Export] public Vector2 AmbientWind { get; set; } // 世界平面的环境风向量，长度为0～1的风强；零值保持仅随移动飘动，暂停沿用角色时钟。
    [Export] public Vector2 ClothVerticalScale { get; set; } = Vector2.One; // 以ClothRoot高度分界的上、下段纵向比例，均须大于0；默认各1，挂点和原图不变。
    [Export] public Vector4 ClothArc { get; set; } // 原图像素中的弧线中心X/Y及椭圆半径X/Y；半径为0关闭局部弧带宽度调整。
    [Export] public float ClothArcWidthScale { get; set; } = 1; // 根部上方羽带的径向厚度比例；沿弧线收细，接近根部时平滑恢复原宽。
}
