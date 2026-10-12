using Godot;

/// <summary>武将自身的出招曲线。只描述姿态与归一化节奏，不保存技能伤害、冷却或施法时间。</summary>
[GlobalClass]
public partial class CharacterActionAnimationConfig : Resource
{
    [Export] public CharacterPoseConfig PrepareVia { get; set; } // 可选前摇途经姿态：先向前提手再收刀，避免直线插值把握点压进肩关节。
    [Export(PropertyHint.Range, "0.1,0.9,0.05")] public float PrepareViaAt { get; set; } = .45f; // 途经姿态占完整蓄势过程的比例，不改变技能前摇时长。
    [Export] public CharacterPoseConfig Windup { get; set; } // 收势准备。
    [Export] public CharacterPoseConfig High { get; set; } // 第一挥击关键点。
    [Export] public CharacterPoseConfig Contact { get; set; } // 前方斩入关键点。
    [Export] public CharacterPoseConfig Follow { get; set; } // 挥击后的随势。
    [Export] public float WindupReach { get; set; } = .72f; // 前摇内到达蓄势姿态的归一化时点。
    [Export] public float HighAt { get; set; } = .26f; // 生效段第一关键点；0 表示省略此段。
    [Export] public float ContactAt { get; set; } = .70f; // 生效段斩入时点。
    [Export] public float FollowAt { get; set; } = .93f; // 生效段随势时点。
    [Export] public float RecoveryHold { get; set; } = .12f; // 后摇开始保留随势的比例。
}
