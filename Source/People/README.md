# S1 武将 PNG 原型

2026-10-09：身体仍使用 `LvBu/lubu-parts-v3.png`；长戟与后弯双翎改用 `LvBu/lubu-reference-accessories-v4.png`（1774×887 RGBA，实际传入三姿态原图生成）。站立双手低持、行走腰侧拖戟、攻击后收再绕身劈扫，由六份姿态资源驱动同一骨架。小腿和靴掌在运行时分区采样。完整记录见 [长戟握持与攻击重做](../../Demand/Prompt/2026.10.09-吕布长戟握持与攻击重做.md)，下方为历史状态。

2026-10-08：吕布已更换为 `LvBu/lubu-parts-v3.png`（1254×1254 RGBA），使用固定腿长与收窄髋点的比例配置。`LvBu/lubu-pose-study-v3.png` 为站立、前行、蓄势三姿态制作样板，不是动画帧图。魏延保持 v2；吕布 v2 与 prototype 保留。详情见 [武侠姿态参考与素材重建](../../Demand/Prompt/2026.10.08-武侠姿态参考与吕布素材重建.md)。

2026-10-02 更新：吕布和魏延的 `*-parts-v2.png` 已通过内置生图生成并保存，均为 1254×1254 RGBA。两份 `*-presentation.tres` 已配置 PartsAtlas，默认使用 16 关节拆件动画；通过 AtlasRegions 校正部件采样，通过 CrestRect 调整冠饰。原图与 `LegacyPortraitRig` 保留作回退。成功提示词、历史失败记录和接入边界见 [拆件生成记录](拆件生成记录.md)。

日期：2026-09-28。生成方式：内置 GPT 图像生成工具（非 CLI），两张独立生成，无输入参考图。保留原始透明 alpha，未抠图、未二次压缩。

## 本轮用户要求

> 根据我的学习路线，帮我完成第一个场景，我希望是看起来像是真实的武将，可以先用一个png代替，你帮我生成一个魏延的武将图，然后把我的图从logo图换成吕布的图片

## 资产与使用

2026-09-30 更新：原图保持不变，显示缩放从 0.074 调整为 0.05（原尺寸约 68%）；运行时由 `CharacterRig` 的八根二维骨骼驱动网格，原 Sprite 隐藏，仅作为资源入口和编辑器预览。支持小幅呼吸、迈步与挥击，尚未拆分身体/武器图层；F4 可查看骨骼。以下原始接入说明记录的是 2026-09-28 基线，当前实现以[最新需求记录](../../Demand/Prompt/2026.9.30-角色尺寸骨骼刀光与HD2D.md)为准。

| 文件 | 角色 | 规格 | 场景 |
| --- | --- | --- | --- |
| LvBu/lubu-prototype.png | 吕布，红黑甲、双翎、方天画戟 | 1024×1536，RGBA PNG | Components/Characters/LvBu.tscn |
| WeiYan/weiyan-prototype.png | 魏延，绿黑甲、长柄刀 | 1024×1536，RGBA PNG | Components/Characters/WeiYan.tscn |

两张图均按用户要求作为可玩原型直接接入，不代表已完成正式四向动画或像素规范验收。写实人体比例是本次明确需求，优先于 S0 的 3.5 头身像素候选约束；本轮不全局改写其他素材风格。角色根节点在脚底，Sprite 显示缩放 0.074，脚底碰撞半径 12、Hurtbox 半径 14；不使用整张立绘当碰撞体。朝向通过脚底箭头表达，临时水平镜像与轻微起伏表达移动；武器左右一致性留待正式方向图解决。

验收：透明通道存在且角像素 alpha=0；Godot 实际截图中边缘无矩形背景。应用图像只更换玩家角色 Sprite，工作室 BaseSource/Logo.png 保留原文件。

## 最终生成提示词

### 魏延

Use case: historical-scene. Asset type: transparent PNG single-character cutout for a 2D top-down action RPG prototype. Create Wei Yan (魏延), a fierce adult Chinese male general of the Three Kingdoms, original historically inspired design, realistic human proportions, weathered face, short black beard, tied hair beneath a practical iron helmet, dark steel lamellar armor with muted forest-green cloth and aged bronze fittings, both hands holding a long single-edged dao polearm in a compact combat-ready stance. Full body including both boots and entire weapon visible. Three-quarter view facing screen-left, slightly elevated camera about 20 degrees, natural realistic painted game rendering with clean readable silhouette and clear material textures, not chibi, not pixel art. One centered character only, vertical 1024x1536 composition, generous transparent margins around helmet weapon and feet. Soft upper-left daylight. Genuinely transparent alpha background, no background scene, no ground plane, no shadow baked behind character, no text, no watermark, no border, no duplicate views. Boots end near 90 percent canvas height. Make character convincing as a real warrior, no oversized fantasy armor, do not copy existing franchise designs.

### 吕布

Use case: historical-scene. Asset type: transparent PNG single-character cutout for a 2D top-down action RPG prototype. Create Lü Bu (吕布), a powerful adult Chinese male general of the Three Kingdoms, original historically inspired design, realistic human proportions, strong stern face, dark hair, distinctive crown with two swept pheasant plumes, dark charcoal lamellar armor with restrained antique-gold fittings and deep crimson cloth and short cape, holding one fangtian ji halberd in a compact combat-ready stance. Full body including both boots and entire halberd visible. Three-quarter view facing screen-right, slightly elevated camera about 20 degrees, natural realistic painted game rendering with clean readable silhouette and clear material textures, not chibi, not pixel art. One centered character only, vertical 1024x1536 composition, generous transparent margins around plumes weapon and feet. Soft upper-left daylight. Genuinely transparent alpha background, no background scene, no ground plane, no shadow baked behind character, no text, no watermark, no border, no duplicate views. Boots end near 90 percent canvas height. Make character convincing as a real warrior, no oversized fantasy armor, do not copy existing franchise designs.
