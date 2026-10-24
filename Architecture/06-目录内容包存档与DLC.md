# 目录、内容包、存档与 DLC

更新日期：2026-10-24。现有目录与本地存档已存在；下方通用注册、内容包、DLC 和完整迁移模型仍为提案。

## 1. 顶层目录的唯一约定

当前一级职责以根 [AGENTS](../AGENTS.md) 为准，实际代码组织见 [12 号文档](12-目录职责与公共代码组织.md)，文档归属见 [文档边界](DocumentationRules.md)。不再把旧提案中的根级 Content、UI、Tests、Tools 当作建设目标；没有实际需要不预建空目录。

| 内容 | 现有位置 |
| --- | --- |
| 通用系统说明与规则 | `Architecture` |
| 内容设计、制作过程与交付记录 | `Demand`；故事与对白另在 `Drama` |
| C# 规则、流程与表现 | `Scripts` 按领域划分 |
| Resource 类型 / 配置实例 | `Scripts/Config` / `Resources/Config` |
| 着色器运行资源 | `Resources/Shaders` |
| 组件 / 完整场景 | `Components` / `Scene` |
| 原图、音频等源素材 | `Source` |
| 当前测试 / 工具 | `Scripts/Tests` / `Scripts/Tools` |
| 构建、截图与临时检查输出 | `Build` |
| 真正跨游戏基础能力 | `addons` / `BaseSource` 子模块 |

## 2. 后续内容包落点

若确实需要打包和独立启停，优先在 `Resources` 中增加有明确边界的内容包目录，或由 Manifest 引用现有分类资源。采用哪一种在内容规模出现后决定；本次不迁移 `.tres`、场景或源资产。

内容包负责定义集合、依赖和授权边界，不要求把规则代码复制进每个包。跨包引用通过稳定 ID 和注册层管理，场景与素材遵循现有资源目录和 UID 规则。

## 3. 系统与内容的对应

通用持续区域、伤害和状态处理属于 Scripts；某个技能的伤害、寿命、贴图和配置实例属于 Resources/Source；其设计与获取方式属于 Demand。架构可以说明字段和接口，不在此维护正式技能数值或章节清单。

## 4. 稳定 ID 与命名空间

所有需要存档、引用、DLC 扩展或跨场景访问的内容必须有稳定 ID：

```text
core:skill/example
core:modifier/stunned
core:unit/example
core:map/example
core:encounter/example
chapter_pack:map/example
dlc_pack:skill/example
```

规则：

- ID 使用稳定英文和命名空间。
- 显示名称使用本地化 Key，可随时修改。
- 文件名可以调整，但移动资源后要保证 ID 不变。
- 存档不保存中文名作为主键。
- 不复用已经发布并进入存档的旧 ID 表示另一种内容。
- 删除内容时保留迁移或废弃映射。

## 5. Content Pack

每个内容包拥有 Manifest：

```text
PackId
Version
GameVersionRange
Dependencies
LoadOrder
Definitions
Scenes
Localization
SaveCompatibilityVersion
ContentHash
```

加载流程：

1. 发现内置和外部内容包。
2. 读取 Manifest。
3. 校验游戏版本和依赖。
4. 确定加载顺序。
5. 注册 Definition。
6. 检查重复 ID 和缺失引用。
7. 构建只读 ContentRegistry。
8. 进入主菜单或阻止加载并报告错误。

首发版本即使只有 `core` 包，也建议内容 ID 按包设计，这比发售后迁移所有 ID 成本低。

## 6. 存档模型

### 6.1 SaveRoot

建议结构：

```text
SaveVersion
GameVersion
EnabledContentPacks
ProfileState
PlayerState
WorldState
QuestState
InventoryState
SettingsReference
PlayTime
SaveTimestamp
```

### 6.2 PlayerState

- 当前 MapId 和 EntranceId。
- 必要的位置补充信息。
- 生命、资源和基础成长。
- 已学技能 ID、等级和强化。
- 当前技能槽配置。
- 装备和货币。
- 声望等长期属性。

### 6.3 WorldState

- 已发现地图。
- 已激活传送点。
- 每张地图的 Persistent Object 状态。
- 一次性遭遇完成状态。
- 全局剧情标记。
- 世界阶段或章节。

### 6.4 不允许保存

- Godot Node 或 Resource 的运行时引用。
- Delegate 和 Signal 连接。
- 当前 SkillCast。
- Hitbox 重叠集合。
- 运行中的异步 Task。
- 短生命周期 VFX。
- 仅靠 NodePath 才能解释的数据。

## 7. 存档版本迁移

迁移按版本逐级执行：

```text
v1 → v2 → v3 → 当前版本
```

不要假定旧版本可以直接跳到最新结构。每次破坏性变更需要记录：

- 字段重命名。
- 技能或物品 ID 替换。
- 地图入口变化。
- 任务结构变化。
- 内容包拆分。
- 默认值补全。

写入存档时建议先写临时文件，校验后替换正式文件，并保留至少一个上一版本备份，降低断电或崩溃导致损坏的风险。

## 8. DLC 边界

DLC 优先作为内容包提供：

- 新地图和场景。
- 新敌人和 Boss。
- 新技能、Modifier 和物品。
- 新任务、对话和本地化。
- 新 VFX、音乐和音效。

核心代码不应出现大量：

```text
if (ExampleDlcEnabled) { ... }
```

应改为：

- 内容包被平台授权并加载。
- ContentRegistry 出现对应 Region、Quest 或 Skill。
- 游戏流程根据内容定义和前置条件展示入口。

Steam 所有权检查属于 Platform 层；具体 DLC 内容属于 Content 层。

## 9. Godot PCK 使用原则

Godot 可以运行时加载 PCK/ZIP，用于补丁、DLC 和 Mod。需要注意：

- 加载顺序会影响路径覆盖。
- 同路径资源可能覆盖已有资源。
- C# 代码包需要额外处理程序集。
- 不可信第三方代码具有安全风险。
- 内容包应尽可能隔离路径和命名空间。
- DLC 应尽早加载，避免基础场景已经预加载旧资源。

首期策略：

1. 官方 DLC 可以使用经过构建和签名流程的 PCK。
2. 首期不开放任意第三方 C# DLL。
3. 若开放 Mod，优先开放受限数据、贴图、音效和经过验证的 Definition。
4. 独立 Mod 工具与正式游戏进程分开。

参考：[Godot PCK、补丁与 Mod 文档](https://docs.godotengine.org/en/latest/tutorials/export/exporting_pcks.html)。

## 10. 多语言边界

Definition 中保存文本 Key：

```text
skill.example.name
skill.example.description
quest.example.title
map.example.name
```

不在 Definition 中把简中名称作为唯一内容标识。技能描述中的数值优先使用占位符和格式化数据，减少强化后文本与实际数值不一致。

## 11. 目录迁移原则

现有项目已有代码和资源，不能为了得到理想目录一次性大搬迁：

1. 新模块优先进入目标目录。
2. 修改旧模块时再逐步迁移。
3. 每次移动后验证 Godot UID、场景引用和 C# 构建。
4. 不因为目录整理中断当前阶段可玩版本。
5. 一级目录实际改变时更新 `AGENTS.md`。
