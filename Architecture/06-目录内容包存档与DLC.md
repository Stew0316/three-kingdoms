# 目录、内容包、存档与 DLC

状态：提案  
目标：区分代码、组件、正式内容和原始资产，并为存档兼容和后续内容包预留稳定边界。

## 1. 顶层目录职责

保留项目当前一级目录，并建议后续按需要增加 `Content`、`Tests` 和 `Tools`。真正新增一级目录时，需要同步更新根目录 `AGENTS.md`。

```text
addons/        强通用、可跨项目复用的 Godot 插件
BaseSource/    工作室 Logo、许可等跨游戏基础资源
Architecture/ 长期架构设计与细化记录
Content/       正式运行时 Definition 与内容包
Scripts/       C# 系统和游戏代码
Components/    可复用的 Godot 组件场景
Scene/         完整流程、地图、遭遇和菜单场景
Source/        本游戏美术、音频、Shader、风格样板
UI/            UI 界面内容；实际建立时进一步约定代码与资源边界
Tests/         规则、内容、集成和存档迁移测试
Tools/         内容校验、沙盒、地图检查和构建工具
Demand/        原始需求、设计想法、阶段计划和验收记录
Build/         导出产物或本地构建结果
```

`addons` 不应成为所有工具代码的默认位置。只有真正跨项目、依赖边界清楚的能力才提取进去；本游戏专属的技能编辑或地图校验工具应先放在 `Tools` 或项目脚本中。

## 2. 推荐目录细分

```text
Content/
├─ Core/
│  ├─ PackManifest.tres
│  ├─ Skills/
│  ├─ Modifiers/
│  ├─ Units/
│  ├─ Items/
│  ├─ Encounters/
│  ├─ Maps/
│  ├─ Quests/
│  ├─ Dialogues/
│  └─ Cues/
├─ Chapter_Hulao/
└─ DLC_*/

Scripts/
├─ Core/
│  ├─ Ids/
│  ├─ Time/
│  ├─ Events/
│  └─ Random/
├─ Application/
├─ Content/
├─ Actors/
├─ Commands/
├─ Abilities/
├─ Modifiers/
├─ Combat/
├─ Projectiles/
├─ AI/
├─ Encounters/
├─ World/
├─ Interaction/
├─ Narrative/
├─ Progression/
├─ Inventory/
├─ Save/
├─ Presentation/
│  ├─ Vfx/
│  ├─ Audio/
│  └─ Camera/
├─ UI/
├─ Platform/
└─ Debug/

Components/
├─ Actors/
├─ Combat/
├─ Abilities/
├─ World/
├─ Interaction/
├─ UI/
└─ Vfx/

Scene/
├─ Boot/
├─ Menus/
├─ World/
├─ Encounters/
├─ Sandboxes/
└─ Cinematics/

Source/
├─ People/
├─ City/
├─ Terrain/
├─ Vfx/
├─ UI/
├─ Audio/
├─ Shaders/
└─ StyleGuide/

Tests/
├─ Unit/
├─ Combat/
├─ ContentValidation/
├─ Integration/
└─ SaveMigration/

Tools/
├─ ContentValidator/
├─ SkillSandbox/
├─ MapValidator/
└─ BuildPipeline/
```

## 3. 按技术层组织与按内容包组织

两种组织方式不要互相冲突：

- `Scripts/` 按系统职责组织，保存可复用规则代码。
- `Content/` 按内容包和内容类型组织，保存正式游戏内容。

例如龙卷风：

```text
Scripts/Abilities/Effects/SpawnAreaEffect.cs
Scripts/Combat/AreaEffects/MovingAreaEffect.cs
Content/Core/Skills/Tornado.tres
Content/Core/Cues/TornadoCast.tres
Components/Vfx/Tornado/TornadoLoop.tscn
Source/Vfx/Tornado/*.png
```

通用移动区域属于系统；龙卷风的具体参数和表现属于内容。

## 4. 稳定 ID 与命名空间

所有需要存档、引用、DLC 扩展或跨场景访问的内容必须有稳定 ID：

```text
core:skill/tornado
core:modifier/stunned
core:unit/weiyan
core:map/start_village
core:encounter/weiyan_trial
chapter_hulao:map/hulao_gate
dlc_redcliff:skill/fire_ship
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
if (DlcRedCliffEnabled) { ... }
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
skill.tornado.name
skill.tornado.description
quest.weiyan_trial.title
map.start_village.name
```

不在 Definition 中把简中名称作为唯一内容标识。技能描述中的数值优先使用占位符和格式化数据，减少强化后文本与实际数值不一致。

## 11. 目录迁移原则

现有项目已有代码和资源，不能为了得到理想目录一次性大搬迁：

1. 新模块优先进入目标目录。
2. 修改旧模块时再逐步迁移。
3. 每次移动后验证 Godot UID、场景引用和 C# 构建。
4. 不因为目录整理中断当前阶段可玩版本。
5. 一级目录实际改变时更新 `AGENTS.md`。

