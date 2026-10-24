using Godot;

/// <summary>雪山与草地雪线的独立表现，碰撞由首城按共享路带构建。</summary>
public partial class SnowMountainExterior : Node2D
{
    public override void _Ready()
    {
        Name="SnowMountainExterior"; ZIndex=-4;
        var groundMaterial=new ShaderMaterial { Shader=GD.Load<Shader>("res://Resources/Shaders/ColdMeadow.gdshader") };
        groundMaterial.SetShaderParameter("path_points",SnowMountainLayout.PathPolygon());
        // 地面延伸到城池背后并位于城内 TileMap 之下，拉远镜头不会出现灰色空洞。
        var terrain=new Polygon2D { Name="MeadowTerrain",ZIndex=-2,Polygon=new[] {new Vector2(-512,-960),new Vector2(2816,-960),new Vector2(2816,960),new Vector2(-512,960)},
            Texture=GD.Load<Texture2D>(SnowMountainLayout.MeadowTexture),TextureRepeat=TextureRepeatEnum.Enabled,
            TextureFilter=TextureFilterEnum.LinearWithMipmaps,
            Material=groundMaterial };
        AddChild(terrain);
        var texture=GD.Load<Texture2D>(SnowMountainLayout.MountainTexture);
        AddChild(new Sprite2D { Name="SnowMountain",Texture=texture,Centered=false,
            Position=SnowMountainLayout.MountainBounds.Position,
            Scale=SnowMountainLayout.MountainBounds.Size/texture.GetSize(),TextureFilter=TextureFilterEnum.LinearWithMipmaps });
        BuildBanks();
    }
    private void BuildBanks()
    {
        using var random=new RandomNumberGenerator { Seed=20261024 };
        var rock=TransparentArtwork.Load(SnowMountainLayout.BankTexture);
        foreach(var bank in new[]{SnowMountainLayout.UpperBank,SnowMountainLayout.LowerBank})
        for(int s=0;s<bank.Length-1;s++)
        {
            Vector2 a=bank[s],b=bank[s+1]; int count=Mathf.CeilToInt(a.DistanceTo(b)/60);
            for(int i=0;i<count;i++)
            {
                Vector2 p=a.Lerp(b,(i+.5f)/count);
                if(p.X<1000 || p.Y<125) continue;
                bool upper=bank==SnowMountainLayout.UpperBank;
                // 岩脊主体放在不可走的一侧，只让零碎石沿伸到边界，保持路带清楚。
                Vector2 outward=(b-a).Normalized().Orthogonal()*(upper ? 1 : -1);
                float width=a.DistanceTo(b)/count+27;
                AddChild(new Sprite2D { Name="RockyRoadBank",Texture=rock,Position=p+outward*16,
                    Scale=new Vector2(width/rock.GetWidth(),.65f*width/rock.GetWidth()),
                    Rotation=random.RandfRange(-.15f,.15f),FlipH=random.Randf()>.5f,
                    TextureFilter=TextureFilterEnum.LinearWithMipmaps });
            }
        }
    }
}
