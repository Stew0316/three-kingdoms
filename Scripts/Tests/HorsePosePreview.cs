using Godot;
using System.Threading.Tasks;

/// <summary>用正式CityHorse绘制器逐相位渲染马匹与关节链，输出可循环慢放的完整一轮。</summary>
public partial class HorsePosePreview : Node2D
{
    private float _phase, _weight;
    private Font _font;
    public override void _Ready() => _font = new SystemFont { FontNames = GameFontNames.CreateChineseFallbacks() };
    public override void _Draw()
    {
        DrawRect(new Rect2(0,0,960,550),new Color("e6dfcf"));
        DrawString(_font,new(24,35),"坐骑关节与步态 · 正式绘制器逐帧采样",HorizontalAlignment.Left,-1,24,new Color("332e28"));
        DrawString(_font,new(24,66),_weight==0 ? "静止承重" : $"完整小跑周期 {_phase*100:0}% · 左：外观   右：近侧关节链",HorizontalAlignment.Left,-1,18,new Color("625749"));
        for(int row=0;row<2;row++)
        {
            float ground=260+row*245;
            DrawLine(new(50,ground),new(910,ground),new Color("b7aa94"),2);
            DrawString(_font,new(48,ground-173),row==0 ? "枣红马" : "青骢马",HorizontalAlignment.Left,-1,18,new Color("625749"));
            float width=row==0 ? 1.07f : .95f;
            foreach(bool front in new[]{false,true})
            {
                var pose=HorseLocomotion.Sample(front,true,_phase,_weight,width);
                Vector2[] points={pose.Root,pose.Proximal,pose.Joint,pose.Fetlock,pose.Hoof};
                for(int i=0;i<points.Length;i++) points[i]=new Vector2(690,ground-6.75f)+points[i]*2.7f;
                DrawPolyline(points,front ? new Color("97472d") : new Color("356b79"),4,true);
                for(int i=0;i<points.Length;i++) DrawCircle(points[i],4.2f,new Color("332e28"));
                string[] labels=front ? new[]{"肩","肘","腕","球节","蹄"} : new[]{"髋","膝","飞节","球节","蹄"};
                for(int i=0;i<3;i++) DrawString(_font,points[i]+new Vector2(front ? 9 : -39,-7),labels[i],HorizontalAlignment.Left,-1,15,new Color("332e28"));
                DrawString(_font,new(front ? 720 : 575,ground+23),pose.Supporting || _weight==0 ? "承重" : "屈腿回收",HorizontalAlignment.Left,-1,15,new Color("625749"));
            }
        }
    }

    public static async Task Capture(SceneTree tree)
    {
        if(DisplayServer.GetName()=="headless") return;
        const string directory="res://Build/horse-joints/preview";
        DirAccess.MakeDirRecursiveAbsolute(directory);
        var viewport=new SubViewport { Size=new(960,550),TransparentBg=false,RenderTargetUpdateMode=SubViewport.UpdateMode.Always };
        tree.Root.AddChild(viewport);
        var backdrop=new HorsePosePreview(); viewport.AddChild(backdrop);
        var brown=new CityHorse { Position=new(230,253.25f),Scale=new(2.7f,2.7f),Appearance=GD.Load<HorseAppearance>("res://Resources/Config/World/horse-chestnut.tres") };
        var dapple=new CityHorse { Position=new(230,498.25f),Scale=new(2.7f,2.7f),Appearance=GD.Load<HorseAppearance>("res://Resources/Config/World/horse-dapple.tres") };
        viewport.AddChild(brown); viewport.AddChild(dapple);
        for(int frame=-1;frame<48;frame++)
        {
            float phase=Mathf.Max(0,frame)/48f, weight=frame<0 ? 0 : 1;
            backdrop._phase=phase; backdrop._weight=weight; backdrop.QueueRedraw();
            brown.SetPreviewPose(phase,weight); dapple.SetPreviewPose(phase,weight);
            await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using var image=viewport.GetTexture().GetImage();
            image.SavePng(directory+(frame<0 ? "/standing.png" : $"/cycle-{frame:00}.png"));
        }
        viewport.QueueFree();
    }
}
