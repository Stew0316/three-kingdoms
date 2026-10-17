using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>城镇与洞窟共用的近距查询：先按距离排序，再检查视线是否被世界碰撞遮挡。</summary>
public static class InteractionQuery
{
    /// <param name="isAvailable">地图自己的目标有效性规则，例如已打开的宝箱不再交互。</param>
    /// <param name="endpointTolerance">允许射线终点命中目标本身的距离；不传时任何遮挡都阻断。</param>
    public static CityInteractable FindNearest(Node2D origin, IEnumerable<CityInteractable> targets,
        Func<CityInteractable, bool> isAvailable = null,
        Func<CityInteractable, float> endpointTolerance = null, float range = 48)
    {
        foreach (var target in targets.OrderBy(item => item.GlobalPosition.DistanceSquaredTo(origin.GlobalPosition)))
        {
            if (isAvailable != null && !isAvailable(target)) continue;
            if (target.GlobalPosition.DistanceSquaredTo(origin.GlobalPosition) > range * range) continue;

            var ray = PhysicsRayQueryParameters2D.Create(origin.GlobalPosition, target.GlobalPosition, PhysicsLayers.WorldBody);
            var hit = origin.GetWorld2D().DirectSpaceState.IntersectRay(ray);
            if (hit.Count == 0) return target;
            if (endpointTolerance != null && ((Vector2)hit["position"]).DistanceTo(target.GlobalPosition) <= endpointTolerance(target))
                return target;
        }
        return null;
    }
}
