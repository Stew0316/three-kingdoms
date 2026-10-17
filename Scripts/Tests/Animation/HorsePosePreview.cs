using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>用正式CityHorse绘制器逐相位渲染马匹与关节链，输出可循环慢放的完整一轮。</summary>
public partial class HorsePosePreview : Node2D
{
    private float _phase, _weight;
    private Font _font;
    private static readonly Color Ink = new("332e28");
    private static readonly Color[] LegColors = { new("82a5ae"),new("306c7d"),new("d39a79"),new("a24c2c") };
    private static readonly string[] LegNames = { "远后", "近后", "远前", "近前" };
    private const float PreviewScale = 3.15f;
    public override void _Ready() => _font = new SystemFont { FontNames = GameFontNames.CreateChineseFallbacks() };
    public override void _Draw()
    {
        DrawRect(new Rect2(0,0,1120,740),new Color("e6dfcf"));
        DrawString(_font,new(24,34),"三拍跑步 · 正式马匹绘制器与四肢关节链",HorizontalAlignment.Left,-1,24,Ink);
        DrawString(_font,new(24,62),_weight==0 ? "静止承重 · 左：两马外观   右：四条腿（深色为近侧，浅色为远侧）" :
            $"完整周期 {_phase*100:0.0}% · {PhaseLabel(_phase)} · 左：两马外观   右：四条腿",HorizontalAlignment.Left,-1,18,new Color("625749"));
        DrawPhaseTimeline();
        DrawLine(new(535,133),new(535,698),new Color("cabea8"),1);
        for(int row=0;row<2;row++)
        {
            float ground=350+row*305;
            DrawLine(new(45,ground),new(1080,ground),new Color("b7aa94"),2);
            // 远蹄的地面投影单独标出，不把远近腿的6像素深度差误看成离地。
            DrawLine(new(575,ground-6*PreviewScale),new(1080,ground-6*PreviewScale),new Color("d1c5b0"),1);
            DrawString(_font,new(45,ground-190),row==0 ? "枣红马" : "青骢马",HorizontalAlignment.Left,-1,18,new Color("625749"));
            float width=row==0 ? 1.07f : .95f;
            Vector2 origin=new(815,ground-2.5f*PreviewScale);
            // 先画远侧，再画近侧，保持与正式马匹相同的遮挡层次。
            foreach(int leg in new[]{0,2,1,3})
            {
                bool front=leg>=2, near=leg%2==1;
                var pose=HorseLocomotion.Sample(front,near,_phase,_weight,width);
                Vector2[] points={pose.Root,pose.Proximal,pose.Joint,pose.Fetlock,pose.Hoof};
                for(int i=0;i<points.Length;i++) points[i]=origin+points[i]*PreviewScale;
                DrawPolyline(points,LegColors[leg],near ? 4.5f : 3,true);
                for(int i=0;i<points.Length;i++)
                {
                    DrawCircle(points[i],near ? 4.3f : 3.5f,LegColors[leg]);
                    DrawCircle(points[i],1.5f,new Color("f4ecdc"));
                }
                if(near)
                {
                    string[] labels=front ? new[]{"肩","肘","腕"} : new[]{"髋","膝","飞节"};
                    for(int i=0;i<labels.Length;i++) DrawString(_font,points[i]+new Vector2(front ? 10 : -39,-7),labels[i],HorizontalAlignment.Left,-1,15,Ink);
                }
                bool supporting=pose.Supporting || _weight==0;
                if(supporting) DrawLine(points[^1]+new Vector2(-8,2),points[^1]+new Vector2(8,2),LegColors[leg],3,true);
                DrawString(_font,new(592+leg*123,ground+30),LegNames[leg]+(supporting ? " · 接地" : " · 回收"),HorizontalAlignment.Left,-1,16,LegColors[leg]);
            }
            Vector2 shoulder=HorseLocomotion.Sample(true,true,_phase,_weight,width).Root;
            Vector2 hip=HorseLocomotion.Sample(false,true,_phase,_weight,width).Root;
            DrawLine(origin+hip*PreviewScale,origin+shoulder*PreviewScale,new Color("6d6151"),2,true);
        }
        DrawString(_font,new(24,724),"落蹄顺序：远后 → 近后＋远前 → 近前 → 腾空；48帧逐相位输出，播放速度另按190/250移速换算。",HorizontalAlignment.Left,-1,16,new Color("625749"));
    }

    private static string PhaseLabel(float phase) => phase<.22f ? "后肢发力" : phase<.46f ? "对角承重" : phase<.76f ? "前肢接地" : "腾空回收";

    private void DrawPhaseTimeline()
    {
        float[] boundaries={0,.22f,.46f,.76f,1};
        string[] labels={"① 后肢发力","② 对角承重","③ 前肢接地","腾空回收"};
        Color[] colors={new("bbcfca"),new("d0d4b6"),new("d9c3a6"),new("b9cbd4")};
        for(int section=0;section<4;section++)
        {
            float left=24+1072*boundaries[section], width=1072*(boundaries[section+1]-boundaries[section]);
            DrawRect(new Rect2(left,82,width-2,32),colors[section]);
            DrawString(_font,new(left+11,104),labels[section],HorizontalAlignment.Left,-1,17,Ink);
        }
        if(_weight>0)
        {
            float cursor=24+1072*_phase;
            DrawLine(new(cursor,77),new(cursor,119),Ink,2,true);
            DrawCircle(new(cursor,78),3.5f,Ink);
        }
    }

