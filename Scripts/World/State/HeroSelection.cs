/// <summary>本次进程的演武选将状态；重开保留，重新启动恢复默认，不持有战斗实例或共享配置。</summary>
public static class HeroSelection
{
    // 我方当前武将的目录索引，默认吕布。
    public static int PlayerIndex { get; set; }

    // 首位对手的目录索引，默认魏延；其余对手由演武场补齐。
    public static int EnemyIndex { get; set; } = 1;
}
