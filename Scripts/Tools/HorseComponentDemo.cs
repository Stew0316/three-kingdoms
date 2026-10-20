using Godot;
using System.Linq;

/// <summary>直接运行即可预览的独立组件样板，不创建城市、战斗人物或碰撞世界。</summary>
public partial class HorseComponentDemo : Node2D
{
    private HorseActor[] _horses;
    public override void _Ready() => _horses=GetChildren().OfType<HorseActor>().ToArray();
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if(input is not InputEventKey {Pressed:true,Echo:false} key) return;
        if(key.Keycode==Key.Space)
        {
            bool playing=!_horses[0].AutoPlayPreview;
            foreach(var horse in _horses) horse.AutoPlayPreview=playing;
        }
        else if(key.Keycode==Key.R)
            foreach(var horse in _horses) { horse.AutoPlayPreview=false; horse.ResetGait(); }
        else if(key.Keycode is Key.Left or Key.Right)
            foreach(var horse in _horses)
            {
                horse.PreviewFaceLeft=key.Keycode==Key.Left;
                if(!horse.AutoPlayPreview) horse.SetPreviewPose(horse.RenderGaitPhase,horse.GaitWeight,horse.PreviewFaceLeft?-1:1);
            }
    }
}
