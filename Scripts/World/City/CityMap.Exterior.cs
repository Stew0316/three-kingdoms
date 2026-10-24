using Godot;
using System.Collections.Generic;

public partial class CityMap
{
    /// <summary>城外只开放草地路带，路外与山体封闭，入口前设置终止面。</summary>
    private void BuildExterior()
    {
        AddChild(new SnowMountainExterior());
        var north=new List<Vector2>(SnowMountainLayout.UpperBank) { new(1608,-704),new(932,-704) };
        AddExteriorCollision(north.ToArray());
        var south=new List<Vector2>(SnowMountainLayout.LowerBank) { new(1692,-704),new(2304,-704),new(2304,640),new(932,640) };
        AddExteriorCollision(south.ToArray());
        Wall(new(1608,-704,84,692));
    }
    private void AddExteriorCollision(Vector2[] points)
    {
        var body=new StaticBody2D { Name="ExteriorBoundary",CollisionLayer=PhysicsLayers.WorldBody,CollisionMask=PhysicsLayers.None };
        body.AddChild(new CollisionPolygon2D { Polygon=points }); AddChild(body);
    }
}
