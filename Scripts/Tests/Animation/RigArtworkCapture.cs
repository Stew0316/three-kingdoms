using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>复制真实绘制层供视觉验收，包含图片拆件与程序冠翎网格，不重新拼造角色。</summary>
internal static class RigArtworkCapture
{
    /// <summary>同步换图与变形网格，不只复制变换；否则预览会把背面动作画成正脸或冻结披风。</summary>
    public static void SyncLayer(Node2D source, Node2D copy, Transform2D transform)
    {
        copy.Visible=source.Visible; copy.Transform=transform; copy.Modulate=source.Modulate;
        if(source is Sprite2D original && copy is Sprite2D sprite)
        {
            sprite.Texture=original.Texture; sprite.FlipH=original.FlipH; sprite.FlipV=original.FlipV; sprite.Material=original.Material;
        }
        if(source is MeshInstance2D originalMesh && copy is MeshInstance2D mesh)
        {
            mesh.Mesh=originalMesh.Mesh; mesh.Texture=originalMesh.Texture; mesh.Material=originalMesh.Material;
        }
        copy.GetParent().MoveChild(copy,copy.GetParent().GetChildCount()-1);
    }
    public static Node2D CopyLayer(Node2D source)
    {
        return source switch
        {
            Sprite2D sprite => new Sprite2D { Texture = sprite.Texture, Material = sprite.Material, Centered = sprite.Centered,
                FlipH = sprite.FlipH, FlipV = sprite.FlipV, TextureFilter = sprite.TextureFilter, Modulate = sprite.Modulate },
            MeshInstance2D mesh => new MeshInstance2D { Mesh = mesh.Mesh, Material = mesh.Material, Texture = mesh.Texture, Modulate = mesh.Modulate },
            _ => throw new InvalidOperationException($"未支持的角色绘制层：{source.GetType().Name}")
        };
    }

    public static async Task Record(Node host, Combatant actor)
    {
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://Build/LvBuV4Motion"));
        var viewport = new SubViewport { Size = new Vector2I(720, 640), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        host.AddChild(viewport);
        viewport.AddChild(new ColorRect { Size = new Vector2(720, 640), Color = new Color(.095f,.095f,.095f) });
        var root = new Node2D { Position = new Vector2(360, 590), Scale = new Vector2(4, 3.68f) };
        viewport.AddChild(root);
        var layers = new List<(Node2D source, Node2D copy)>();
        foreach (Node2D source in actor.Rig.ArtworkRoot.GetChildren())
        {
            var copy = CopyLayer(source); root.AddChild(copy); layers.Add((source, copy));
        }
        actor.Position = new Vector2(250, 235);
        actor.Face(Vector2.Right);
        actor.SetMoveInput(Vector2.Zero);
        for (int frame = 0; frame < 330; frame++)
        {
            if (frame == 60) actor.SetMoveInput(Vector2.Right);
            if (frame == 180) actor.SetMoveInput(Vector2.Zero);
            await host.ToSignal(host.GetTree(), SceneTree.SignalName.PhysicsFrame);
            await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
            foreach (var (source, copy) in layers)
            {
                SyncLayer(source,copy,actor.Rig.Skeleton.GlobalTransform.AffineInverse()*source.GlobalTransform);
            }
            await host.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            if (frame % 2 == 0)
            {
                using var image = viewport.GetTexture().GetImage();
                image.SavePng($"res://Build/LvBuV4Motion/Frame{frame / 2:D3}.png");
                if (frame == 30) image.SavePng("res://Build/LvBuReferenceIdleV4.png");
            }
        }
        viewport.QueueFree();
    }
}
