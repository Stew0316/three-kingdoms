/// <summary>场景树中由代码查找的固定节点名称。</summary>
public static class NodeNames
{
    public const string Visual = "Visual";
    public const string Health = "Health";
    public const string Hurtbox = "Hurtbox";
    public const string AttackHitbox = "AttackHitbox";
    public const string CollisionShape = "CollisionShape2D";
    public const string Feedback = "Feedback";
    public const string Controller = "Controller";
    public const string Rig = "Rig";
    public const string WeaponTrail = "WeaponTrail";
    public const string ColorRect = "ColorRect";
    public const string Fighters = "Fighters";
    public const string LvBu = "LvBu";
    public const string WeiYan = "WeiYan";
    public const string Hud = "HUD";
    public const string Layout = "Layout";
    public const string Skeleton = "Skeleton2D";
    public const string Skin = "Skin";
    public const string AnimationPlayer = "AnimationPlayer";
    public const string BoneOverlay = "BoneOverlay";
}

/// <summary>跨多级场景节点访问时使用的固定路径。</summary>
public static class SceneNodePaths
{
    public const string Player = "Fighters/LvBu";
    public const string Enemy = "Fighters/WeiYan";
    public const string SkinToSkeleton = "../Skeleton2D";
}

/// <summary>Godot 属性轨道及主题 API 使用的固定属性名称。</summary>
public static class GodotPropertyNames
{
    public const string Rotation = "rotation";
    public const string PanelStyle = "panel";
    public const string BackgroundStyle = "background";
    public const string FillStyle = "fill";
    public const string NormalStyle = "normal";
    public const string HoverStyle = "hover";
    public const string PressedStyle = "pressed";
    public const string FontSize = "font_size";
    public const string FontColor = "font_color";
}

/// <summary>Godot 资源和 Inspector 使用的固定约定字符串。</summary>
public static class GodotResourceNames
{
    public const string DefaultAnimationLibrary = "";
    public const string MotionStrengthRange = "0,1.5,0.05";
}
