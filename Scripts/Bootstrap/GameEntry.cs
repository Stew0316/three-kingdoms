using Godot;
using System.Linq;

/// <summary>先显示启动页，再后台加载场景；原专项参数仍直接路由。</summary>
public partial class GameEntry : Node
{
    private StartupSplash _splash;
    private string[] _paths;
    private Resource[] _resources;
    private ulong _started;
    private bool _requested, _finished;
    private int _visibleFrames;
    private int _loadIndex;
    public override void _Ready() => CallDeferred(MethodName.Start);
    private void Start()
    {
        var args=OS.GetCmdlineUserArgs();
        bool preview=args.Contains("--startup-snow-test");
        bool cave=args.Any(a=>a is "--cave" or "--cave-test");
        bool arena=args.Any(a=>a=="--arena" || ((a.Contains("test") || a.StartsWith("--capture"))
            && a is not "--city-test" and not "--horse-test" and not "--navigation-test" and not "--status-cycle-test" and not "--status-quit-test" and not "--startup-snow-test"));
        string target=cave ? WorldScenePaths.OldRoadCave : arena ? WorldScenePaths.Arena : WorldScenePaths.FirstCity;
        if(!preview && args.Any(a=>a.Contains("test") || a.StartsWith("--capture")))
        { GetTree().ChangeSceneToFile(target); return; }
        _splash=new StartupSplash(); AddChild(_splash);
        _started=Time.GetTicksMsec();
        if(preview) GetTree().Root.CallDeferred(Node.MethodName.AddChild,new StartupSnowTests());
        // 人物场景由目标场景正常加载；额外线程预载该脚本场景在 4.7.1 中留下原生清理警告。
        // 城市的大幅外景贴图仍提前加载，人物装配全过程由启动覆盖层遮住。
        _paths=target==WorldScenePaths.FirstCity
            ? new[] { target, SnowMountainLayout.MountainTexture, SnowMountainLayout.MeadowTexture, SnowMountainLayout.BankTexture }
            : new[] { target };
        _resources=new Resource[_paths.Length];
    }
    public override void _Process(double delta)
    {
        if(_paths==null || _finished) return;
        // 等待两次绘制调度，保证启动页先显示；逐帧查询不留下依附已释放入口节点的异步等待。
        if(++_visibleFrames<2) return;
        if(!_requested)
        {
            _requested=true;
            if(ResourceLoader.LoadThreadedRequest(_paths[_loadIndex])!=Error.Ok) { Fail(); return; }
        }
        if(_loadIndex<_paths.Length)
        {
            var status=ResourceLoader.LoadThreadedGetStatus(_paths[_loadIndex]);
            if(status is ResourceLoader.ThreadLoadStatus.Failed or ResourceLoader.ThreadLoadStatus.InvalidResource)
            { Fail(); return; }
            if(status!=ResourceLoader.ThreadLoadStatus.Loaded) return;
            _resources[_loadIndex]=ResourceLoader.LoadThreadedGet(_paths[_loadIndex]);
            _loadIndex++;
            // 逐项保存完成的资源句柄；下一请求失败时仍能保留已显示的启动页。
            if(_loadIndex<_paths.Length) { _requested=false; return; }
        }
        // 从启动页出现起至少一秒；加载超过一秒则就绪即切换，不再追加等待。
        if(Time.GetTicksMsec()-_started<1000) return;
        _finished=true;
        _splash.Reparent(GetTree().Root);
        // 覆盖层自行等新场景提交首帧，切场景释放本节点不影响收尾。
        _splash.DismissAfterSceneChange();
        if(GetTree().ChangeSceneToPacked((PackedScene)_resources[0])!=Error.Ok) _splash.ShowError();
    }
    private void Fail() { _finished=true; _splash.ShowError(); }
}
