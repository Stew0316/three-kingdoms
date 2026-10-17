using Godot;
using System.Linq;

/// <summary>洞窟布局的单一来源：地面、碰撞、光线遮挡与蜡烛稳定 ID 使用同一坐标。</summary>
public static class CaveLayout
{
    public const int CellSize = 32, Columns = 36, Rows = 22;
    public static readonly Vector2 Entrance = new(192, 586);
    public static readonly Vector2 Supplies = new(256, 590);
    public static readonly Rect2I[] Rooms = {
        new(2,13,8,7), new(3,3,10,8), new(6,10,3,4),
        new(12,8,3,3), new(14,8,9,10), new(22,9,4,3), new(25,3,9,9)
    };
    public static readonly (string Id, string Title, Vector2 Position)[] Candles = {
        ("old-road:entrance", "入口石室", new(144,432)),
        ("old-road:west", "西侧旧廊", new(240,112)),
        ("old-road:central", "中央石室", new(592,272)),
        ("old-road:east", "东侧深窟", new(944,112))
    };
    public static bool IsFloor(int x, int y)
    {
        if(!Rooms.Any(r => r.HasPoint(new Vector2I(x,y)))) return false;
        // 岩室拐角削去整格岩块，碰撞和阴影随之变化；连接走廊始终保留至少三格宽。
        return (x,y) is not (2,13) and not (2,14) and not (9,13) and not (2,19) and not (9,19)
            and not (3,3) and not (4,3) and not (12,3) and not (12,4) and not (3,10)
            and not (14,8) and not (22,8) and not (14,17) and not (15,17) and not (22,17)
            and not (25,3) and not (26,3) and not (33,3) and not (33,4) and not (33,11);
    }
    public static bool IsCandleId(string id) => Candles.Any(c => c.Id == id);
}
