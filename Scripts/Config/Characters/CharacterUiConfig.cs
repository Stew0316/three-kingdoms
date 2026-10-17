using Godot;

/// <summary>武将独立的界面身份与主题；技能名称和图标由装备的技能定义提供。</summary>
[GlobalClass]
public partial class CharacterUiConfig : Resource
{
    [Export] public string CharacterId { get; set; } = ""; // 稳定武将标识，不作为技能执行分支。
    [Export] public string DisplayName { get; set; } = "武将";
    [Export] public Texture2D Portrait { get; set; } // 角色头像资源，可独立于战斗模型替换。
    [Export] public Color NameColor { get; set; } = new("9d4037");
    [Export] public Color HealthColor { get; set; } = new("bc5947");
    [Export] public Color DetailColor { get; set; } = new("8f3c34");
}
