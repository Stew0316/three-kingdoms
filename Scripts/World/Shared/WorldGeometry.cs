using Godot;

/// <summary>地图碰撞创建的公共入口，矩形尺寸同时用于物理阻挡与可选遮光。</summary>
public static class WorldGeometry
{
    public static StaticBody2D AddWall(Node parent, Rect2 bounds, bool blocksLight = false)
    {
        var body = new StaticBody2D
        {
            Position = bounds.GetCenter(), CollisionLayer = PhysicsLayers.WorldBody, CollisionMask = PhysicsLayers.None
        };
        body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = bounds.Size } });
        if (blocksLight)
        {
            Vector2 half = bounds.Size / 2;
            body.AddChild(new LightOccluder2D
            {
                Occluder = new OccluderPolygon2D
                {
                    Polygon = new[] { -half, new Vector2(half.X, -half.Y), half, new Vector2(-half.X, half.Y) }
                }
            });
        }
        parent.AddChild(body);
        return body;
    }
}
