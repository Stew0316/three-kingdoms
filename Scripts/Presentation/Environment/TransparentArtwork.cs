using Godot;
using System.Collections.Generic;

/// <summary>保留原始 PNG，仅用透明包围盒采样；缓存避免重复解码大型素材。</summary>
public static class TransparentArtwork
{
    private static readonly Dictionary<string,AtlasTexture> Cache=new();
    public static AtlasTexture Load(string path)
    {
        if(Cache.TryGetValue(path,out var cached)) return cached;
        var texture=GD.Load<Texture2D>(path);
        using var image=texture.GetImage();
        if(image.IsCompressed()) image.Decompress();
        // 生成图可能带极淡的透明余像，不能让它们把器物本体挤小。只据可见 Alpha 收紧采样。
        int left=image.GetWidth(),top=image.GetHeight(),right=-1,bottom=-1;
        for(int y=0;y<image.GetHeight();y++) for(int x=0;x<image.GetWidth();x++)
            if(image.GetPixel(x,y).A>.16f) { left=Mathf.Min(left,x);top=Mathf.Min(top,y);right=Mathf.Max(right,x);bottom=Mathf.Max(bottom,y); }
        Rect2 region=right>=left ? new Rect2(Mathf.Max(0,left-2),Mathf.Max(0,top-2),Mathf.Min(image.GetWidth()-Mathf.Max(0,left-2),right-left+5),Mathf.Min(image.GetHeight()-Mathf.Max(0,top-2),bottom-top+5)) : image.GetUsedRect();
        var result=new AtlasTexture { Atlas=texture,Region=region };
        Cache[path]=result;
        return result;
    }
}
