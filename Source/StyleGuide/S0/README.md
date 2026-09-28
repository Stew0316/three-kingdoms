# S0 风格草样

版本：`TK-ART-v0.1-candidate`。这三张图由内置 GPT 图像生成工具生成，用于确定人物、地面和刀光是否属于同一视觉方向。它们是候选参考，不是可直接导入战斗的最终像素素材。

| 文件 | 用途 | 技术状态 |
| --- | --- | --- |
| `lubu-character-candidate.png` | 吕布身份、颜色、甲胄层次与长兵器轮廓 | 透明背景源稿；需按 64×64、44–48px 可见身高重新像素化与修帧 |
| `central-plains-ground-candidate.png` | 中原草地、土路、低对比环境配色 | 非透明概念块；未经 32×32 无缝拼接验证，不作为 TileSet |
| `halberd-slash-candidate.png` | 金白刃口、暗红尾迹和横扫方向 | 透明背景源稿；需按 128×128 重绘、压缩辉光并匹配真实命中范围 |

## 本次生成提示词

生成模式：内置 GPT 图像生成器，三张图均为新生成，无输入参考图。

**人物候选**

> Create an original stylized 2D game character sprite reference for Lü Bu in a late-Han fantasy setting. Three-quarter top-down action RPG view, about 3.5 heads tall, strong readable silhouette, dark red and charcoal lamellar armor with restrained antique-gold accents, black tied hair, and a long ji halberd fully visible. Use crisp clustered shapes, limited shading, and the palette from the project art direction. Transparent background, centered single character, no text, no scenery. Do not copy the design of any commercial game, anime, or film depiction. This is concept source art for a later hand-redrawn 64×64 sprite, so prioritize silhouette and color hierarchy over tiny ornament.

**地面候选**

> Create an original square 2D game environment concept tile showing muted Central Plains grass crossed by a soft diagonal packed-earth path. Three-quarter top-down RPG view, restrained earth green and wood brown palette, low contrast so characters remain the focus, no buildings, props, characters, UI, text, or landmarks. Use simplified pixel-art-inspired color clusters and avoid photorealistic texture. The result is a manual redraw reference for a 32×32 seamless terrain set; keep edges visually calm, but do not add a tile grid.

**刀光候选**

> Create one original 2D halberd slash effect on a transparent background: a decisive crescent sweep traveling from left to right, bone-white and pale-gold cutting edge, restrained dark-red trailing fragments, crisp readable direction, large transparent margins. No character, weapon, scenery, magic circle, text, UI, smoke cloud, or blue neon. Pixel-art-inspired clustered shapes with limited glow. This is concept source art for a later hand-redrawn 128×128 combat effect, so the bright edge must remain readable over dark, light, and grass backgrounds.

完整风格前缀、反向约束和后续生成模板见 `Demand/Stage/风格-生图提示词模板.md`。生成器无法保证精确帧尺寸、无缝边缘和像素簇一致，因此所有候选图进入正式资源前都必须人工验收和重绘。

## S0 结论

- 保留配色方向：沉静土绿/木褐环境，吕布暗红与金色为焦点，刀光只在短时使用骨白高亮。
- 保留人物强轮廓、环境弱对比、刀光方向明确的层级。
- 当前人物细节过密，直接缩到 64×64 会丢失甲片和脸部；正式 sprite 必须重新简化，不能直接缩放。
- 当前地面不保证无缝，正式地块必须按 32×32 邻接关系制作并做四边拼接测试。
- 当前刀光辉光面积偏大，正式版需缩短残光、保留透明留白，并在黑/白/草地三种背景检查。

批准状态：`candidate`。S0 只冻结视觉方向和逻辑尺寸；S4 完成真正可导入的 `R-CHR-001`、`R-ENV-001`、`R-FX-001` 后再升级到 approved。