    public static async Task Capture(SceneTree tree)
    {
        if(DisplayServer.GetName()=="headless") return;
        const string directory="res://Build/horse-canter/preview";
        DirAccess.MakeDirRecursiveAbsolute(directory);
        var viewport=new SubViewport { Size=new(1120,740),TransparentBg=false,RenderTargetUpdateMode=SubViewport.UpdateMode.Always };
        tree.Root.AddChild(viewport);
        var backdrop=new HorsePosePreview(); viewport.AddChild(backdrop);
        var brown=new CityHorse { Position=new(240,350-2.5f*PreviewScale),Scale=Vector2.One*PreviewScale,Appearance=GD.Load<HorseAppearance>("res://Resources/Config/World/horse-chestnut.tres") };
        var dapple=new CityHorse { Position=new(240,655-2.5f*PreviewScale),Scale=Vector2.One*PreviewScale,Appearance=GD.Load<HorseAppearance>("res://Resources/Config/World/horse-dapple.tres") };
        viewport.AddChild(brown); viewport.AddChild(dapple);
        for(int frame=-1;frame<48;frame++)
        {
            float phase=Mathf.Max(0,frame)/48f, weight=frame<0 ? 0 : 1;
            backdrop._phase=phase; backdrop._weight=weight; backdrop.QueueRedraw();
            brown.SetPreviewPose(phase,weight); dapple.SetPreviewPose(phase,weight);
            await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using var image=viewport.GetTexture().GetImage();
            image.SavePng(directory+(frame<0 ? "/standing.png" : $"/canter-{frame:00}.png"));
        }
        viewport.QueueFree();
    }

    /// <summary>去掉身体和其它三条腿的遮挡，检查真正渲染出的腿皮肤与蹄是否连成一片。</summary>
    public static async Task<(float Ratio,string Context)> CaptureLimbConnections(SceneTree tree)
    {
        const int cellWidth=256,cellHeight=256,columns=4;
        const string directory="res://Build/horse-canter/limb-connections";
        DirAccess.MakeDirRecursiveAbsolute(directory);
        var viewport=new SubViewport { Size=new(cellWidth*columns,cellHeight*2),TransparentBg=true,RenderTargetUpdateMode=SubViewport.UpdateMode.Always };
        tree.Root.AddChild(viewport);
        var rigs=new HorsePaintedRig[8];
        int[] hoofParts={21,13,17,9};
        for(int i=0;i<rigs.Length;i++)
        {
            int leg=i%4;
            var appearance=GD.Load<HorseAppearance>(i<4 ? "res://Resources/Config/World/horse-chestnut.tres" : "res://Resources/Config/World/horse-dapple.tres");
            rigs[i]=new HorsePaintedRig {Appearance=appearance,Scale=Vector2.One*4,
                Position=new(leg*cellWidth+128+(leg>=2?-18:19)*appearance.BodyWidth*4,(i/4)*cellHeight+220)};
            viewport.AddChild(rigs[i]);
            foreach(var child in rigs[i].GetNode<Node2D>("彩绘层").GetChildren().OfType<CanvasItem>())
                child.Visible=child==rigs[i].LimbSkins[leg] || child.Name==$"部件{hoofParts[leg]:00}";
        }
        float minimum=1; string context="";
        for(int frame=-1;frame<48;frame++)
        {
            foreach(var rig in rigs) rig.ApplyPose(Mathf.Max(0,frame)/48f,frame<0?0:1);
            await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using var image=viewport.GetTexture().GetImage();
            image.Convert(Image.Format.Rgba8);
            byte[] pixels=image.GetData();
            for(int i=0;i<8;i++)
            {
                float ratio=ConnectedRatio(pixels,image.GetWidth(),i%4*cellWidth,i/4*cellHeight,cellWidth,cellHeight);
                if(ratio<minimum) {minimum=ratio;context=$"马{i/4+1}，腿{i%4}，帧{frame}";}
            }
            if(frame<0 || frame%6==0) image.SavePng(directory+(frame<0?"/standing.png":$"/canter-{frame:00}.png"));
        }
        viewport.QueueFree();
        return (minimum,context);
    }

    private static float ConnectedRatio(byte[] pixels,int imageWidth,int left,int top,int width,int height)
    {
        var solid=new bool[width*height]; var queue=new int[solid.Length];
        int total=0,largest=0;
        for(int y=0;y<height;y++) for(int x=0;x<width;x++)
        {
            // 忽略边缘抗锯齿的极淡像素；实色皮肤和蹄必须属于同一连通区域。
            bool present=pixels[((top+y)*imageWidth+left+x)*4+3]>=64;
            solid[y*width+x]=present; if(present) total++;
        }
        for(int start=0;start<solid.Length;start++)
        {
            if(!solid[start]) continue;
            int read=0,write=1; queue[0]=start; solid[start]=false;
            while(read<write)
            {
                int pixel=queue[read++],x=pixel%width,y=pixel/width;
                for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++)
                {
                    int nx=x+dx,ny=y+dy;
                    if(nx<0 || nx>=width || ny<0 || ny>=height) continue;
                    int next=ny*width+nx;
                    if(solid[next]) {solid[next]=false;queue[write++]=next;}
                }
            }
            largest=Math.Max(largest,write);
        }
        return total>0?(float)largest/total:0;
    }
}
