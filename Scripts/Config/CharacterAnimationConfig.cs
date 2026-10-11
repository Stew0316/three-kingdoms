using Godot;

/// <summary>武将独立的站立、行走及语义动作映射；换技能不会换这套动作。</summary>
[GlobalClass]
public partial class CharacterAnimationConfig : Resource
{
    [Export] public float MotionStrength { get; set; } = 1;
    [Export] public bool ReferencePoseSet { get; set; } // 使用长兵器关键姿态和独立持械约束，替代通用角度公式。
    [Export] public CharacterPoseConfig StandingPose { get; set; } // 角色自己的持械警戒姿态。
    [Export] public CharacterPoseConfig MovingPose { get; set; } // 角色自己的行走持械姿态及双手权重。
    [Export] public CharacterPoseConfig DodgePose { get; set; } // 自身闪避姿态，借入闪避技能仍使用这份资源。
    [Export] public float DodgeEnterSeconds { get; set; } = .045f; // 进入闪避姿态的平滑时间。
    [Export] public Vector2 HurtPelvisShift { get; set; } = new(1.5f, 2); // 受击横向偏移按击退方向乘权重，纵向沉胯。
    [Export] public float HurtChestDegrees { get; set; } = 9.167325f; // 受击胸部倾角幅度。
    [Export] public Vector2 HurtFreeHand { get; set; } = new(12, -43); // 自由手防御位置。
    [Export] public float HurtWeaponDegrees { get; set; } = -14.323945f; // 受击时兵器的防御角度。
    [Export] public bool MartialWalk { get; set; } // 开启路程驱动的武将步态，参考姿态分支由承重腿决定腰高。
    [Export(PropertyHint.Range, "4,16,0.5")] public float StandStanceWidth { get; set; } = 10; // 站立警戒时的半脚距，独立于移动步幅。
    [Export(PropertyHint.Range, "0,5,0.25")] public float StandCrouch { get; set; } = .75f; // 站立时轻微屈膝，移动时再降低重心。
    [Export(PropertyHint.Range, "4,16,0.5")] public float WalkStanceWidth { get; set; } = 12; // 两脚距身体中线的距离，骨架单位。
    [Export(PropertyHint.Range, "0,5,0.25")] public float WalkCrouch { get; set; } = 2.5f; // 腰胯下沉量，脚底仍停留在地面。
    [Export(PropertyHint.Range, "32,100,1")] public float WalkCycleDistance { get; set; } = 72; // 完成左右各一步所需的实际移动距离，世界像素。
    [Export(PropertyHint.Range, "1,16,0.25")] public float WalkStride { get; set; } = 4.5f; // 单脚前后移动的半幅，骨架单位。
    [Export(PropertyHint.Range, "0,5,0.25")] public float WalkFootLift { get; set; } = 2; // 摆动脚离地高度，避免高抬腿和蹦跳。
    [Export(PropertyHint.Range, "10,35,1")] public float WalkSupportKneeDegrees { get; set; } = 24; // 参考步态的承重膝弯角，0 为完全伸直；摆动腿由足点单独求解。
    [Export] public CharacterActionAnimationConfig Basic { get; set; } // 普通挥击语义。
    [Export] public CharacterActionAnimationConfig Dash { get; set; } // 突进攻击语义。
    [Export] public CharacterActionAnimationConfig Sweep { get; set; } // 范围挥击语义。
    [Export] public CharacterActionAnimationConfig Counter { get; set; } // 反击语义，装备任何来源反击技能均用自身动作。
    public CharacterActionAnimationConfig GetAction(CombatMotion motion) => motion switch
    { CombatMotion.Dash => Dash, CombatMotion.Sweep => Sweep, _ => Basic };
}
