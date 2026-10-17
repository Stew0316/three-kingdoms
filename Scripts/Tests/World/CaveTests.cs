using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>隔离存档的洞窟验收，覆盖物品使用门槛、真实点火/碰撞/切图和局部光照像素。</summary>
public partial class CaveTests : Node
{
    private int _checks;
    private OldRoadCave Cave => (OldRoadCave)GetTree().CurrentScene;
    private AdventureSession Session => AdventureSession.Instance;
    public override void _Ready() => CallDeferred(MethodName.Run);
    private async void Run()
    {
        try
        {
            await Frames(5);
            DirAccess.MakeDirRecursiveAbsolute("res://Build/cave");
            Check(Cave.Darkness.Color.R<.15f && Cave.Candles.Values.All(c=>!c.Burning),"洞内初始变暗且四盏壁烛均未点燃");
            Check(Cave.Map.GetNode<TileMapLayer>("Ground").GetUsedCells().Count>300,"洞窟使用真实TileMap地面");
            Check(!Session.UseTorch(),"无火把物品不能使用");
            Check(!Session.Progress.LightCaveCandle(CaveLayout.Candles[0].Id,true),"没有物品时伪造使用状态也不能点火");
            await Visit(CaveLayout.Candles[0].Id);
            Press(Cave,Key.E); Check(Session.Progress.LitCaveCandles.Count==0,"无火把时实际交互不能点火");
            // 每个目标都要在连通的地面上，避免只靠瞬移测试掩盖断路。
            var seen=new HashSet<Vector2I>(); var queue=new Queue<Vector2I>();
            queue.Enqueue(new(6,18)); seen.Add(new(6,18));
            while(queue.TryDequeue(out var p)) foreach(var d in new[]{Vector2I.Left,Vector2I.Right,Vector2I.Up,Vector2I.Down})
            { var q=p+d; if(CaveLayout.IsFloor(q.X,q.Y) && seen.Add(q)) queue.Enqueue(q); }
            foreach(var candle in CaveLayout.Candles)
                Check(seen.Contains(new((int)candle.Position.X/32,(int)(candle.Position.Y+32)/32)),"地面通路可达："+candle.Title);
            await Visit("supplies"); Press(Cave,Key.E);
            Check(Session.Progress.HasTorch && !Session.TorchInUse,"领取物品入袋，但不会自动使用");
            Cave.Dialogue.Close();
            await Visit(CaveLayout.Candles[0].Id); Press(Cave,Key.E);
            Check(Session.Progress.LitCaveCandles.Count==0,"仅拥有未使用火把不能点火");
            Press(Cave,Key.I); Check(Cave.Dialogue.IsOpen,"I打开实际行囊界面");
            await Capture("01-backpack");
            Vector2 before=Cave.Player.Position; Input.ActionPress("move_right"); await Frames(8); Input.ActionRelease("move_right");
            Check(Cave.Player.Position.DistanceTo(before)<1,"行囊打开时不能移动");
            Choose(Cave.Dialogue,"使用火把"); await Frames(3);
            Check(Session.TorchInUse && Cave.Torch.Light.Enabled,"使用按钮开启真实随身光源");
            await Capture("02-torch");
            // 灯光对比使用相同镜头与位置，排除随身光及火焰的影响。
            Session.PutAwayTorch(); await Frames(3);
            using var dark=await Capture("03-before-candle");
            Session.UseTorch(); Press(Cave,Key.E); await Frames(3); Session.PutAwayTorch(); await Frames(3);
            Check(Session.Progress.LitCaveCandles.Count==1 && Cave.Candles.Values.Count(c=>c.Light.Enabled)==1,"点亮一盏只启用一个固定区域光源");
            using var lit=await Capture("04-one-region");
            if(dark!=null && lit!=null)
            {
                Vector2 near=CaveLayout.Candles[0].Position+new Vector2(0,60), far=new(298,610);
                float dn=Brightness(dark,near),ln=Brightness(lit,near),df=Brightness(dark,far),lf=Brightness(lit,far);
                Check(ln>dn*1.8f && ln>dn+.06f,$"实机像素：近处明显增亮 {dn:0.000} → {ln:0.000}");
                Check(Math.Abs(lf-df)<.025f,$"实机像素：远处保持黑暗 {df:0.000} → {lf:0.000}");
            }
            Session.UseTorch(); Press(Cave,Key.E); Check(Session.Progress.LitCaveCandles.Count==1,"重复点火幂等且不消耗火把");
            Check(Session.Progress.Items[AdventureProgress.TorchId]==1,"多次使用仍持有一支火把");
            Check(!Session.Progress.LightCaveCandle("unknown",true),"未知蜡烛ID不能写入进度");
            // 跨墙交互：西侧蜡烛在岩壁另一侧，即使距离够近也必须拒绝。
            var west=Cave.Interactions.First(i=>i.ContentId==CaveLayout.Candles[1].Id);
            Cave.Player.Position=west.Position-new Vector2(0,42); await Frames(3);
            Cave.Interact(west); Check(!Session.Progress.LitCaveCandles.Contains(west.ContentId),"岩壁阻止隔墙点火");
            Cave.Player.Position=new(113,210); await Frames(3); Input.ActionPress("move_left"); await Frames(25); Input.ActionRelease("move_left");
            Check(Cave.Player.Position.X>=107,"岩壁实际阻挡身体，轮廓与碰撞一致");
            foreach(var c in CaveLayout.Candles.Skip(1)) { await Visit(c.Id); Press(Cave,Key.E); await Frames(2); Check(Cave.Candles[c.Id].Light.Enabled,"独立点亮："+c.Title); }
            await Capture("05-deep-cave");
            Check(AdventureSaveStore.Write("res://Build/cave/isolated-save.json",Session.Progress)==Error.Ok,"隔离磁盘存档成功");
            var loaded=AdventureSaveStore.Read("res://Build/cave/isolated-save.json",Session.Config);
            Check(loaded.HasTorch && loaded.LitCaveCandles.Count==4,"火把和四盏蜡烛从磁盘恢复");
            using(var file=FileAccess.Open("res://Build/cave/legacy-save.json",FileAccess.ModeFlags.Write))
                file.StoreString("{\"Version\":1,\"Coins\":345,\"BoulderBroken\":true,\"TrainingCount\":2}");
            var legacy=AdventureSaveStore.Read("res://Build/cave/legacy-save.json",Session.Config);
            Check(!legacy.HasTorch && legacy.LitCaveCandles.Count==0 && legacy.Coins==345 && legacy.TrainingCount==2,"旧档兼容且保留原有成长和货币");
            Session.Progress.AcceptTrial(); Session.Progress.Smash();
            await Visit("exit"); Press(Cave,Key.E); Choose(Cave.Dialogue,"返回槐安"); await Frames(8);
            var city=(FirstCity)GetTree().CurrentScene;
            Check(city.Player.Position.DistanceTo(new(864,512))<2,"洞窟出口返回城市正确入口");
            city.Player.Position=new(864,506); await Frames(3); Press(city,Key.E);
            Check(city.Dialogue.IsOpen,"城市洞窟入口可交互"); Choose(city.Dialogue,"进入洞窟"); await Frames(8);
            Check(Cave.Candles.Values.All(c=>c.Light.Enabled) && !Cave.Player.IsMounted,"重入还原全部壁烛，洞内徒步");
            Session.PutAwayTorch(); await Frames(3);
            Check(Cave.Candles.Values.All(c=>c.Light.Enabled) && !Cave.Torch.Light.Enabled,"收起随身火把不熄灭固定壁烛");
            Session.Progress.BoulderBroken=false;
            await Visit("exit"); Press(Cave,Key.E); Choose(Cave.Dialogue,"返回槐安"); await Frames(8);
            Check(((FirstCity)GetTree().CurrentScene).Player.Position.DistanceTo(new(857,294))<2,"独立运行洞窟且未破石时返回城门安全点");
            GD.Print($"CAVE_TEST_PASS: {_checks} 项；真实渲染 {DisplayServer.GetName()!="headless"}"); GetTree().Quit();
        }
        catch(Exception error) { GD.PushError("CAVE_TEST_FAIL: "+error); GetTree().Quit(1); }
    }
    private async Task Visit(string id)
    {
        Cave.Dialogue.Close(); var item=Cave.Interactions.First(i=>i.ContentId==id);
        Cave.Player.Position=item.Position+new Vector2(0,id=="exit" ? -28 : 30); Cave.Player.SetMoveInput(Vector2.Zero);
        await Frames(4); Check(Cave.FindNearest()?.ContentId==id,"可接近并选择："+id);
    }
    private static void Press(Node node,Key key)
    { using var input=new InputEventKey { Keycode=key,PhysicalKeycode=key,Pressed=true }; node._UnhandledInput(input); }
    private static IEnumerable<Button> Buttons(Node root)
    { foreach(var child in root.GetChildren()) { if(child is Button b) yield return b; foreach(var b2 in Buttons(child)) yield return b2; } }
    private static void Choose(Node root,string text) => Buttons(root).First(b=>b.Text.StartsWith(text)).EmitSignal(BaseButton.SignalName.Pressed);
    private async Task Frames(int count)
    { for(int i=0;i<count;i++) { await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); } }
    private async Task<Image> Capture(string name)
    {
        if(DisplayServer.GetName()=="headless") return null;
        Cave.Camera.ResetSmoothing(); await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        var img=GetViewport().GetTexture().GetImage(); img.SavePng("res://Build/cave/"+name+".png"); return img;
    }
    private float Brightness(Image image,Vector2 world)
    {
        Vector2 screen=Cave.GetGlobalTransformWithCanvas()*world;
        Vector2 scale=new(image.GetWidth()/GetViewport().GetVisibleRect().Size.X,image.GetHeight()/GetViewport().GetVisibleRect().Size.Y);
        Vector2 pixel=screen*scale; float total=0;
        for(int y=-3;y<=3;y++) for(int x=-3;x<=3;x++)
        { Color c=image.GetPixel(Math.Clamp((int)pixel.X+x,0,image.GetWidth()-1),Math.Clamp((int)pixel.Y+y,0,image.GetHeight()-1)); total+=(c.R+c.G+c.B)/3; }
        return total/49;
    }
    private void Check(bool ok,string text) { if(!ok) throw new InvalidOperationException(text); _checks++; GD.Print("通过："+text); }
}
