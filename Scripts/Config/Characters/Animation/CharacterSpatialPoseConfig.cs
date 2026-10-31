using Godot;

/// <summary>空间骨架关键姿态。深度只用于二维投影和遮挡，不参与世界碰撞；角度允许超过360度以保留整圈转身。</summary>
[GlobalClass]
public partial class CharacterSpatialPoseConfig : Resource
{
    [Export] public float Time { get; set; } // 动作原速中的秒数。
    [Export] public Vector3 Root { get; set; } // 相对站立骨盆的位移，Y向下；负Y表示腾空。
    [Export] public float Yaw { get; set; } // 骨盆绕竖轴的连续角度，0正面、90右侧、180背面。
    [Export] public float ChestTwist { get; set; } // 胸相对髋的转角，用于腰肩先后发力。
    [Export] public float Lean { get; set; } // 胸部在画面平面内的倾角，不整体旋转脚底。
    [Export] public Vector3 Grip { get; set; } = new(-12, 12, 20); // 主握点相对胸骨，跟随胸部坐标系。
    [Export] public Vector2 BladeAngles { get; set; } = new(0, 80); // 刀尖方向：相对胸部的水平角、仰角，单位度。
    [Export] public Vector3 FreeHand { get; set; } = new(24, 22, 8); // 副手释放时相对胸部的位置。
    [Export] public float TwoHandWeight { get; set; } // 副手接柄权重，0为自然摆臂，1为双手握持。
    [Export] public Vector3 FarFoot { get; set; } = new(-15, 0, 0); // 脚底相对人物地面原点；支撑帧固定，腾空帧Y为负。
    [Export] public Vector3 NearFoot { get; set; } = new(15, 0, 0);
    [Export] public float FarFootYaw { get; set; } = 65; // 脚尖方向独立于髋部，允许支撑脚先稳住再随转身换步。
    [Export] public float NearFootYaw { get; set; } = 65;
    [Export] public float FarFootRoll { get; set; } // 腾空/蹬地时的靴掌角，落地必须归零。
    [Export] public float NearFootRoll { get; set; }
}
