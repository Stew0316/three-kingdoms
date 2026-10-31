using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>按实际角色动作逐帧保存魏延预览；固定相机只用于看清关节，动作仍来自真实移动和技能入口。</summary>
internal static class WeiYanMotionCapture
{
    public static async Task Record(Node host, Combatant actor)
    {
        // 等待先前回归用例的强化结束，预览从普通站姿开始；后续强化仍由真实技能触发。
        actor.SetMoveInput(Vector2.Zero);
        while (actor.Status.EmpowerRemaining > 0)
            await host.ToSignal(host.GetTree(),SceneTree.SignalName.PhysicsFrame);
        const string folder = "res://Build/WeiYanV3/Motion";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
        var viewport = new SubViewport { Size = new Vector2I(720, 600), Disable3D = true,
            ProcessMode = Node.ProcessModeEnum.Always, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        host.AddChild(viewport);
        viewport.AddChild(new ColorRect { Size = new Vector2(720,600), Color = new Color("262b27") });
        var root = new Node2D { Position = new Vector2(340,550), Scale = new Vector2(4,3.84f) };
        viewport.AddChild(root);
        var layers = new List<(Node2D source,Node2D copy)>();
        foreach (Node2D source in actor.Rig.ArtworkRoot.GetChildren())
        {
            var copy = RigArtworkCapture.CopyLayer(source);
            root.AddChild(copy); layers.Add((source,copy));
        }
        var title = new Label { Position = new Vector2(24,18), Theme = UiStyles.ChineseTheme(24) };
        title.AddThemeFontSizeOverride("font_size",24);
        viewport.AddChild(title);
        actor.Face(Vector2.Right);
        actor.SetMoveInput(Vector2.Zero);
        actor.Position = new Vector2(260,235);
        for (int frame=0; frame<480; frame++)
        {
            if (frame == 45) actor.SetMoveInput(Vector2.Right);
            if (frame == 125) actor.SetMoveInput(Vector2.Zero);
            if (frame == 165) actor.TryAction(CombatAction.Basic);
            if (frame == 240) actor.TryAction(CombatAction.Sweep);
            if (frame == 330) actor.TryAction(CombatAction.Dash);
            if (frame == 405) { actor.Position = new Vector2(300,235); actor.SetMoveInput(Vector2.Left); }
            if (frame == 450) actor.SetMoveInput(Vector2.Zero);
            await host.ToSignal(host.GetTree(),SceneTree.SignalName.PhysicsFrame);
            await host.ToSignal(host.GetTree(),SceneTree.SignalName.ProcessFrame);
            await host.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            title.Text = frame < 45 ? "魏延 · 站立" : frame < 125 ? "魏延 · 行走" : frame < 165 ? "魏延 · 停步"
                : frame < 240 ? "魏延 · 斜劈" : frame < 330 ? "魏延 · 横斩" : frame < 405 ? "魏延 · 突进" : "魏延 · 转向与收势";
            // 固定相机剔除世界位移，但保留人物整体左右镜像。
            root.Scale = new Vector2(4 * Mathf.Sign(actor.Rig.Scale.X),3.84f);
            foreach (var (source,copy) in layers)
            {
                RigArtworkCapture.SyncLayer(source,copy,actor.Rig.Skeleton.GlobalTransform.AffineInverse()*source.GlobalTransform);
            }
            // 当前源层随空间深度重新排序，复制品采用同一遮挡顺序。
            foreach(Node2D source in actor.Rig.ArtworkRoot.GetChildren())
                foreach(var pair in layers) if(pair.source==source) root.MoveChild(pair.copy,root.GetChildCount()-1);
            if (frame % 2 != 0) continue;
            bool paused=host.GetTree().Paused;
            host.GetTree().Paused=true;
            await host.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using (var image=viewport.GetTexture().GetImage())
            {
                image.SavePng($"{folder}/Frame{frame/2:D3}.png");
                if (frame==30) image.SavePng("res://Build/WeiYanV3/Idle.png");
                if (frame==88) image.SavePng("res://Build/WeiYanV3/Walk.png");
                if (frame==198) image.SavePng("res://Build/WeiYanV3/Attack.png");
            }
            host.GetTree().Paused=paused;
        }
        viewport.QueueFree();
    }
}
