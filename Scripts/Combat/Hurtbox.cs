using Godot;

public partial class Hurtbox : Area2D
{
	[Export] Node target; // 宿主根节点（木桩或角色），场景里拖进来

	public void TakeDamage(DamageInfo info)
	{
		target?.GetNodeOrNull<Health>("Health")?.ApplyDamage(info);
		// 阶段 4 的击退、Hurt 状态触发也写在这里
	}
}
