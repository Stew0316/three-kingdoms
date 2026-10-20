using Godot;

/// <summary>独立地面阴影采样，椭圆不随躯干上浮；参数是半径与0到1透明度。</summary>
public static class HorseShadow
{
    public readonly record struct Pose(Vector2 Offset,Vector2 Scale,float Opacity);
    public static Pose Sample(float phase,float weight,float bodyWidth=1)
    {
        float lift=Mathf.SmoothStep(0,6,HorseLocomotion.GroundClearance(phase,weight));
        return new(new(0,3),new Vector2(34*bodyWidth,6)*Mathf.Lerp(1,.88f,lift),Mathf.Lerp(.14f,.10f,lift));
    }
}
