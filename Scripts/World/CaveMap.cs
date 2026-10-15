using Godot;

/// <summary>灰青岩壁、暖土路与旧木支架；碰撞和遮光均由不可行走格生成。</summary>
public partial class CaveMap : Node2D
{
    public Node2D Actors { get; private set; }
    public override void _Ready()
    {
        BuildFloor();
        Actors = new Node2D { Name="Actors", YSortEnabled=true }; AddChild(Actors);
        for(int y=0;y<CaveLayout.Rows;y++) for(int x=0;x<CaveLayout.Columns;x++)
        {
            if(CaveLayout.IsFloor(x,y)) continue;
            var body=new StaticBody2D { Position=new(x*32+16,y*32+16), CollisionLayer=1, CollisionMask=0 };
            body.AddChild(new CollisionShape2D { Shape=new RectangleShape2D { Size=new(32,32) } }); AddChild(body);
            body.AddChild(new LightOccluder2D { Occluder=new OccluderPolygon2D {
                Polygon=new Vector2[]{new(-16,-16),new(16,-16),new(16,16),new(-16,16)}
            }});
        }
    }
    private void BuildFloor()
    {
        using var img=Image.CreateEmpty(128,32,false,Image.Format.Rgba8);
        var rng=new RandomNumberGenerator { Seed=14102026 };
        for(int k=0;k<4;k++)
        {
            Color soil=new("9a8d75"); img.FillRect(new Rect2I(k*32,0,32,32),soil);
            for(int n=0;n<65;n++) img.SetPixel(k*32+rng.RandiRange(0,31),rng.RandiRange(0,31),soil.Lightened(rng.RandfRange(-.035f,.035f)));
        }
        var atlas=new TileSetAtlasSource { Texture=ImageTexture.CreateFromImage(img),TextureRegionSize=new(32,32) };
        for(int i=0;i<4;i++) atlas.CreateTile(new(i,0));
        var set=new TileSet { TileSize=new(32,32) }; set.AddSource(atlas,0);
        var floor=new TileMapLayer { Name="Ground",TileSet=set,ZIndex=-2 }; AddChild(floor);
        for(int y=0;y<CaveLayout.Rows;y++) for(int x=0;x<CaveLayout.Columns;x++)
            if(CaveLayout.IsFloor(x,y)) floor.SetCell(new(x,y),0,new(rng.RandiRange(0,3),0));
    }
    public override void _Draw()
    {
        for(int y=0;y<CaveLayout.Rows;y++) for(int x=0;x<CaveLayout.Columns;x++)
        {
            Vector2 p=new(x*32,y*32);
            if(CaveLayout.IsFloor(x,y))
            {
                if(!CaveLayout.IsFloor(x-1,y) || !CaveLayout.IsFloor(x,y-1) || !CaveLayout.IsFloor(x+1,y))
                {
                    // 墙根潮痕集中在边缘，以灰绿块面衔接岩壁与暖土地面。
                    DrawColoredPolygon(new Vector2[]{p+new Vector2(1,1),p+new Vector2(29,2),p+new Vector2(27,7),p+new Vector2(16,5),p+new Vector2(3,10)},new Color("777d66"));
                    if((x+y)%3==0) DrawLine(p+new Vector2(5,8),p+new Vector2(10,6),new Color("969781"),2);
                }
                if((x*7+y*11)%13==0) // 零星大块石片，不把纹理铺满地面。
                    DrawColoredPolygon(new Vector2[]{p+new Vector2(9,18),p+new Vector2(16,14),p+new Vector2(24,18),p+new Vector2(22,22),p+new Vector2(11,23)},new Color("898873"));
                continue;
            }
            DrawRect(new Rect2(p,new(32,32)),new Color("3e4947"));
            if(CaveLayout.IsFloor(x,y+1))
            {
                // 岩壁的落地边与阻挡格下沿一致，面向角色留出蜡烛壁龛。
                DrawColoredPolygon(new Vector2[]{p+new Vector2(0,8),p+new Vector2(11,5),p+new Vector2(32,10),p+new Vector2(32,32),p+new Vector2(0,32)},new Color("6e756b"));
                DrawLine(p+new Vector2(0,10),p+new Vector2(32,12),new Color("929480"),2);
                DrawLine(p+new Vector2(0,30),p+new Vector2(32,30),new Color("4a554e"),3);
                DrawPolyline(new Vector2[]{p+new Vector2(15,13),p+new Vector2(12,20),p+new Vector2(21,27)},new Color("575f57"),1);
            }
            if(CaveLayout.IsFloor(x+1,y)) DrawRect(new Rect2(p+new Vector2(25,0),new(7,32)),new Color("747b6b"));
            if(CaveLayout.IsFloor(x-1,y)) DrawRect(new Rect2(p,new(7,32)),new Color("59675e"));
            if(CaveLayout.IsFloor(x,y-1)) DrawRect(new Rect2(p,new(32,5)),new Color("858a76"));
        }
        // 废弃驿道留下不连续的旧石板，指向石室之间的开口。
        foreach(var segment in new (Vector2 From,Vector2 To)[]{
            (new(193,587),new(234,330)),(new(236,314),new(360,305)),
            (new(374,306),new(601,370)),(new(620,359),new(883,321)),(new(893,305),new(942,190))})
        {
            int count=(int)segment.From.DistanceTo(segment.To)/34;
            for(int i=0;i<count;i++)
            {
                Vector2 p=segment.From.Lerp(segment.To,(i+.5f)/count);
                if(!CaveLayout.IsFloor((int)p.X/32,(int)p.Y/32)) continue;
                DrawColoredPolygon(new Vector2[]{p+new Vector2(-10,-5),p+new Vector2(7,-7),p+new Vector2(12,2),p+new Vector2(8,7),p+new Vector2(-11,5)},new Color(i%3==0 ? "aaa28a" : "918f7b"));
                DrawLine(p+new Vector2(-9,-4),p+new Vector2(6,-6),new Color("b3ab94"),1);
            }
        }
        // 旧驿路石阶与榫接支木沿墙布置，保持通路开敞。
        int index=0;
        foreach(var c in CaveLayout.Candles)
        {
            Vector2 p=c.Position;
            if(index++%2==1)
            {
                // 深处壁龛使用石质托座，与入口的旧木支架区分。
                DrawColoredPolygon(new Vector2[]{p+new Vector2(-24,-10),p+new Vector2(-17,-28),p+new Vector2(16,-26),p+new Vector2(25,-8)},new Color("828574"));
                DrawRect(new Rect2(p+new Vector2(-19,-10),new(38,6)),new Color("b0a98d"));
                continue;
            }
            DrawRect(new Rect2(p+new Vector2(-46,-9),new(7,38)),new Color("66533e"));
            DrawRect(new Rect2(p+new Vector2(40,-9),new(7,38)),new Color("66533e"));
            DrawRect(new Rect2(p+new Vector2(-49,-11),new(99,7)),new Color("887354"));
            DrawLine(p+new Vector2(-43,-5),p+new Vector2(-29,7),new Color("aa9270"),3);
            DrawLine(p+new Vector2(43,-5),p+new Vector2(29,7),new Color("aa9270"),3);
        }
        for(int i=0;i<4;i++) DrawRect(new Rect2(169,593+i*9,46,6),new Color("a9a48a"));
        DrawRect(new Rect2(CaveLayout.Supplies-new Vector2(12,16),new(24,18)),new Color("725637"));
        DrawLine(CaveLayout.Supplies+new Vector2(-12,-13),CaveLayout.Supplies+new Vector2(12,-13),new Color("c1a474"),3);
        DrawLine(CaveLayout.Supplies+new Vector2(-7,-21),CaveLayout.Supplies+new Vector2(8,-10),new Color("bd9d64"),3);
    }
}
