using Godot;

/// <summary>武将内容目录与统一装配入口；共享资源只读，选择状态由 HeroSelection 单独保存。</summary>
public static class HeroCatalog
{
    public static readonly string[] Ids = { "lubu", "weiyan", "guanyu", "liubei" };
    // 显示名称从每位武将独立UI资源读取，目录不另外维护一套人物资料。
    public static string[] Names => System.Array.ConvertAll(Ids,
        id => GD.Load<CharacterUiConfig>(ResourceRoot(id) + "-ui.tres").DisplayName);

    // 保留原调用入口；可变选择属于会话状态，不属于目录定义或共享 Resource。
    public static int PlayerIndex { get => HeroSelection.PlayerIndex; set => HeroSelection.PlayerIndex = value; }
    public static int EnemyIndex { get => HeroSelection.EnemyIndex; set => HeroSelection.EnemyIndex = value; }

    /// <summary>同一武将的基础属性、模型表现、界面资料与默认装备共用资源前缀。</summary>
    private static string ResourceRoot(string id) => $"res://Resources/Config/Characters/{id}/{id}";

    /// <summary>界面预览与角色装配读取同一默认装备；调用方不能改写返回的共享资源。</summary>
    public static SkillLoadoutConfig LoadDefaultLoadout(int index) =>
        GD.Load<SkillLoadoutConfig>(ResourceRoot(Ids[Mathf.Clamp(index, 0, Ids.Length - 1)]) + "-loadout.tres");

    public static void Configure(Combatant actor, int index)
    {
        string id = Ids[Mathf.Clamp(index, 0, Ids.Length - 1)];
        string root = ResourceRoot(id);
        actor.Config = GD.Load<CombatantConfig>(root + ".tres");
        actor.Presentation = GD.Load<CharacterPresentationConfig>(root + "-presentation.tres");
        actor.Ui = GD.Load<CharacterUiConfig>(root + "-ui.tres");
        actor.Loadout = LoadDefaultLoadout(index);
    }
}
