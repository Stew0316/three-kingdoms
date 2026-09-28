using Godot;

public partial class Hurtbox : Area2D
{
	[Export] Node target; // 宿主根节点（木桩或角色），场景里拖进来
	public Node Target => target ?? GetParent();

	public void TakeDamage(DamageInfo info)
	{
		if (Target is Combatant combatant)
			combatant.ReceiveDamage(info);
		else
			Target?.GetNodeOrNull<Health>("Health")?.ApplyDamage(info);
	}
}
