# 技能与物品图标

图标按用途分为 `Skills` 和 `Items`，文件名使用大驼峰并保留版本后缀。当前图采用深炭棕背景、铁铜与木漆质感、大块明暗的厚重器物风格，靠主体轮廓识别；没有文字、数字和预制界面框，快捷键、冷却及库存由界面叠加。

## 当前资产

| 文件 | 用途与接入 | 原始生成文件 |
| --- | --- | --- |
| `Skills/FormationBreakV1.png` | 破阵突进，绑定 `Resources/Config/Skills/lubu-dash.tres`。 | `exec-4d65c9e4-dc5d-4867-8c07-d979a8b1abf4.png` |
| `Skills/HalberdSweepV1.png` | 方天横扫，绑定 `Resources/Config/Skills/lubu-sweep.tres`。 | `exec-6d883431-0d22-4a57-9268-38e7c6bef350.png` |
| `Skills/CrimsonFuryV1.png` | 赤焰爆气，绑定 `Resources/Config/Skills/lubu-fury.tres`。 | `exec-e62daad2-ec1f-422b-8480-d47d462d6dd4.png` |
| `Items/TorchV1.png` | 火把，绑定 `Resources/Config/Items/torch.tres` 的 `Icon`；物品数量仍读取真实冒险存档。 | `exec-83ad40a1-22c1-49f8-98a1-e8382e2fedda.png` |
| `Items/WineGourdV1.png` | 酒葫芦候选图，尚未建立物品定义、效果或库存，不占快捷物品槽。底栏葫芦形蓝量控件由界面单独绘制。 | `exec-3a13c2a7-5106-4987-9436-a8c69d1c2250.png` |

上述素材均为此前内置 imagegen 生成的单张图，本次物品接入直接复用已有原图，没有再次生成。原始目录为 `C:/Users/linjinxiang/.codex/generated_images/01a15e59-70a1-7be1-a288-79d1208ffaa6`。项目文件完整复制原图，未裁剪、拼接、重绘；生成缓存中的原图保留。

五张原图均为 **1254×1254、8 位 RGB、无 Alpha**，不是透明剪影或图集。界面按整图缩放到方形槽位，主体居中，无格序或额外锚点；实际槽位尺寸由 `PlayerStatusBar` 统一管理。图标器物和技能气焰为游戏化表达，不作为历史形制依据。

## 维护与验证

- 技能纹理由 `CombatActionConfig.Icon` 配置；物品纹理由 `AdventureItemDefinition.Icon` 配置。修改纹理不改变技能数值、物品效果或存档库存。
- 空槽不伪造物品数量；候选素材在建立正式物品定义及玩法前保持未接入。
- 本次物品原始完整提示词没有随此前生成结果保存在项目中，不补写或伪造原始提示词。技能既有记录及赤焰爆气提示词见 [技能图标接入记录](../../../Demand/Prompt/2026.10.22-技能图标接入.md)。
- 已核对火把及酒葫芦原图内容、尺寸与颜色格式。实际快捷栏显示、冷却与库存验证随本轮底栏整合统一执行，不以本文件的资产清单代替游戏内验收。
