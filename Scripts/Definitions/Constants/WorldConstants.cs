/// <summary>运行时共用的场景入口；场景文件迁移时在这里统一修改。</summary>
public static class WorldScenePaths
{
    public const string FirstCity = "res://Scene/FirstCity.tscn";
    public const string OldRoadCave = "res://Scene/OldRoadCave.tscn";
    public const string Arena = "res://Scene/Arena.tscn";
    public const string Combatant = "res://Components/Characters/Combatant.tscn";
}

/// <summary>冒险内容与存档的稳定标识；目录调整不得改变已保存的 ID。</summary>
public static class AdventureIds
{
    public const string Torch = "core:item/torch";
    public const string WeiYanTrial = "core:encounter/weiyan_trial";
    public const string SavePath = "user://first-city-v1.json";
    public const int SaveVersion = 1;
}
