using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public partial class LvBuDownPreview
{
    /// <summary>固定冠翎挂点，只比较实机材质像素，排除呼吸动作造成的假风动；同时检查暂停。</summary>
    private async Task CheckIdleWind(bool capture)
    {
        _actor.SetMoveInput(Vector2.Zero); _walking=false;
        for(int frame=0;frame<30;frame++) await Tick();
        var names=new[] { "正面左冠翎", "正面右冠翎" };
        var views=new SubViewport[2];
        var copies=new Sprite2D[2];
        var roots=new Vector2I[2];
        var baseline=new Image[2];
        var changed=new int[2];
        var rootChanged=new int[2];
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Output+"/IdleWindFrames"));
        try
        {
            for(int index=0;index<2;index++)
            {
                views[index]=new SubViewport { Size=new Vector2I(400,512), Disable3D=true,
                    TransparentBg=true, RenderTargetUpdateMode=SubViewport.UpdateMode.Always };
                AddChild(views[index]);
                // 为两根独立冠翎的上拱与尾梢保留完整取景范围。
                var root=new Node2D { Position=new Vector2(200,164), Scale=Vector2.One*4 };
                views[index].AddChild(root);
                var source=Rig.ArtworkRoot.GetChildren().OfType<Sprite2D>().Single(item=>item.Name==names[index]);
                var copy=(Sprite2D)RigArtworkCapture.CopyLayer(source); root.AddChild(copy); copies[index]=copy;
                copy.Transform=Rig.Joints[15].GlobalTransform.AffineInverse()*source.GlobalTransform;
                var config=_actor.Presentation.Model.Attachments.Single(item=>item.LayerName==names[index]);
                roots[index]=(Vector2I)copy.ToGlobal(config.ClothRoot).Round();
            }
            
            await Tick();
            for(int index=0;index<2;index++) baseline[index]=views[index].GetTexture().GetImage();
            for(int frame=0;frame<120;frame++)
            {
                await Tick();
                if(capture && frame%2==0)
                {
                    GetTree().Paused=true;
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    using var preview=GetViewport().GetTexture().GetImage();
                    preview.SavePng($"{Output}/IdleWindFrames/Frame{frame/2:D3}.png");
                    GetTree().Paused=false;
                }
                if(frame%30!=29) continue;
                for(int index=0;index<2;index++)
                {
                    using var current=views[index].GetTexture().GetImage();
                    changed[index]=Math.Max(changed[index],ChangedPixels(baseline[index],current,new Rect2I(0,0,400,512)));
                    rootChanged[index]=Math.Max(rootChanged[index],ChangedPixels(baseline[index],current,new Rect2I(roots[index]-Vector2I.One*3,Vector2I.One*7)));
                }
            }
            GetTree().Paused=true;
            using var paused=views[0].GetTexture().GetImage();
            await Tick(); await Tick();
            using var afterPause=views[0].GetTexture().GetImage();
            bool pausePassed=ChangedPixels(paused,afterPause,new Rect2I(0,0,400,512))==0;
            GetTree().Paused=false;
            string json=JsonSerializer.Serialize(new { LeftChangedPixels=changed[0], RightChangedPixels=changed[1],
                RootChangedPixels=rootChanged, PausePassed=pausePassed },new JsonSerializerOptions { WriteIndented=true });
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(Output+"/WindAudit.json"),json);
            GD.Print(json);
            if(changed.Any(value=>value<25) || rootChanged.Any(value=>value>0) || !pausePassed)
                throw new InvalidOperationException("待机冠翎风动、根部固定或暂停像素检查未通过");
        }
        finally
        {
            GetTree().Paused=false;
            foreach(var image in baseline) image?.Dispose();
            foreach(var view in views) view?.QueueFree();
        }
    }

    private static int ChangedPixels(Image first,Image second,Rect2I region)
    {
        int count=0;
        for(int y=region.Position.Y;y<region.End.Y;y++)
            for(int x=region.Position.X;x<region.End.X;x++)
                if(first.GetPixel(x,y)!=second.GetPixel(x,y)) count++;
        return count;
    }
}
