using Godot;

/// <summary>首城的 TileMap 地面、五栋建筑与物理边界；隐藏小路只有巨石一处入口。</summary>
public partial class CityMap : Node2D
{
    public StaticBody2D BoulderBody { get; private set; }
    public bool Broken { get; set; }
    public bool CacheOpen { get; set; }
    public Node2D Actors { get; private set; }
    private Font _font;
    public override void _Ready()
    {
        _font = new SystemFont { FontNames = GameFontNames.CreateChineseFallbacks() };
        BuildTiles();
        Actors = new Node2D { Name = "Actors", YSortEnabled = true }; AddChild(Actors);
        // 小屋采用紧凑 RPG 比例，缩小屋体，不移动门前 NPC 和任务交互点。
        AddBuilding(new(166,202),new(136,100),"槐安驿站",new("66706a"),0);
        AddBuilding(new(465,202),new(144,108),"军营 · 演武堂",new("68716b"),1);
        AddBuilding(new(748,202),new(128,96),"铁匠铺",new("6e7067"),2);
        AddBuilding(new(169,496),new(128,96),"槐下民居",new("6b7466"),3);
        AddBuilding(new(704,532),new(166,124),"马厩",new("786752"),4);
        Wall(new(0,0,960,32)); Wall(new(0,608,960,32)); Wall(new(0,0,28,640)); Wall(new(932,0,28,640));
        // 独立封闭口袋：北口宽56像素，碎石后徒步/骑马均可通过，不可绕侧面取得宝箱。
        Wall(new(808,356,16,252)); Wall(new(916,356,16,252));
        Wall(new(808,350,28,16)); Wall(new(892,350,40,16));
        BoulderBody = Wall(new(836,350,56,34));
        if (Broken) RemoveBoulder();
        foreach(var p in new Vector2[]{new(56,93),new(307,76),new(580,96),new(880,126),new(60,565),new(354,525),new(531,510)})
        {
            var tree = new CityTree { Position=p }; Actors.AddChild(tree);
            Wall(new Rect2(p-new Vector2(7,6),new Vector2(14,12)));
        }
    }
    private void BuildTiles()
    {
        // 地面保持 60×40 个真实 TileMap 格子。同类瓦片只有细微纹理差异，避免棋盘草坪。
        using var image = Image.CreateEmpty(192,16,false,Image.Format.Rgba8);
        var random = new RandomNumberGenerator { Seed=314159 };
        for(int tile=0;tile<12;tile++)
        {
            int group=tile/4;
            Color groundColor=group==0 ? new("b1b092") : group==1 ? new("c7b596") : new("b8b6a1");
            image.FillRect(new Rect2I(tile*16,0,16,16),groundColor);
            for(int i=0;i<55;i++) image.SetPixel(tile*16+random.RandiRange(0,15),random.RandiRange(0,15),groundColor.Lightened(random.RandfRange(-.052f,.047f)));
            if(group==2)
            {
                // 石板缝错位，明暗克制；土路仍留在铺装两侧。
                int seam=tile%2==0 ? 5 : 10;
                image.FillRect(new Rect2I(tile*16,0,16,1),new Color("a4a48e"));
                image.FillRect(new Rect2I(tile*16+seam,0,1,9),new Color("a3a38d"));
                image.FillRect(new Rect2I(tile*16,9,16,1),new Color("aaa893"));
                image.FillRect(new Rect2I(tile*16+(seam+7)%16,10,1,6),new Color("a5a38e"));
                image.FillRect(new Rect2I(tile*16+1,1,seam-1,1),new Color("c9c5ae"));
            }
        }
        var atlas = new TileSetAtlasSource { Texture=ImageTexture.CreateFromImage(image), TextureRegionSize=new(16,16) };
        for(int i=0;i<12;i++) atlas.CreateTile(new(i,0));
        var set = new TileSet { TileSize=new(16,16) }; set.AddSource(atlas,0);
        var ground = new TileMapLayer { Name="Ground",TileSet=set,TextureFilter=TextureFilterEnum.Nearest }; AddChild(ground);
        ground.ZIndex=-5;
        for(int y=0;y<40;y++) for(int x=0;x<60;x++)
        {
            bool road = y>=15 && y<=19 || x>=18 && x<=21 && y>=5 && y<=36 || x>=36 && x<=39 && y>=5 && y<=36;
            bool courtyard = x>=25 && x<=33 && y>=13 && y<=15 || x>=6 && x<=14 && y>=13 && y<=15 || x>=42 && x<=50 && y>=13 && y<=15 || x>=40 && x<=49 && y>=33 && y<=35;
            bool paving=y>=17 && y<=18 || (x==19 || x==38) && y>=6 && y<=36;
            ground.SetCell(new(x,y),0,new((paving ? 8 : road||courtyard ? 4 : 0)+random.RandiRange(0,3),0));
        }
    }
    private void AddBuilding(Vector2 position, Vector2 size,string title,Color roof,int kind)
    {
        Actors.AddChild(new CityBuilding { Position=position, BuildingSize=size,Title=title,Roof=roof,Kind=kind,ZIndex=kind==4 ? -1 : 0 });
        if(kind!=4) Wall(new Rect2(position-new Vector2(size.X/2,size.Y-14),new Vector2(size.X,size.Y-14)));
        else
        {
            Wall(new Rect2(position-new Vector2(size.X/2,size.Y-14),new Vector2(size.X,15)));
            Wall(new Rect2(position-new Vector2(size.X/2,size.Y-14),new Vector2(8,size.Y-14)));
            Wall(new Rect2(position+new Vector2(size.X/2-8,-size.Y+14),new Vector2(8,size.Y-14)));
        }
    }
    public StaticBody2D Wall(Rect2 rect) => WorldGeometry.AddWall(this, rect);
    public void RemoveBoulder()
    {
        Broken=true;
        if (IsInstanceValid(BoulderBody)) { BoulderBody.CollisionLayer=0; BoulderBody.QueueFree(); }
        QueueRedraw();
    }
}
