using Godot;

/// <summary>演武场内容目录。选择保存武将目录索引；每次重开重新装配单位，不复用战斗状态。</summary>
public static class HeroCatalog
{
    public static readonly string[] Ids = { "lubu", "weiyan", "guanyu", "liubei" };
    // 显示名称从每位武将独立UI资源读取，目录不另外维护一套人物资料。
    public static string[] Names => System.Array.ConvertAll(Ids,
        id => GD.Load<CharacterUiConfig>($"res://Resources/Config/Combat/{id}-ui.tres").DisplayName);
    public static int PlayerIndex { get; set; }
    public static int EnemyIndex { get; set; } = 1;
    public static void Configure(Combatant actor, int index)
    {
        string id = Ids[Mathf.Clamp(index, 0, Ids.Length - 1)];
        string root = $"res://Resources/Config/Combat/{id}";
        actor.Config = GD.Load<CombatantConfig>(root + ".tres");
        actor.Presentation = GD.Load<CharacterPresentationConfig>(root + "-presentation.tres");
        actor.Ui = GD.Load<CharacterUiConfig>(root + "-ui.tres");
        actor.Loadout = GD.Load<SkillLoadoutConfig>(root + "-loadout.tres");
    }
}
