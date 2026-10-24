# 三国游历

基于 Godot 4.7.1 C# 的二维俯视游历与即时战斗游戏。当前可玩内容包括槐安城、四人演武场和旧驿道洞窟。

## 阅读入口

| 要了解的内容 | 文档 |
| --- | --- |
| 协作规则与一级目录 | [AGENTS.md](AGENTS.md) |
| 游戏故事与章节编写 | [剧本](Drama/README.md) |
| 全局人物、移速与地图菜单 | [导航架构](Architecture/14-全局人物数据与地图菜单.md) |
| 三国美术与自然动作标准 | [城池与地形风格指南](Source/StyleGuide/城池与地形风格指南.md) |
| 系统能力与未实现边界 | [系统实现索引](Architecture/当前实现索引.md) |
| 当前可玩内容、配置与验证入口 | [内容实现索引](Demand/Content/ImplementationIndex.md) |
| 八类技能树、自由组合与成长设计 | [技能设计](Architecture/SkillDesign.md) |
| 架构与游戏内容如何分开记录 | [文档边界](Architecture/DocumentationRules.md) |
| 代码职责、静态定义、配置与公共部分 | [目录职责与公共代码组织](Architecture/12-目录职责与公共代码组织.md) |
| 架构专题与长期方案 | [架构索引](Architecture/README.md) |
| 当前素材与历史生成记录 | [美术资源索引](Source/README.md) |
| 阶段计划与验收文档 | [阶段索引](Demand/Stage/README.md) |
| 需求、设计与制作记录 | [Demand 索引](Demand/README.md) |
| 本轮整理与结构检查 | [技能体系与架构内容分离](Demand/Prompt/2026.10.24-技能体系与架构内容分离.md) |

## 运行与验证

使用 Godot .NET 编辑器打开 `project.godot` 并构建 C# 项目。正常运行由 `GameEntry` 显示黑底致谢与猫头鹰启动页，加载后进入槐安城；东门可步行沿草路前往雪山麓。引擎命令行自定义参数 `--arena` 进入演武场，`--cave` 预览洞窟。

构建可使用 `dotnet build Three-Kingdoms.csproj`。各专项回归的入口和用途见 [当前实现索引](Demand/Content/ImplementationIndex.md#验证入口)；截图、运行日志放在 `Build`。测试命令存在不等于本次已经执行，具体执行环境、结果和遗留项记在对应需求文档。

## 文档分工

- `AGENTS.md` 保持简短，只记录长期规则和一级目录职责。
- `Architecture` 只保存通用系统、技能体系、依赖边界与运行机制；不维护具体人物、地图、任务和数值内容。
- `Demand/Content` 保存具体内容与实现索引，`Demand/Design` 保存具体设想，`Demand/Prompt` 保存用户问题与当次决定，`Demand/Stage` 保存阶段计划，`Demand/美术` 保存生成与接入记录。
- `Source/StyleGuide` 定义当前视觉标准；资产索引标出实际使用版本，历史素材保留追溯。

历史文档按当时背景阅读，后续明确需求优先。文档更新不代表已有资源完成重绘，也不代表原有验收自动覆盖新改动。
