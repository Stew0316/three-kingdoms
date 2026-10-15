using Godot;

/// <summary>物品库中的只读定义；拥有数量放在冒险存档，使用状态放在当前会话。</summary>
[GlobalClass]
public partial class AdventureItemDefinition : Resource
{
    [Export] public string ItemId { get; set; } = "core:item/torch";
    [Export] public string DisplayName { get; set; } = "火把";
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
}
