using Godot;

/// <summary>可复用的空间骨架动作片段；原速、关键帧与多角度美术独立于伤害规则。</summary>
[GlobalClass]
public partial class CharacterSpatialMotionConfig : Resource
{
    [Export] public Godot.Collections.Array<CharacterSpatialPoseConfig> Keys { get; set; } = new();
    [Export] public float Duration { get; set; } = 4;
    [Export] public float BlendIn { get; set; } = .12f; // 与当前实姿态衔接的秒数。
    [Export] public float BlendOut { get; set; } = .2f; // 回到本角色站姿的秒数。
}
