using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

/// <summary>全局配置、唯一冒险人物和版本化存档入口；角色节点、冷却与短时状态随场景重建。</summary>
public partial class AdventureSession : Node
{
    public static AdventureSession Instance { get; private set; }
    public AdventureProgress Progress { get; private set; } = new();
    public FirstCityConfig Config { get; private set; }
    public GameSettings Settings { get; private set; }
    public KeyBindings Bindings { get; private set; }
    public DayNightClock Clock { get; private set; }
    // 每次启动随机，跨图保持植被外观，避免从洞窟返回时树种突然变化。
    public uint VegetationSeed { get; private set; }
    public WorldAtlas Atlas { get; private set; }
    private readonly Dictionary<string, CharacterAttributes> _characters = new();
    public IReadOnlyDictionary<string, CharacterAttributes> Characters => _characters;
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
        return tree.ChangeSceneToFile(WorldScenePaths.OldRoadCave);
    }
    public Error LeaveCave(SceneTree tree, Combatant player)
    {
        Progress.HealthRatio = (float)player.Health.CurrentHealth / player.Health.MaxHealth;
        // 独立运行洞窟时可能尚未破石，回到城门外侧安全位置，避免困在旧驿道。
        CityEntrance = Progress.BoulderBroken ? "cave" : "gate";
        Save();
        return tree.ChangeSceneToFile(WorldScenePaths.FirstCity);
    }
    private bool _testing;
    private float _trialEntryHealthRatio = 1; // 重试恢复入场生命，不让战败后的零生命进入新角色。
    private const string SavePath = AdventureIds.SavePath;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        Config = GD.Load<FirstCityConfig>("res://Resources/Config/World/first-city.tres");
        Settings = GD.Load<GameSettings>("res://Resources/Config/World/game-settings.tres");
        Atlas = WorldAtlas.Load();
        _testing = OS.GetCmdlineUserArgs().Any(a => a.Contains("test") || a.StartsWith("--capture"));
        Bindings = new KeyBindings(_testing);
        Clock = new DayNightClock(Settings);
        VegetationSeed = GD.Randi();
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

    public override void _Process(double delta)
    {
        // 会话持续跨图，但菜单或战斗结算暂停时不能继续推进昼夜。
        if(!GetTree().Paused) Clock.Advance(delta);
    }

    /// <summary>先写临时文件，再替换；测试进程不读写玩家存档。</summary>
    public void Save()
    {
        if (_characters.TryGetValue("core:lubu", out var player)) Progress.HealthRatio = player.HealthRatio;
        if (_testing) return;
        var error = AdventureSaveStore.Write(SavePath,Progress);
        SaveWarning = error == Error.Ok ? "" : "保存失败：请保留当前游戏进程";
        if (error != Error.Ok) GD.PushWarning(SaveWarning);
    }

    public Error EnterTrial(SceneTree tree)
    {
        PutAwayTorch();
        _trialEntryHealthRatio = _characters.TryGetValue("core:lubu", out var player) ? player.HealthRatio : Progress.HealthRatio;
        InCityTrial = true;
        HeroCatalog.PlayerIndex = 0; HeroCatalog.EnemyIndex = 1;
        tree.Paused = false;
        var error = tree.ChangeSceneToFile(WorldScenePaths.Arena);
        if (error != Error.Ok) InCityTrial = false;
        return error;
    }
    public void PrepareTrialRestart()
    {
        if (InCityTrial && _characters.TryGetValue("core:lubu", out var player))
            player.RestoreRatio(_trialEntryHealthRatio);
    }
    public Error ReturnToCity(SceneTree tree, Combatant player)
    {
        Progress.HealthRatio = player.IsDead ? .25f : (float)player.Health.CurrentHealth / player.Health.MaxHealth;
        player.Attributes?.RestoreRatio(Progress.HealthRatio);
        CityEntrance = player.IsDead ? "inn" : "gate";
        Save();
        InCityTrial = false;
        tree.Paused = false;
        return tree.ChangeSceneToFile(WorldScenePaths.FirstCity);
    }
    /// <summary>冒险角色绑定唯一人物数据；city 保留为兼容参数，跨图不再改变基础徒步速度。</summary>
    public void ConfigureActor(Combatant actor, bool city)
    {
        string id = actor.Ui.CharacterId;
        if (!_characters.TryGetValue(id, out var attributes))
        {
            attributes = new CharacterAttributes(id, actor.Config, Progress.HealthRatio);
            if (id == "core:lubu")
            {
                attributes.Config.MaxHealth += Progress.HealthBonus(Config);
                attributes.Config.MoveSpeed = Config.WalkSpeed;
                attributes.DamageMultiplier = Progress.PowerMultiplier(Config);
                attributes.RestoreRatio(Progress.HealthRatio);
            }
            if (Progress.CharacterMoveRates.TryGetValue(id, out float rate))
                attributes.MoveRate = Mathf.Clamp(rate, Settings.MinimumMoveRate, Settings.MaximumMoveRate);
            _characters.Add(id, attributes);
        }
        actor.Attributes = attributes;
        actor.Config = attributes.Config;
    }
    /// <summary>改变普通移动倍率；技能突进和击退仍按自身配置结算。</summary>
    public void SetMoveRate(Combatant actor, float rate)
    {
        if (!float.IsFinite(rate)) return;
        rate = Mathf.Clamp(rate, Settings.MinimumMoveRate, Settings.MaximumMoveRate);
        if (actor.Attributes != null) actor.Attributes.MoveRate = rate;
        else actor.LocalMoveRate = rate; // 独立演武是可重置沙盒，不写入冒险人物。
        if (actor.Attributes == null) return;
        Progress.CharacterMoveRates[actor.Attributes.Id] = rate;
        Save();
    }
    public void RestoreHealth(Combatant actor)
    {
        if (actor.Attributes != null) return; // Health 已直接绑定唯一数据，切图不重复扣血。
        int damage = actor.Health.MaxHealth - Math.Max(1, Mathf.RoundToInt(actor.Health.MaxHealth * Progress.HealthRatio));
        if (damage > 0) actor.Health.ApplyDamage(new DamageInfo(null, damage, Vector2.Zero));
    }
    public void CompleteTrial(bool won)
    {
        if (won && InCityTrial && Progress.EncounterCleared(AdventureIds.WeiYanTrial)) Save();
    }
}
