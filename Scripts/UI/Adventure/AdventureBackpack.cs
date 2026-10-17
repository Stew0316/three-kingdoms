using Godot;
using System;

/// <summary>城镇和洞窟共用的物品入口，按钮显式改变使用状态，不将物品混入技能槽。</summary>
public static class AdventureBackpack
{
    public static void Open(CityDialogue dialogue, Action<string> notify)
    {
        var session = AdventureSession.Instance;
        var item = GD.Load<AdventureItemDefinition>("res://Resources/Config/Items/torch.tres");
        if (!session.Progress.HasTorch)
        {
            dialogue.Open("行囊 · 物品", "行囊里还没有火把。可向槐安驿丞领取，洞窟入口的行旅物资中也备有一支。");
            return;
        }
        dialogue.Open("行囊 · 物品", $"{item.DisplayName} ×1  ·  {(session.TorchInUse ? "正在使用" : "尚未使用")}\n{item.Description}",
            (session.TorchInUse ? "收起火把" : "使用火把", () =>
            {
                if (session.TorchInUse) { session.PutAwayTorch(); notify("已收起火把 · 已点燃的蜡烛继续照明"); }
                else if (session.UseTorch()) notify("火把已点燃 · 靠近墙上蜡烛按 E 点火");
            }));
    }
}
