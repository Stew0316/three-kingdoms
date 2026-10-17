using Godot;

/// <summary>角色骨架初始化与资源绑定。</summary>
public partial class CharacterRig
{
    /// <summary>建立固定业务关节和父子关系，武将配置覆盖默认绑定。</summary>
    private void BuildSkeleton()
    {
        Skeleton = new Skeleton2D { Name = "Skeleton2D" }; AddChild(Skeleton);
        Vector2[] joints = CharacterRigDefaults.CreateJointPositions();
        if (Actor.Presentation?.Model.CalibratedLegProportions == true)
        {
            var proportions = Actor.Presentation.Model;
            _pelvisHeight = proportions.PelvisHeight;
            _thighLength = proportions.ThighLength;
            _lowerLegLength = proportions.LowerLegLength;
            joints[0] = new Vector2(0, -_pelvisHeight);
            // 髋点位于骨盆内侧，不能跟随脚距一起向外挪；腿从胯下连接。
            joints[9] = new Vector2(-proportions.HipHalfWidth, 2);
            joints[11] = new Vector2(proportions.HipHalfWidth, 2);
            joints[10] = joints[12] = new Vector2(0, _thighLength);
        }
        if (Actor.Presentation?.Animation.ReferencePoseSet == true)
        {
            // 依据三姿态样板重新绑定上半身；手脚目标共用脚底原点，不从旧持械角度继承。
            joints[1] = new Vector2(0, -11);
            joints[2] = new Vector2(3, -12);
            joints[3] = new Vector2(-7, -6);
            joints[6] = new Vector2(8, -5);
            joints[4] = joints[7] = new Vector2(0, 12);
            joints[5] = joints[8] = new Vector2(0, 12);
            joints[13] = new Vector2(-5, -9);
            joints[15] = new Vector2(-1, -10);
            // 长戟独立于手臂骨链：先确定武器与握点，再由双臂追踪，允许单手释放。
            _parents[14] = -1;
            joints[14] = Vector2.Zero;
        }
        // 正式武将使用自己的完整绑定；上述默认值仅供旧原型或内存测试纹理回退。
        if (Actor.Presentation.Model.JointPositions.Count == CharacterRigDefaults.JointCount)
            for (int index = 0; index < CharacterRigDefaults.JointCount; index++) joints[index] = Actor.Presentation.Model.JointPositions[index];
        for (int i=0;i<CharacterRigDefaults.JointCount;i++)
        {
            var bone = new Bone2D { Name = CharacterRigDefaults.JointNames[i], Position = joints[i] };
            bone.SetAutocalculateLengthAndAngle(false);
            bone.SetLength(i is 9 or 11 ? _thighLength : i is 10 or 12 ? _lowerLegLength : 11);
            bone.Rest = bone.Transform;
            (_parents[i] < 0 ? (Node)Skeleton : _bones[_parents[i]]).AddChild(bone); _bones[i] = bone;
        }
    }
}
