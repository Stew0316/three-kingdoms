using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public partial class LvBuBackPreview
{
    /// <summary>固定冠翎挂点，只比较实机材质像素，排除呼吸动作造成的假风动；同时检查暂停。</summary>
    private async Task CheckIdleWind(bool capture)
    {
        _actor.SetMoveInput(Vector2.Zero); _walking=false;
        for(int frame=0;frame<30;frame++) await Tick();
        var names=new[] { "背面左冠翎", "背面右冠翎" };
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
                // 上方容纳拱起，下方给加长后的尾梢留出空间。
                var root=new Node2D { Position=new Vector2(200,164), Scale=Vector2.One*4 };
                views[index].AddChild(root);
                var source=Rig.ArtworkRoot.GetChildren().OfType<Sprite2D>().Single(item=>item.Name==names[index]);
                var copy=(Sprite2D)RigArtworkCapture.CopyLayer(source); root.AddChild(copy); copies[index]=copy;
                copy.Transform=Rig.Joints[15].GlobalTransform.AffineInverse()*source.GlobalTransform;
                var config=_actor.Presentation.Model.Attachments.Single(item=>item.LayerName==names[index]);
                roots[index]=(Vector2I)copy.ToGlobal(config.ClothRoot).Round();
            }
            await CheckPlumeProportions(views,copies,roots,names);
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

    /// <summary>关闭风动比较真实Alpha边界，验证上下段比例与完整取景，避免仅核对资源数值。</summary>
    private async Task CheckPlumeProportions(SubViewport[] views,Sprite2D[] copies,Vector2I[] roots,string[] names)
    {
        var originals=copies.Select(copy=>copy.Material).ToArray();
        var before=new Vector2[2];
        var after=new Vector2[2];
        try
        {
            for(int index=0;index<2;index++)
            {
                var material=(ShaderMaterial)originals[index].Duplicate();
                material.SetShaderParameter("cloth_weight",0f);
                material.SetShaderParameter("cloth_vertical_scale",Vector2.One);
                copies[index].Material=material;
            }
            await Tick();
            for(int index=0;index<2;index++)
            {
                using var image=views[index].GetTexture().GetImage();
                before[index]=MeasureVerticalExtent(image,roots[index].Y);
                var config=_actor.Presentation.Model.Attachments.Single(item=>item.LayerName==names[index]);
                ((ShaderMaterial)copies[index].Material).SetShaderParameter("cloth_vertical_scale",config.ClothVerticalScale);
            }
            await Tick();
            for(int index=0;index<2;index++)
            {
                using var image=views[index].GetTexture().GetImage();
                after[index]=MeasureVerticalExtent(image,roots[index].Y);
                var expected=before[index]*_actor.Presentation.Model.Attachments.Single(item=>item.LayerName==names[index]).ClothVerticalScale;
                // Alpha过滤与像素取整允许两个像素误差，不能吞掉整段或因绘制边界不足而裁切。
                if(Mathf.Abs(after[index].X-expected.X)>2 || Mathf.Abs(after[index].Y-expected.Y)>2)
                    throw new InvalidOperationException($"{names[index]}上下段比例不符：修改前{before[index]}，修改后{after[index]}，预期{expected}");
            }
            var report=Enumerable.Range(0,2).Select(index=>new { Layer=names[index],
                BeforeAbove=before[index].X, AfterAbove=after[index].X, AboveRatio=after[index].X/before[index].X,
                BeforeBelow=before[index].Y, AfterBelow=after[index].Y, BelowRatio=after[index].Y/before[index].Y }).ToArray();
            string json=JsonSerializer.Serialize(report,new JsonSerializerOptions { WriteIndented=true });
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(Output+"/PlumeProportionAudit.json"),json);
            GD.Print(json);
        }
        finally { for(int index=0;index<2;index++) copies[index].Material=originals[index]; }
    }

    private static Vector2 MeasureVerticalExtent(Image image,int rootY)
    {
        int top=image.GetHeight(),bottom=-1;
        for(int y=0;y<image.GetHeight();y++)
            for(int x=0;x<image.GetWidth();x++)
                if(image.GetPixel(x,y).A>.08f) { top=Math.Min(top,y); bottom=Math.Max(bottom,y); }
        if(top<=0 || bottom>=image.GetHeight()-1 || top>=rootY || bottom<=rootY)
            throw new InvalidOperationException("冠翎形状缺失或超出比例检查取景范围");
        return new Vector2(rootY-top,bottom-rootY);
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
