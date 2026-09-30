using Godot;

/// <summary>统一受击入口，将命中请求交给角色；兼容旧木桩的生命组件。</summary>
public partial class Hurtbox : Area2D
{
	[Export] Node target; // 宿主根节点（木桩或角色），场景里拖进来
	// 显式指定宿主优先，否则以父节点作为受击对象。
	public Node Target => target ?? GetParent();

	/// <summary>转发 info：角色处理受击状态与击退，旧木桩只处理生命扣减。</summary>
	public void TakeDamage(DamageInfo info)
	{
		if (Target is Combatant combatant)
			combatant.ReceiveDamage(info);
		else
			Target?.GetNodeOrNull<Health>(NodeNames.Health)?.ApplyDamage(info);
	}
}
