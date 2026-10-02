using Godot;
using System.Collections.Generic;

/// <summary>二维骨骼残影：冻结当前 16 个拆件的世界姿态并逐渐淡出，不依赖 HD2D 视口截图。</summary>
public partial class AfterimageTrail2D : Node2D
{
    private Combatant _actor;
    private float _lastSpawn;
    private readonly List<Ghost> _ghosts=new();
    private readonly CanvasItemMaterial _additive=new() { BlendMode=CanvasItemMaterial.BlendModeEnum.Add };

    private sealed class Ghost
    {
        public Node2D Root;
        public float Remaining;
        public float Lifetime;
    }

    public override void _Ready()
    {
        _actor=GetParent<Combatant>();
        TopLevel=true;
    }

    public override void _Process(double delta)
    {
        if(_actor?.Presentation?.Afterimages==true && !_actor.IsDead && !_actor.BattleFinished
            && _actor.Velocity.Length()>30 && _actor.VisualTime-_lastSpawn>=_actor.Presentation.AfterimageInterval)
        {
            _lastSpawn=_actor.VisualTime;
            SpawnGhost();
        }
        for(int i=_ghosts.Count-1;i>=0;i--)
        {
            Ghost ghost=_ghosts[i];
            ghost.Remaining-=(float)delta;
            if(ghost.Remaining<=0 || _actor.BattleFinished)
            {
                ghost.Root.QueueFree();
                _ghosts.RemoveAt(i);
            }
            else ghost.Root.Modulate=new Color(1,1,1,ghost.Remaining/ghost.Lifetime);
        }
    }

    /// <summary>复制当前每个拆件的局部姿态；残影生成后不再跟随人物和骨骼。</summary>
    private void SpawnGhost()
    {
        if(_actor.Rig.Parts.Count==0 || _actor.GetParent() is not Node2D fighters) return;
        var root=new Node2D { Name=$"{_actor.Name}残影",Position=_actor.Position };
        fighters.AddChild(root);
        fighters.MoveChild(root,0); // 相同脚点时先画残影；不使用会落入地板之下的负 Z。
        Transform2D inverseActor=_actor.GlobalTransform.AffineInverse();
        foreach(Sprite2D part in _actor.Rig.DrawOrderedParts)
        {
            if(!part.Visible || part.Texture==null) continue;
            var copy=new Sprite2D
            {
                Texture=part.Texture,Centered=part.Centered,Offset=part.Offset,FlipH=part.FlipH,FlipV=part.FlipV,
                RegionEnabled=part.RegionEnabled,RegionRect=part.RegionRect,ZIndex=part.ZIndex,
                TextureFilter=part.TextureFilter,Modulate=_actor.Presentation.AfterimageColor,Material=_additive
            };
            root.AddChild(copy);
            copy.Transform=inverseActor*part.GlobalTransform;
        }
        float lifetime=Mathf.Max(.05f,_actor.Presentation.AfterimageLifetime);
        _ghosts.Add(new Ghost { Root=root,Remaining=lifetime,Lifetime=lifetime });
        if(_ghosts.Count<=6) return;
        _ghosts[0].Root.QueueFree();
        _ghosts.RemoveAt(0);
    }
}
