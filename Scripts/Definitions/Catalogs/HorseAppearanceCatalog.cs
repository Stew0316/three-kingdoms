using Godot;

/// <summary>内置马匹外观目录；两份原始资源只读共享，不包含速度或动作状态。</summary>
public static class HorseAppearanceCatalog
{
    // 场景缓存持有原生资源，并不保证C#包装对象跨场景存活。保留这两份有限的托管强引用，
    // 防止再次回城加载外观时与Godot脚本Resource的终结器竞争；不收集运行时创建的外观。
    private const string ChestnutPath="res://Resources/Config/World/horse-chestnut.tres";
    private const string DapplePath="res://Resources/Config/World/horse-dapple.tres";
    private static HorseAppearance _chestnut,_dapple;
    public static HorseAppearance Chestnut => _chestnut??=GD.Load<HorseAppearance>(ChestnutPath);
    public static HorseAppearance Dapple => _dapple??=GD.Load<HorseAppearance>(DapplePath);

    /// <summary>直接拖入预设场景时也保护内置配置；自定义外观仍由使用它的实例持有。</summary>
    public static void RetainBuiltIn(HorseAppearance appearance)
    {
        if(appearance?.ResourcePath==ChestnutPath) _chestnut??=appearance;
        else if(appearance?.ResourcePath==DapplePath) _dapple??=appearance;
    }
}
