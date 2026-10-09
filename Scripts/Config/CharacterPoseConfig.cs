using Godot;

/// <summary>统一角色局部坐标中的关键姿态；脚点为原点，正 X 为人物前方，角度单位为度。</summary>
[GlobalClass]
public partial class CharacterPoseConfig : Resource
{
    [Export] public Vector2 PelvisOffset { get; set; } // 相对绑定腰高的重心偏移，正 Y 为沉胯。
    [Export] public float PelvisDegrees { get; set; } // 腰胯带动角，先于长戟挥出，胸部在其上继续转动。
    [Export] public float ChestDegrees { get; set; } // 胸部相对骨盆的倾角。
    [Export] public float HeadDegrees { get; set; } // 头部相对胸部的补偿角。
    [Export] public Vector2 FarHand { get; set; } // 主持械手目标，位于画面后侧的手臂。
    [Export] public Vector2 FreeHand { get; set; } // 未握戟时前侧手的自然位置。
    [Export] public Vector2 FarFoot { get; set; } // 后侧脚底目标。
    [Export] public Vector2 NearFoot { get; set; } // 前侧脚底目标。
    [Export] public float WeaponDegrees { get; set; } // 戟头从朝上起旋转的角度；90 度朝前、180 度朝下。
    [Export] public float BladeDistance { get; set; } = 42; // 主握点到戟尖距离，控制手在长柄上的位置。
    [Export] public float GripSeparation { get; set; } = 30; // 双手沿戟杆的间距。
    [Export(PropertyHint.Range, "0,1,0.05")] public float TwoHandWeight { get; set; } // 0 为副手自由，1 为副手握住第二握点。
}
