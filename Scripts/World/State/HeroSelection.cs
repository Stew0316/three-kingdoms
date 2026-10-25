/// <summary>本次进程的演武选将状态；重开保留，重新启动恢复默认，不持有战斗实例或共享配置。</summary>
public static class HeroSelection
{
    // 我方当前武将的目录索引，默认吕布。
    public static int PlayerIndex { get; set; }

    // 对手的目录索引，默认魏延；正常切磋在吕布与魏延之间交换双方。
    public static int EnemyIndex { get; set; } = 1;
}
