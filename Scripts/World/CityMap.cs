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
    public StaticBody2D Wall(Rect2 rect)
    {
        var body = new StaticBody2D { Position=rect.GetCenter(),CollisionLayer=1,CollisionMask=0 };
        body.AddChild(new CollisionShape2D { Shape=new RectangleShape2D { Size=rect.Size } }); AddChild(body); return body;
    }
    public void RemoveBoulder()
    {
        Broken=true;
        if (IsInstanceValid(BoulderBody)) { BoulderBody.CollisionLayer=0; BoulderBody.QueueFree(); }
        QueueRedraw();
    }
    public override void _Draw()
    {
        DrawGroundDetails();
        DrawCityWalls();
        DrawHiddenLane();
        DrawEastGate();
        DrawYardDetails();
    }

    /// <summary>土面叠加成片的灰绿草地、碎石和磨损边缘。装饰不添加路上碰撞。</summary>
    private void DrawGroundDetails()
    {
        DrawMeadow(new(57,198),new(32,33),17);
        DrawMeadow(new(255,121),new(24,65),31);
        DrawMeadow(new(364,126),new(21,57),43);
        DrawMeadow(new(552,126),new(18,53),59);
        DrawMeadow(new(856,155),new(50,51),67);
        DrawMeadow(new(97,361),new(45,24),79);
        DrawMeadow(new(213,365),new(52,22),83);
        DrawMeadow(new(411,388),new(44,32),97);
        DrawMeadow(new(491,434),new(61,47),101);
        DrawMeadow(new(715,361),new(65,23),113);
        DrawMeadow(new(395,564),new(42,25),127);
        DrawMeadow(new(536,565),new(25,26),139);
        DrawMeadow(new(116,568),new(42,27),151);
        var random=new RandomNumberGenerator { Seed=20261013 };
        for(int x=39;x<924;x+=9) foreach(float y in new[]{239f,321f})
        {
            if(x>=282 && x<=354 || x>=573 && x<=643) continue;
            var p=new Vector2(x+random.RandfRange(-3,3),y+random.RandfRange(-2,2));
            DrawLine(p,p+new Vector2(random.RandfRange(2,6),random.RandfRange(-1,1)),new Color("a59879"),1);
            if(x%4==0) DrawLine(p+new Vector2(2,2),p+new Vector2(0,-3),new Color("8f9871"),1);
        }
        for(int y=88;y<584;y+=11) foreach(float x in new[]{287f,353f,575f,641f})
        {
            if(y>234 && y<326) continue;
            var p=new Vector2(x+random.RandfRange(-2,2),y);
            DrawLine(p,p+new Vector2(random.RandfRange(-1,2),random.RandfRange(2,6)),new Color("a4977c"),1);
            if(y%3==0) DrawCircle(p+new Vector2(2,3),1.2f,new Color("93957d"));
        }
        foreach(float x in new[]{166f,465f,748f})
            for(int i=0;i<3;i++) DrawRect(new Rect2(x-17+(i%2)*3,205+i*8,32,6),new Color(i%2==0 ? "a5a691" : "b6b49c"));
        for(int i=0;i<8;i++) DrawRect(new Rect2(204+i*9,507+(i%2)*3,8,5),new Color("bbb499"));
        DrawColoredPolygon(new Vector2[]{new(644,540),new(778,539),new(786,577),new(640,579)},new Color("b8a47f"));
        for(int i=0;i<30;i++)
        {
            var p=new Vector2(645+random.RandfRange(0,133),543+random.RandfRange(0,29));
            DrawLine(p,p+new Vector2(random.RandfRange(2,6),random.RandfRange(-2,2)),new Color("cbb889"),1);
        }
    }

    private void DrawMeadow(Vector2 center,Vector2 radius,ulong seed)
    {
        var random=new RandomNumberGenerator { Seed=seed };
        var points=new Vector2[14];
        for(int i=0;i<points.Length;i++)
        {
            float angle=i*Mathf.Tau/points.Length;
            points[i]=center+new Vector2(Mathf.Cos(angle)*radius.X,Mathf.Sin(angle)*radius.Y)*random.RandfRange(.8f,1.12f);
        }
        DrawColoredPolygon(points,new Color("a6aa86"));
        for(int i=0;i<40;i++)
        {
            var p=center+new Vector2(random.RandfRange(-radius.X*.74f,radius.X*.74f),random.RandfRange(-radius.Y*.7f,radius.Y*.7f));
            Color tint=i%3==0 ? new("898f70") : new("969d78");
            DrawLine(p,p+new Vector2(-2,-random.RandfRange(2,5)),tint,1);
            if(i%2==0) DrawLine(p,p+new Vector2(2,-3),tint,1);
        }
    }

    /// <summary>石基、夯土墙身和灰瓦压顶，与原来的物理边界重合。</summary>
    private void DrawCityWalls()
    {
        DrawRect(new Rect2(0,0,960,33),new Color("777c6b"));
        DrawRect(new Rect2(0,10,960,16),new Color("a69d7f"));
        DrawRect(new Rect2(0,28,960,6),new Color("858574"));
        DrawRect(new Rect2(0,608,960,32),new Color("a2987d"));
        DrawRect(new Rect2(0,630,960,10),new Color("797c6c"));
        DrawTileCap(new Rect2(0,0,960,12));
        DrawTileCap(new Rect2(0,603,960,13));
        for(int x=0;x<960;x+=28)
        {
            DrawLine(new(x,29),new(x,33),new Color("646e61"),1);
            DrawLine(new(x+14,15),new(x+14,25),new Color("958e74"),1);
            DrawLine(new(x,617),new(x,630),new Color("918970"),1);
        }
        foreach(float x in new[]{0f,932f})
        {
            DrawRect(new Rect2(x,28,28,580),new Color("7c816e"));
            DrawRect(new Rect2(x+4,28,18,580),new Color("aaa181"));
            DrawRect(new Rect2(x+3,28,5,580),new Color("687466"));
            DrawRect(new Rect2(x+8,28,2,580),new Color("a4aa92"));
            DrawRect(new Rect2(x+22,28,4,580),new Color("8b8c73"));
            for(int y=28;y<608;y+=21)
            {
                DrawLine(new(x+3,y),new(x+10,y),new Color("4f6057"),1);
                DrawLine(new(x+12,y+7),new(x+22,y+7),new Color("978f75"),1);
            }
        }
    }

    private void DrawTileCap(Rect2 rect)
    {
        DrawRect(rect,new Color("596b65"));
        DrawLine(rect.Position,rect.Position+new Vector2(rect.Size.X,0),new Color("8f9a86"),2);
        DrawLine(rect.Position+new Vector2(0,rect.Size.Y),rect.End,new Color("3f554f"),2);
        for(float x=rect.Position.X+3;x<rect.End.X;x+=7)
        {
            DrawLine(new(x,rect.Position.Y+2),new(x-2,rect.End.Y-1),new Color("86917c"),1);
            DrawCircle(new(x-2,rect.End.Y),1.3f,new Color("768575"));
        }
    }

    private void DrawHiddenLane()
    {
        DrawRect(new Rect2(824,356,92,250),new Color("b8a37f"));
        DrawRect(new Rect2(842,356,43,240),new Color("c7b18b"));
        foreach(float x in new[]{808f,916f})
        {
            DrawRect(new Rect2(x,356,16,252),new Color("89896f"));
            DrawRect(new Rect2(x+2,356,12,245),new Color("b1a17e"));
            DrawRect(new Rect2(x,355,6,249),new Color("617164"));
            DrawLine(new(x+2,356),new(x+2,606),new Color("9fa68b"),1);
            for(int y=367;y<607;y+=14) DrawLine(new(x,y),new(x+6,y),new Color("405c51"),1);
            for(int y=370;y<598;y+=40)
            {
                DrawRect(new Rect2(x-1,y,17,8),new Color("9d9274"));
                DrawLine(new(x+1,y+2),new(x+14,y+2),new Color("c3b28a"),1);
            }
        }
        DrawTileCap(new Rect2(808,347,28,12));
        DrawTileCap(new Rect2(892,347,40,12));
        for(int i=0;i<12;i++)
        {
            float y=397+i*16;
            DrawLine(new(830,y),new(834,y-5),new Color("7d8868"),1);
            DrawLine(new(903,y+4),new(907,y-2),new Color("7d8868"),1);
            if(i%3==0) DrawRect(new Rect2(851+i%2*10,y,11,5),new Color("adab90"));
        }
        DrawString(_font,new(842,595),"旧驿道",HorizontalAlignment.Left,-1,11,new Color("746e55"));
        // 小型岩窟入口依附旧驿道，保留中央通路与更南侧木匣。
        DrawColoredPolygon(new Vector2[]{new(837,474),new(838,443),new(849,430),new(878,431),new(892,447),new(892,474)},new Color("777e70"));
        DrawColoredPolygon(new Vector2[]{new(848,473),new(848,449),new(857,441),new(873,442),new(881,451),new(881,473)},new Color("293b38"));
        DrawLine(new(838,446),new(850,434),new Color("abb099"),3);
        DrawRect(new Rect2(843,473,43,5),new Color("b0a88b"));
        DrawString(_font,new(843,491),"旧道暗窟",HorizontalAlignment.Left,-1,10,new Color("4c594b"));
        if(!Broken)
        {
            DrawColoredPolygon(new Vector2[]{new(833,379),new(832,356),new(842,338),new(869,332),new(892,347),new(897,373),new(879,388),new(847,386)},new Color("767e73"));
            DrawColoredPolygon(new Vector2[]{new(832,356),new(842,338),new(869,332),new(887,350),new(857,361)},new Color("adb19a"));
            DrawColoredPolygon(new Vector2[]{new(858,361),new(887,350),new(897,373),new(879,388),new(866,377)},new Color("8f9986"));
            DrawPolyline(new Vector2[]{new(862,338),new(853,350),new(865,360),new(858,372)},new Color("535f56"),2);
            DrawLine(new(847,341),new(857,338),new Color("d0ceb2"),2);
            DrawLine(new(835,369),new(847,378),new Color("667562"),2);
        }
        else for(int i=0;i<7;i++) DrawCircle(new(838+i*8,376+(i%2)*6),3,new Color("828a7e"));
        DrawRect(new Rect2(849,570,30,4),new Color(0,0,0,.17f));
        DrawRect(new Rect2(850,553,27,19),new Color("6a4c31")); DrawRect(new Rect2(849,CacheOpen?545:550,29,9),new Color("a27c48"));
        DrawRect(new Rect2(853,553,2,17),new Color("bd9c5f")); DrawRect(new Rect2(873,553,2,17),new Color("bd9c5f"));
        DrawRect(new Rect2(862,551,4,20),new Color("e4bf73"));
    }

    /// <summary>道路末端保留宽门洞，城门交互坐标仍为 (903,294)。</summary>
    private void DrawEastGate()
    {
        DrawRect(new Rect2(886,225,46,18),new Color("8c8c77"));
        DrawRect(new Rect2(891,213,41,24),new Color("bab091"));
        DrawRect(new Rect2(886,322,46,24),new Color("8c8c77"));
        DrawRect(new Rect2(891,316,41,21),new Color("bab091"));
        DrawTileCap(new Rect2(880,206,58,13));
        DrawTileCap(new Rect2(880,310,58,13));
        DrawRect(new Rect2(924,242,8,68),new Color("695b42"));
        DrawLine(new(928,244),new(928,309),new Color("b09c72"),2);
        DrawRect(new Rect2(915,242,9,69),new Color("a6a68d"));
        for(int i=0;i<5;i++) DrawLine(new(916,246+i*13),new(924,246+i*13),new Color("8c9179"),1);
        DrawRect(new Rect2(891,222,34,15),new Color("544d3b"));
        DrawString(_font,new(896,234),"东门",HorizontalAlignment.Left,-1,11,new Color("d9c9a0"));
        DrawString(_font,new(861,278),"演武场",HorizontalAlignment.Left,-1,11,new Color("665e48"));
        DrawLine(new(884,294),new(907,294),new Color("817a60"),2);
        DrawPolyline(new Vector2[]{new(901,289),new(907,294),new(901,299)},new Color("817a60"),2);
    }

    private void DrawYardDetails()
    {
        DrawRect(new Rect2(402,210,126,28),new Color("bda883"));
        for(int x=410;x<=514;x+=52)
        {
            DrawLine(new(x,221),new(x-4,242),new Color("705c41"),3);
            DrawLine(new(x,221),new(x+4,242),new Color("705c41"),2);
            DrawCircle(new(x,221),8,new Color("c6b48b"));
            DrawArc(new(x,221),5,0,Mathf.Tau,16,new Color("8d6750"),1.5f);
            DrawCircle(new(x,221),1.5f,new Color("805240"));
        }
        DrawRect(new Rect2(803,220,26,19),new Color("696a59"));
        DrawRect(new Rect2(800,214,26,19),new Color("6e705f"));
        DrawRect(new Rect2(804,218,18,11),new Color("464f43"));
        DrawCircle(new(813,225),6,new Color("bc793f")); DrawCircle(new(813,226),3,new Color("e0b45e"));
        DrawLine(new(799,214),new(827,214),new Color("a1a38b"),3);
        DrawRect(new Rect2(791,232,10,5),new Color("665b46"));
        // 三处街灯分别采用纸圆灯、木框灯和竹编灯，按建筑用途区分，保持街区克制。
        DrawStreetLamp(new(270,242),0);
        DrawStreetLamp(new(565,242),1);
        DrawStreetLamp(new(840,242),2);
        DrawWell(new(406,514));
        DrawJar(new(90,222),8); DrawJar(new(251,485),7); DrawJar(new(788,572),7);
        DrawBasket(new(91,207),new Color("aa8c5e")); DrawBasket(new(246,224),new Color("ab9870"));
        DrawLine(new(229,213),new(262,217),new Color("786647"),2);
        DrawLine(new(82,460),new(82,498),new Color("7c6848"),2);
        DrawLine(new(65,459),new(65,498),new Color("7c6848"),2);
        DrawLine(new(62,464),new(85,464),new Color("90774f"),2);
        DrawRect(new Rect2(68,466,10,19),new Color("afa589"));
        DrawLine(new(69,483),new(77,483),new Color("8d8971"),1);
        for(int i=0;i<6;i++) DrawLine(new(790+i*2,510),new(786+i*3,487),new Color("b49c61"),1);
        DrawLine(new(789,500),new(801,500),new Color("87714b"),2);
    }

    private void DrawStreetLamp(Vector2 p,int kind)
    {
        DrawRect(new Rect2(p+new Vector2(-5,-4),new Vector2(10,4)),new Color("99997e"));
        DrawLine(p,p+new Vector2(0,-28),new Color("766b4c"),2);
        DrawLine(p+new Vector2(-1,-27),p+new Vector2(12,-27),new Color("766b4c"),2);
        DrawLine(p+new Vector2(10,-27),p+new Vector2(10,-23),new Color("85714f"),1);
        if(kind==0)
        {
            DrawCircle(p+new Vector2(10,-17),6,new Color("cdb483"));
            DrawArc(p+new Vector2(10,-17),4,-Mathf.Pi/2,Mathf.Pi/2,8,new Color("a58d60"),1);
            DrawLine(p+new Vector2(10,-11),p+new Vector2(10,-8),new Color("9a7550"),1);
        }
        else
        {
            DrawRect(new Rect2(p+new Vector2(5,-23),new Vector2(10,13)),new Color(kind==1 ? "665c43" : "a58c5b"));
            DrawRect(new Rect2(p+new Vector2(7,-21),new Vector2(6,9)),new Color("c8b382"));
            if(kind==1) DrawLine(p+new Vector2(3,-24),p+new Vector2(17,-24),new Color("53695c"),2);
            else for(int i=0;i<3;i++) DrawLine(p+new Vector2(5,-21+i*4),p+new Vector2(15,-18+i*4),new Color("967d4e"),1);
        }
    }

    private void DrawWell(Vector2 p)
    {
        DrawSetTransform(p);
        DrawColoredPolygon(new Vector2[]{new(-17,-4),new(-11,-14),new(11,-14),new(18,-4),new(17,9),new(-16,9)},new Color("7e8774"));
        DrawColoredPolygon(new Vector2[]{new(-17,-4),new(-10,-13),new(11,-13),new(18,-4),new(10,3),new(-9,3)},new Color("afb19a"));
        DrawColoredPolygon(new Vector2[]{new(-9,-4),new(-5,-9),new(6,-9),new(10,-4),new(5,-1),new(-5,-1)},new Color("4f645b"));
        DrawLine(new(-10,2),new(-10,8),new Color("636f60"),1);
        DrawLine(new(5,3),new(5,9),new Color("636f60"),1);
        DrawLine(new(-13,-5),new(-13,-35),new Color("7a6545"),3);
        DrawLine(new(14,-5),new(14,-35),new Color("7a6545"),3);
        DrawLine(new(-17,-32),new(18,-32),new Color("8f744b"),3);
        DrawLine(new(0,-31),new(0,-9),new Color("bbaa7c"),1);
        DrawSetTransform(Vector2.Zero);
    }

    private void DrawJar(Vector2 p,float radius)
    {
        DrawColoredPolygon(new Vector2[]{p+new Vector2(-radius*.5f,0),p+new Vector2(-radius,-radius),p+new Vector2(-radius*.6f,-radius*2),p+new Vector2(radius*.6f,-radius*2),p+new Vector2(radius,-radius),p+new Vector2(radius*.5f,0)},new Color("8a795c"));
        DrawLine(p+new Vector2(-radius*.6f,-radius*2),p+new Vector2(radius*.6f,-radius*2),new Color("b4a07a"),3);
        DrawLine(p+new Vector2(-radius*.4f,-radius*1.8f),p+new Vector2(radius*.4f,-radius*1.8f),new Color("5a5d49"),2);
        DrawLine(p+new Vector2(-radius*.5f,-radius*1.3f),p+new Vector2(-radius*.4f,-radius*.5f),new Color("ae9973"),1);
    }

    private void DrawBasket(Vector2 p,Color color)
    {
        DrawColoredPolygon(new Vector2[]{p+new Vector2(-8,-10),p+new Vector2(8,-10),p+new Vector2(6,0),p+new Vector2(-6,0)},color);
        for(int i=0;i<3;i++) DrawLine(p+new Vector2(-7,-8+i*3),p+new Vector2(7,-8+i*3),color.Darkened(.18f),1);
        DrawLine(p+new Vector2(-8,-10),p+new Vector2(8,-10),color.Lightened(.2f),2);
    }
}

public partial class CityTree : Node2D
{
    public override void _Draw()
    {
        // 图集槐树与屋舍采用同一绘制风格，树干脚点及物理碰撞保持不变。
        if(CityArtAtlas.Texture!=null)
        {
            var region=CityArtAtlas.Region(5);
            float width=84,height=width*region.Size.Y/region.Size.X;
            TextureFilter=TextureFilterEnum.Linear;
            DrawTextureRectRegion(CityArtAtlas.Texture,new Rect2(-width*.5f,-height+5,width,height),region);
            return;
        }
        DrawRect(new Rect2(-5,-34,10,34),new Color("756b4c"));
        DrawCircle(new(2,-35),30,new Color("596f54")); DrawCircle(new(-12,-48),23,new Color("6d7f5a"));
        DrawCircle(new(10,-53),25,new Color("7b8960")); DrawCircle(new(0,-66),17,new Color("90996a"));
        DrawCircle(new(14,-64),5,new Color("a3a775"));
    }
}

