using Godot;
using System;
using System.Text.Json;
using System.Collections.Generic;

/// <summary>版本化进度文件读写；接收显式路径，使自动验收可使用隔离文件而不触碰玩家存档。</summary>
public static class AdventureSaveStore
{
    public static AdventureProgress Read(string path, FirstCityConfig config)
    {
        using var file=FileAccess.Open(path,FileAccess.ModeFlags.Read);
        if(file==null) throw new InvalidOperationException("无法读取进度文件");
        var data=JsonSerializer.Deserialize<AdventureProgress>(file.GetAsText());
        if(data==null || data.Version!=1 || !Enum.IsDefined(data.Trial)) throw new InvalidOperationException("存档版本或任务状态无效");
        data.TrainingCount=Math.Clamp(data.TrainingCount,0,Math.Min(config.TrainingHealth.Length,config.TrainingPower.Length));
        data.HealthRatio=float.IsFinite(data.HealthRatio) ? Math.Clamp(data.HealthRatio,.01f,1) : 1;
        data.Coins=Math.Max(0,data.Coins);
        data.Items ??= new Dictionary<string,int>();
        data.Items = data.HasTorch ? new Dictionary<string,int> { [AdventureProgress.TorchId]=1 } : new();
        data.LitCaveCandles ??= new HashSet<string>();
        data.LitCaveCandles.RemoveWhere(id => !CaveLayout.IsCandleId(id));
        return data;
    }
    public static Error Write(string path,AdventureProgress progress)
    {
        using(var file=FileAccess.Open(path+".tmp",FileAccess.ModeFlags.Write))
        {
            if(file==null) return FileAccess.GetOpenError();
            file.StoreString(JsonSerializer.Serialize(progress)); file.Flush();
            if(file.GetError()!=Error.Ok) return file.GetError();
        }
        return DirAccess.RenameAbsolute(ProjectSettings.GlobalizePath(path+".tmp"),ProjectSettings.GlobalizePath(path));
    }
}
