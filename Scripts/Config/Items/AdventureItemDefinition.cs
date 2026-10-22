using Godot;

/// <summary>物品库中的只读定义；拥有数量放在冒险存档，使用状态放在当前会话。</summary>
[GlobalClass]
public partial class AdventureItemDefinition : Resource
{
    [Export] public string ItemId { get; set; } = "core:item/torch";
    [Export] public string DisplayName { get; set; } = "火把";
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
    /// <summary>快捷物品槽显示的图标；仅决定外观，不代表玩家已经拥有该物品。</summary>
    [Export] public Texture2D Icon { get; set; }
}
