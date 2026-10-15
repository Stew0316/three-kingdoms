using Godot;
using System;
using System.Linq;

/// <summary>首城跨图会话及版本化本地存档；遭遇内的伤害、冷却与角色节点不跨图保存。</summary>
public partial class AdventureSession : Node
{
    public static AdventureSession Instance { get; private set; }
    public AdventureProgress Progress { get; private set; } = new();
    public FirstCityConfig Config { get; private set; }
    public bool InCityTrial { get; private set; }
    public string CityEntrance { get; private set; } = "inn";
    public string SaveWarning { get; private set; } = "";
    /// <summary>主动使用火把的临时状态；只拥有物品不会自动点亮，重启需重新使用。</summary>
    public bool TorchInUse { get; private set; }
    public bool UseTorch()
    {
        if (!Progress.HasTorch) return false;
        TorchInUse = true;
        return true;
    }
    public void PutAwayTorch() => TorchInUse = false;
    public Error EnterCave(SceneTree tree, Combatant player)
    {
        Progress.HealthRatio = (float)player.Health.CurrentHealth / player.Health.MaxHealth;
        Save();
        return tree.ChangeSceneToFile("res://Scene/OldRoadCave.tscn");
    }
    public Error LeaveCave(SceneTree tree, Combatant player)
    {
        Progress.HealthRatio = (float)player.Health.CurrentHealth / player.Health.MaxHealth;
        // 独立运行洞窟时可能尚未破石，回到城门外侧安全位置，避免困在旧驿道。
        CityEntrance = Progress.BoulderBroken ? "cave" : "gate";
        Save();
        return tree.ChangeSceneToFile("res://Scene/FirstCity.tscn");
    }
    private bool _testing;
    private const string SavePath = "user://first-city-v1.json";

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        Config = GD.Load<FirstCityConfig>("res://Resources/Config/World/first-city.tres");
        _testing = OS.GetCmdlineUserArgs().Any(a => a.Contains("test") || a.StartsWith("--capture"));
        Progress = new AdventureProgress { Coins = Config.StartingCoins };
        if (!_testing && FileAccess.FileExists(SavePath))
        {
            try
            {
                Progress = AdventureSaveStore.Read(SavePath,Config);
            }
            catch (Exception error) { SaveWarning = "存档读取失败，本次从初始进度开始"; GD.PushWarning(SaveWarning + "：" + error.Message); }
        }
    }

    /// <summary>先写临时文件，再替换；测试进程不读写玩家存档。</summary>
    public void Save()
    {
        if (_testing) return;
        var error = AdventureSaveStore.Write(SavePath,Progress);
        SaveWarning = error == Error.Ok ? "" : "保存失败：请保留当前游戏进程";
        if (error != Error.Ok) GD.PushWarning(SaveWarning);
    }

    public Error EnterTrial(SceneTree tree)
    {
        PutAwayTorch();
        InCityTrial = true;
        HeroCatalog.PlayerIndex = 0; HeroCatalog.EnemyIndex = 1;
        tree.Paused = false;
        var error = tree.ChangeSceneToFile("res://Scene/Arena.tscn");
        if (error != Error.Ok) InCityTrial = false;
        return error;
    }
    public Error ReturnToCity(SceneTree tree, Combatant player)
    {
        Progress.HealthRatio = player.IsDead ? .25f : (float)player.Health.CurrentHealth / player.Health.MaxHealth;
        CityEntrance = player.IsDead ? "inn" : "gate";
        Save();
        InCityTrial = false;
        tree.Paused = false;
        return tree.ChangeSceneToFile("res://Scene/FirstCity.tscn");
    }
    /// <summary>只复制该次实例的基础属性；共享资源与敌人配置保持只读。</summary>
    public void ConfigureActor(Combatant actor, bool city)
    {
        actor.Config = (CombatantConfig)actor.Config.Duplicate();
        actor.Config.MaxHealth += Progress.HealthBonus(Config);
        if (city) actor.Config.MoveSpeed = Config.WalkSpeed;
        actor.ProgressionDamageMultiplier = Progress.PowerMultiplier(Config);
    }
    public void RestoreHealth(Combatant actor)
    {
        int damage = actor.Health.MaxHealth - Math.Max(1, Mathf.RoundToInt(actor.Health.MaxHealth * Progress.HealthRatio));
        if (damage > 0) actor.Health.ApplyDamage(new DamageInfo(null, damage, Vector2.Zero));
    }
    public void CompleteTrial(bool won)
    {
        if (won && InCityTrial && Progress.EncounterCleared("core:encounter/weiyan_trial")) Save();
    }
}
