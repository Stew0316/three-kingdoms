using System;
using System.Linq;
using System.Collections.Generic;

/// <summary>可序列化的首城进度。任务只监听遭遇事实，不依赖 UI、NPC 或 Buff。</summary>
public sealed class AdventureProgress
{
    public enum QuestStage { Available, Accepted, Completed, Rewarded }
    public int Version { get; set; } = AdventureIds.SaveVersion;
    public QuestStage Trial { get; set; }
    public bool BoulderBroken { get; set; }
    public bool CacheClaimed { get; set; }
    public int TrainingCount { get; set; }
    public bool WeaponForged { get; set; }
    public int Coins { get; set; } = 120;
    public float HealthRatio { get; set; } = 1;
    // 按人物稳定 ID 保存倍率；缺省为 1，兼容已有版本 1 存档。
    public Dictionary<string, float> CharacterMoveRates { get; set; } = new();
    // 兼容旧版存档：缺失的新字段自动初始化，不重置原有任务与成长。
    public Dictionary<string, int> Items { get; set; } = new();
    public HashSet<string> LitCaveCandles { get; set; } = new();
    public const string TorchId = AdventureIds.Torch;
    public bool HasTorch => Items != null && Items.TryGetValue(TorchId, out int count) && count > 0;
    public bool ReceiveTorch()
    {
        if (HasTorch) return false;
        Items ??= new();
        Items[TorchId] = 1;
        return true;
    }
    public bool LightCaveCandle(string id, bool torchInUse)
    {
        if (!HasTorch || !torchInUse || !CaveLayout.IsCandleId(id)) return false;
        return LitCaveCandles.Add(id);
    }
    public bool CanSmash => Trial != QuestStage.Available;
    public bool HasBook => Trial == QuestStage.Rewarded;
    public int HealthBonus(FirstCityConfig config) => config.TrainingHealth.Take(TrainingCount).Sum();
    public float PowerMultiplier(FirstCityConfig config) => 1 + config.TrainingPower.Take(TrainingCount).Sum()
        + (WeaponForged ? config.ForgePower : 0) + (HasBook ? config.BookPower : 0);
    public bool AcceptTrial()
    {
        if (Trial != QuestStage.Available) return false;
        Trial = QuestStage.Accepted;
        return true;
    }
    public bool EncounterCleared(string encounterId)
    {
        if (encounterId != AdventureIds.WeiYanTrial || Trial != QuestStage.Accepted) return false;
        Trial = QuestStage.Completed;
        return true;
    }
    public bool ClaimTrial(FirstCityConfig config)
    {
        if (Trial != QuestStage.Completed) return false;
        Trial = QuestStage.Rewarded;
        Coins += config.QuestCoins;
        return true;
    }
    public bool Train(FirstCityConfig config)
    {
        if (TrainingCount >= Math.Min(config.TrainingHealth.Length, config.TrainingPower.Length)) return false;
        TrainingCount++;
        return true;
    }
    public bool Forge(FirstCityConfig config)
    {
        if (WeaponForged || Coins < config.ForgeCost) return false;
        Coins -= config.ForgeCost;
        WeaponForged = true;
        return true;
    }
    public bool Smash()
    {
        if (!CanSmash || BoulderBroken) return false;
        BoulderBroken = true;
        return true;
    }
    public bool ClaimCache()
    {
        if (!BoulderBroken || CacheClaimed) return false;
        CacheClaimed = true;
        Coins += 80;
        return true;
    }
}
