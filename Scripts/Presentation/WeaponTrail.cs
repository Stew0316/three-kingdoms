using Godot;

/// <summary>原创弧形刀光：宽拖尾、亮刃和火星，只读施法时钟，不产生伤害。</summary>
public partial class WeaponTrail : Node2D
{
    [Export] public bool EffectsEnabled { get; set; } = true; // 关闭装饰后保留 Combatant 的危险预警。
    public bool IsEmitting { get; private set; } // 供调试与回归检查使用。
    private Combatant _actor; // 特效跟随的角色。
    /// <summary>绑定宿主并使用加色混合，让亮刃在地面之上形成短时发光效果。</summary>
    public override void _Ready()
    {
        _actor = GetParent<Combatant>();
        ZIndex = PresentationZIndexes.WeaponTrail;
        Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
    }
    /// <summary>读取规则时钟判断是否绘制；delta 不参与独立计时，暂停时不会继续播放。</summary>
    public override void _Process(double delta)
    {
        // 从伤害生效起计算的秒数；负值表示仍在前摇，额外 0.15 秒仅显示残光。
        float time = _actor.ActionTime - _actor.Spec.Prepare;
        IsEmitting = EffectsEnabled && !_actor.BattleFinished && _actor.CurrentState == Combatant.State.Attack
            && time >= 0 && time < _actor.Spec.Active + .15f;
        QueueRedraw();
    }
    /// <summary>依照锁定方向绘制拖尾和亮刃，不查询目标，也不修改伤害。</summary>
    public override void _Draw()
    {
        if (!IsEmitting) return;
        float time = _actor.ActionTime - _actor.Spec.Prepare;
        // 刀光整个可见寿命的 0～1 进度，包括生效窗口和短暂残光。
        float progress = Mathf.Clamp(time / (_actor.Spec.Active + .15f), 0, 1);
        // 亮度包络：快速变亮后衰减，寿命两端回到透明。
        float fade = Mathf.Sin(Mathf.Pi * Mathf.Pow(progress, .55f));
        // 施法中心方向角，单位为弧度，不读取攻击后的新朝向。
        float center = _actor.CastDirection.Angle();
        // 攻击扇形半角，决定刀光扫描的左右边界。
        float half = _actor.Spec.HalfAngle;
        // 亮刃前端的当前角度；提前完成扫动，剩余寿命用于消散。
        float head = center - half + 2 * half * Mathf.Min(1, progress * 1.6f);
        // 拖尾起始角，限制在攻击扇形内；横扫比普通攻击保留更长的弧。
        float tail = Mathf.Max(center - half, head - (_actor.CurrentAction == CombatAction.Sweep ? 2.2f : 1.2f));
        // 刀光外缘半径，沿用动作的世界像素范围，避免画面与判定明显脱节。
        float radius = _actor.Spec.Range;
        // 阵营基础色：敌方橙红、玩家暖金，亮刃另用接近白色的高光。
        Color tint = _actor.IsEnemy ? new Color(PresentationColors.EnemyTrail) : new Color(PresentationColors.PlayerTrail);
        if (_actor.CurrentAction == CombatAction.Dash)
        {
            Vector2 tip = _actor.CastDirection * radius;
            DrawLine(-_actor.CastDirection * 25, tip, new Color(tint, fade * .25f), 12, true);
            DrawLine(Vector2.Zero, tip, new Color(1, .96f, .81f, fade), 2, true);
        }
        else
        {
            Ribbon(tail, head, radius, 14, new Color(tint, fade * .10f));
            Ribbon(tail, head, radius, 8, new Color(tint, fade * .45f));
            DrawArc(Vector2.Zero, radius, tail, head, 40, new Color(tint, fade * .65f), 4, true);
            DrawArc(Vector2.Zero, radius, tail + .08f, head, 40, new Color(1, .97f, .82f, fade), 1.4f, true);
            DrawArc(Vector2.Zero, radius - 12, tail + .15f, head, 32, new Color(tint, fade * .3f), 1, true);
        }
        // 固定少量火星，不每帧创建粒子节点；暂停与打断自动跟随施法时钟。
        for (int i = 0; i < 7; i++)
        {
            float angle = head - i * .16f;
            Vector2 direction = Vector2.FromAngle(angle);
            Vector2 point = direction * (radius + progress * (i % 3) * 4);
            DrawLine(point, point + direction * (2 + i % 4), new Color(tint, fade * .6f), 1, true);
        }
    }
    /// <summary>绘制两端收尖的弧形带状网格。</summary>
    /// <param name="start">弧线起始角，弧度。</param>
    /// <param name="end">弧线结束角，弧度。</param>
    /// <param name="radius">外缘半径，世界像素。</param>
    /// <param name="width">弧带中段最大宽度，世界像素。</param>
    /// <param name="color">含透明度的颜色，参与加色混合。</param>
    private void Ribbon(float start, float end, float radius, float width, Color color)
    {
        // 沿弧线的细分段数；内外边缘反向连接成闭合轮廓。
        const int count = 32;
        var points = new Vector2[(count + 1) * 2];
        for (int i = 0; i <= count; i++)
        {
            float fraction = i / (float)count;
            Vector2 direction = Vector2.FromAngle(Mathf.Lerp(start, end, fraction));
            points[i] = direction * radius;
            points[points.Length - 1 - i] = direction * (radius - width * Mathf.Sin(fraction * Mathf.Pi));
        }
        if (end - start > .01f) DrawColoredPolygon(points, color);
    }
}
