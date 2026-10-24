using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>真实启动、东门往返、路带封闭和山脚入口验收，测试会话不读写正式存档。</summary>
public partial class StartupSnowTests : Node
{
    private int _checks;
    private FirstCity City => GetTree().CurrentScene as FirstCity;
    public override void _Ready() => CallDeferred(MethodName.Run);
    private async void Run()
    {
        try
        {
            await Frames(2);
            Check(GetTree().CurrentScene is GameEntry,"加载期仍处于启动场景");
            var splash=GetTree().CurrentScene.GetNode<StartupSplash>("StartupSplash");
            var labels=splash.GetNode("BlackBackground/ThankYou").GetChildren().OfType<Label>().ToArray();
            Check(labels[0].Text=="谢谢你玩我的游戏" && labels[1].Text=="thank you for playing my games.","启动中英文文案准确");
            Check(splash.GetNode<ColorRect>("BlackBackground").Color==Colors.Black,"启动为纯黑底");
            await Capture("01-Startup");
            ulong deadline=Time.GetTicksMsec()+30000;
            while(City==null && Time.GetTicksMsec()<deadline) await Frames(1);
            Check(City!=null,"后台加载结束进入首城");
            await Frames(5);
            Check(!GetTree().Root.GetChildren().OfType<StartupSplash>().Any(),"首城呈现后启动覆盖层释放");
            AdventureSession.Instance.Clock.SetPosition(3);
            City.Camera.PositionSmoothingEnabled=false;
            City.Player.Position=new(905,280); await Frames(3);
            await Move(InputActions.MoveRight,60);
            Check(City.Player.Position.X>1000,"实际徒步穿过东门进入草地");
            await Capture("02-EastGate");
            await Move(InputActions.MoveLeft,60);
            Check(City.Player.Position.X<932,"实际徒步原路返回城内");
            var points=new Vector2[]{new(900,280),new(1080,285),new(1260,290),new(1460,270),new(1650,220),new(1650,80),new(1650,8)};
            City.Player.Position=points[0]; await Frames(3);
            await FollowPath(points.Skip(1).ToArray());
            Check(City.Player.Position.DistanceTo(points[^1])<18,"真实移动连续走完东门到山脚全程");
            Check(City.FindNearest()?.ContentId=="snow-mountain","连续行走后可选中唯一山脚入口");
            City.Mount(2); await FollowPath(points.Reverse().Skip(1).ToArray());
            Check(City.Player.Position.X<932,"最快坐骑可沿整条弯路返回城内"); City.Dismount();
            for(int i=0;i<points.Length-1;i++)
            {
                City.Player.Position=points[i]; await Frames(2);
                Check(!City.Player.TestMove(City.Player.GlobalTransform,points[i+1]-points[i]),"人物碰撞体可连续通过路段 "+i);
            }
            City.Player.Position=new(1230,290); await Frames(2);
            Check(City.Player.TestMove(City.Player.GlobalTransform,new(0,-180)),"草路北侧阻挡人物进入外景");
            Check(City.Player.TestMove(City.Player.GlobalTransform,new(0,180)),"草路南侧阻挡人物进入外景");
            await Capture("03-MeadowTransition");
            City.Player.Position=new(1650,8); await Frames(3);
            Check(City.Player.TestMove(City.Player.GlobalTransform,new(0,-100)),"山脚尽头阻挡攀爬和进入山体");
            Check(City.Player.TestMove(City.Player.GlobalTransform,new(-160,0)),"入口左侧岩壁封闭");
            Check(City.Player.TestMove(City.Player.GlobalTransform,new(160,0)),"入口右侧岩壁封闭");
            Check(City.FindNearest()?.ContentId=="snow-mountain","唯一山脚入口可接近与查看");
            await Capture("04-SnowMountain");
            City.Interact(City.FindNearest()); await Frames(2);
            Check(City.Dialogue.IsOpen,"山脚入口显示封雪说明");
            await Capture("05-FoothillEntrance"); City.Dialogue.Close();
            City.Mount(2); City.Player.Position=new(905,280); await Frames(2); await Move(InputActions.MoveRight,35);
            Check(City.Player.Position.X>1000,"最快坐骑可穿过东门");
            City.Player.Position=new(1230,290); await Frames(2); await Move(InputActions.MoveDown,40);
            Check(City.Player.Position.Y<375,"最快坐骑也不能越过道路边界"); City.Dismount();
            City.Player.Position=new(1650,8); await Frames(190); AdventureSession.Instance.Clock.SetPosition(9); await Frames(3);
            await Capture("06-MountainNight"); AdventureSession.Instance.Clock.SetPosition(3);
            City.Navigation.Open("地图"); await Frames(3);
            City.Navigation.FindChildren("*","Button",true,false).OfType<Button>().First(b=>b.Text=="本地导览").EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(3); Check(City.Navigation.Page=="本地导览","本地导览可查看新增雪山与整条草路");
            await Capture("07-Map"); City.Navigation.Close();
            City.SetProcess(false); City.Camera.Zoom=Vector2.One*.28f; City.Camera.Position=new(1260,-10);
            await Frames(3); await Capture("08-WorldOverview");
            // 截图在 FramePostDraw 回调里恢复，等回调返回再退出，避免在绘制阶段销毁场景。
            await Frames(3);
            GD.Print($"STARTUP_SNOW_TEST_PASS: {_checks} 项；真实渲染 {DisplayServer.GetName()!="headless"}"); GetTree().Quit();
        }
        catch(Exception e) { GD.PushError("STARTUP_SNOW_TEST_FAIL: "+e); GetTree().Quit(1); }
    }
    private async Task Move(string action,int count) { Input.ActionPress(action); await Frames(count); Input.ActionRelease(action); await Frames(4); }
    private async Task FollowPath(Vector2[] points)
    {
        try
        {
            foreach(Vector2 point in points)
            {
                int frames=0;
                while(City.Player.Position.DistanceTo(point)>12 && frames<600)
                {
                    Vector2 direction=(point-City.Player.Position).Normalized();
                    SetAxis(InputActions.MoveLeft,InputActions.MoveRight,direction.X);
                    SetAxis(InputActions.MoveUp,InputActions.MoveDown,direction.Y);
                    await Frames(2); frames+=2;
                }
                if(frames>=600) throw new InvalidOperationException($"连续行走卡在 {City.Player.Position}，目标 {point}");
            }
        }
        finally
        {
            foreach(string action in new[]{InputActions.MoveLeft,InputActions.MoveRight,InputActions.MoveUp,InputActions.MoveDown}) Input.ActionRelease(action);
        }
        await Frames(5);
    }
    private static void SetAxis(string negative,string positive,float value)
    {
        Input.ActionRelease(negative); Input.ActionRelease(positive);
        if(Mathf.Abs(value)>.01f) Input.ActionPress(value<0 ? negative : positive,Mathf.Abs(value));
    }
    private async Task Frames(int count)
    { for(int i=0;i<count;i++) { await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); } }
    private async Task Capture(string name)
    {
        if(DisplayServer.GetName()=="headless") return;
        City?.Camera.ResetSmoothing(); await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage(); image.SavePng("res://Build/StartupSnow/"+name+".png");
    }
    private void Check(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); _checks++; GD.Print("通过："+message); }
}
