using Godot;

/// <summary>首城切片的只读平衡参数；任务、训练与坐骑的运行状态另存于 AdventureProgress。</summary>
[GlobalClass]
public partial class FirstCityConfig : Resource
{
    [Export] public string CityName { get; set; } = "槐安城";
    [Export] public float WalkSpeed { get; set; } = 125;
    [Export] public float BrownHorseSpeed { get; set; } = 190;
    [Export] public float WhiteHorseSpeed { get; set; } = 250;
    [Export] public int StartingCoins { get; set; } = 120;
    [Export] public int ForgeCost { get; set; } = 60;
    [Export] public int QuestCoins { get; set; } = 100;
    [Export] public int[] TrainingHealth { get; set; } = { 100, 60, 30 };
    [Export] public float[] TrainingPower { get; set; } = { .06f, .04f, .02f };
    [Export] public float ForgePower { get; set; } = .1f;
    [Export] public float BookPower { get; set; } = .1f;
    [Export] public float TrainingSeconds { get; set; } = 4;
}
