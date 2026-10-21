using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>天下图内容目录；坐标对应 Town.png，路线节点独立于图片，后续城市可绑定场景。</summary>
public sealed class WorldAtlas
{
    public sealed class City
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public string ScenePath { get; set; } = "";
        public Vector2 Position => new(X, Y);
    }
    public sealed class Point
    {
        public float X { get; set; }
        public float Y { get; set; }
        public Vector2 Position => new(X, Y);
    }
    public sealed class Route
    {
        public string From { get; set; }
        public string To { get; set; }
        public string Kind { get; set; }
        public Point[] Via { get; set; } = Array.Empty<Point>();
    }
    public int Width { get; set; }
    public int Height { get; set; }
    public City[] Cities { get; set; } = Array.Empty<City>();
    public Route[] Routes { get; set; } = Array.Empty<Route>();
    public City Find(string id) => Cities.First(c => c.Id == id);
    public IEnumerable<Route> Connections(string id) => Routes.Where(r => r.From == id || r.To == id);
    public Vector2[] Polyline(Route route) =>
        new[] { Find(route.From).Position }.Concat(route.Via.Select(p => p.Position)).Append(Find(route.To).Position).ToArray();

    public static WorldAtlas Load()
    {
        using var file = FileAccess.Open("res://Resources/Config/WorldMap/town-map.json", FileAccess.ModeFlags.Read);
        var atlas = JsonSerializer.Deserialize<WorldAtlas>(file.GetAsText().TrimStart('\uFEFF'));
        if (atlas == null || atlas.Cities.Length == 0 || atlas.Cities.Select(c => c.Id).Distinct().Count() != atlas.Cities.Length)
            throw new InvalidOperationException("天下图城池目录无效");
        foreach (var route in atlas.Routes) { atlas.Find(route.From); atlas.Find(route.To); }
        return atlas;
    }
}
