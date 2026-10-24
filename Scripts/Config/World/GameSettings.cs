using Godot;

/// <summary>全局玩法与视图参数；每项注明单位、用途和约束，玩家按键选择另存用户配置。</summary>
[GlobalClass]
public partial class GameSettings : Resource
{
    // 普通移动加速度，像素/秒²；只影响起步，不改变招式位移。
    [Export] public float Acceleration { get; set; } = 1500;
    // 松开移动键后的制动力，像素/秒²；应大于零。
    [Export] public float Deceleration { get; set; } = 2200;
    // 人物可选择的最小移动倍率，无量纲，应大于零。
    [Export] public float MinimumMoveRate { get; set; } = .5f;
    // 人物可选择的最大移动倍率，不小于最小倍率。
    [Export] public float MaximumMoveRate { get; set; } = 2;
    // 城市镜头缩放；小于 1 时拉远，保持世界比例。
    [Export] public float CityZoom { get; set; } = .72f;
    // 洞窟镜头缩放；小于 1 时扩大可见范围。
    [Export] public float CaveZoom { get; set; } = .8f;
    // 演武场镜头缩放；小于 1 时扩大战斗视野。
    [Export] public float ArenaZoom { get; set; } = .8f;
    // 徒步时每个小地图像素对应的世界像素，越大显示范围越广。
    [Export] public float MinimapWorldUnitsPerPixel { get; set; } = 3.2f;
    // 骑乘时每个小地图像素对应的世界像素，随上下马平滑变化。
    [Export] public float MinimapMountedWorldUnitsPerPixel { get; set; } = 4.2f;
    // 局部小地图视野倍率；2 表示比例尺由原 1:100 变为 1:200，覆盖两倍线性距离。总览不受影响。
    [Export] public float MinimapRangeMultiplier { get; set; } = 2;
    // 游戏纪年起点（公元年），是剧情设定，不作为吕布生年的史实断言。
    [Export] public int CalendarStartYear { get; set; } = 160;
    // 昼夜每格的真实游戏秒数；300 秒即 5 分钟，12 格共 60 分钟。暂停不计时，最小 1 秒。
    [Export] public float DayNightSecondsPerSegment { get; set; } = 300;
    // 新会话从第几格开始，范围 [0,12)；0 黎明、3 日中、6 黄昏、9 午夜。
    [Export] public float DayNightStartSegment { get; set; } = 1;
    // 白天室外环境乘色；白色保持原始材质色，UI 不受影响。
    [Export] public Color DayAmbient { get; set; } = Colors.White;
    // 黎明室外环境乘色，暖光逐渐过渡到白昼。
    [Export] public Color DawnAmbient { get; set; } = new(.74f,.65f,.58f);
    // 黄昏室外环境乘色，日落后逐渐过渡到月夜。
    [Export] public Color DuskAmbient { get; set; } = new(.72f,.49f,.40f);
    // 夜晚室外环境乘色，蓝灰月光保持道路和人物可辨；洞窟继续使用自身局部照明。
    [Export] public Color NightAmbient { get; set; } = new(.32f,.40f,.58f);
}
