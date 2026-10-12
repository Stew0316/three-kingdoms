## 基础说明

- 这是一个2d游戏，主要是做对战，2d的冒险游戏，题材是三国时期，主线是游历，敌人类型主要为人形怪，BOSS和小兵都是。

## 输出

- 所有的输出都必须是中文

## 技术栈

- godot 4.7.1 c#版本

## 身份

- 我是一个前端+略懂后端的程序员，回答我问题的时候，可以不用太小白，多一点技术视角
- 讲解技术细节的时候可以尽量细一点

## 文件结构

- addons
  - 通用的插件放入其中，可以给其他类型或者其他游戏引用的内容
- BaseSource
  - 基础资源，例如是本工作室的logo等这样非常通用资源，不只是在本游戏里使用的内容
  - logo-owl-l-v1.png 为参考 logo-l.png 熊版风格生成的猫头鹰 STEW Logo 候选；logo-owl-light-v2.png 和 logo-owl-dark-v2.png 为配套亮色与暗色背景版本，原图保留；生成记录见 Demand/美术/猫头鹰Logo生成记录.md
- Source
  - People/GuanYu 使用 guanyu-parts-v1.png 与独立长柄 guanyu-glaive-v1.png；People/LiuBei 使用 liubei-parts-v1.png（包含两把完整雌雄剑），均为独立生成并绑定的角色资源，未覆盖旧武将。完整提示词及采样记录见 Demand/美术/2026.10.12-关羽刘备拆件生成记录.md
  - City是城池资源
  - People是人物资源；吕布身体使用 LvBu/lubu-parts-v3.png，长戟与后弯双翎使用 lubu-reference-accessories-v4.png；16 业务关节加独立靴掌图层，魏延保留 weiyan-parts-v2.png；lubu-pose-study-v3.png 为造型比例参考，当前持戟动作以 2026-10-09 的低位警戒、后收戟与绕身劈扫为准；旧图保留回退，生成与接入记录见 People/拆件生成记录.md
  - Terrain是地形资源
  - StyleGuide是美术风格样板和生成记录；候选图不等于正式游戏资产
- Resources
  - 四人现有独立 model/animation/ui/presentation/loadout；默认 K、L 两个主动和一个被动，普攻及空格基础闪避另计。吕布位移/横扫/反伤，魏延易伤/自耗强化/反击，关羽眩晕/范围减速/闪避暴击，刘备多段连击/治疗震击/吸血。旧吕布魏延暴击资源保留但不默认装备
  - 关羽动作使用沉稳长步、提刀重劈与震地；刘备轻步、双剑分持、交错刺斩。DualWeapons 属于模型，OffhandWeaponRegion/Size/BladeDistance 绑定副剑，姿态 FreeHand/OffhandWeaponDegrees 控制其握点和角度，刀光与残影包含两把剑；技能交换仍保留自身武器和动作
  - Config保存 Godot 可在 Inspector 编辑的 `.tres`；Combat 下每位武将独立保存基础属性、`*-model` 模型绑定、`*-animation` 动作映射、`*-ui` 界面资料、`*-loadout` 默认装备，`*-presentation` 仅组合模型/动作和角色装饰效果；类型在 `Scripts/Config`
  - Config/Skills 保存独立主动/被动技能定义，技能不引用武将模型；实例的 SkillLoadoutRuntime 持有可交换的装备与按技能ID独立冷却。输入槽 CombatAction 与动作语义 CombatMotion 分离，换技能后数值/名称跟随技能，模型/动作/UI身份保持武将自身配置；禁止修改共享资源或通过卸装清冷却，详见 Architecture/09-武将模型动作UI与技能组合.md
  - 吕布独立动作资源描述低持、拖戟和绕身劈扫，承重膝约24°；魏延独立动作资源描述双手斜持、收刀短步与斜劈压斩，承重膝约27°；骨架求解器按资源采样，不再含 HalberdSweep/GuardedGlaive 身份风格分支。双人共用固定骨长、握点和靴掌求解基础代码
  - 魏延 v2 头图保留完整原生盔缨，weiyan-model.tres 以独立头部尺寸绑定，并关闭重复冠饰 CrestVisible；远靴单独翻转朝前。吕布 v4 双翎独立配件保持原挂点与可见性。原PNG不改写
  - 魏延头部现围绕颈部连接点缩小10%，PartRects[2] 为 Rect2(-9.45,-15.8,14.4,19.8)。吕布模型用独立 HeadTexture/HeadRegion 接入 Source/People/LvBu/lubu-head-painted-v1.png，以魏延写实彩绘脸部为参考，保留无须、金冠与长发；头部显示尺寸仍11×14，v3身体和v4配件保留。详见 Demand/美术/2026.10.11-武将头部比例与吕布脸部画风.md
  - 魏延武器使用模型独立的 WeaponAtlasClipPolygon 凸多边形采样边界，沿刀尖与衣摆间隙剔除邻格残片；保留原裁剪包围盒、武器尺寸和握点，遮罩不进入技能配置。--weiyan-pose-test 包含实际渲染像素检查
  - 两人分别用 ArmAtlasAnchors 标定肩肘腕、拳掌独立采样，保持中立腕。当前双臂弯曲支路均为-1，需结合前伸握点和下沉肘部判断观感，不能仅翻转符号；吕布站立70°/握距32/主握至刃尖70，魏延站立58°、移动64°/握距30/主握至刃尖76。双手均握在柄部；各自动作配置以 PrepareVia 控制提械路线。吕布 SupportHandAttachStart=0.9，单手拖戟转回身前后再接上前手；魏延默认0、持续双手持刀。两套模型和动作均独立，技能交换不改变握法。详见 Demand/Prompt/2026.10.11-参考武打双手持械修正.md
- Scripts
  - CombatStatusRuntime 保存每单位独占的眩晕、减速、易伤、强化计时；Hitbox 按目标与命中段去重。多段/自疗/消耗/强化来自技能快照，换装不清状态。当前为近战技能子集，非完整 Modifier 叠层系统
  - c#脚本代码放入其中
  - Utils
    - 工具类
  - Config
    - 配置项
  - Const
    - 定义的静态值
- Scene
  - 演武场右上方“选武将”可选我方/敌方四名武将，无需参数。HeroCatalog 在子节点初始化前装配角色资源，R 重开保留本进程的选择；应用重新启动仍为吕布对魏延。选人会暂停，返回恢复原暂停状态
  - --four-hero-test 验证四人独立动作、八向移动、双剑、技能效果与实际选将按钮重载，95项检查；图形模式截图在 Build/four-heroes。需求与技能表见 Demand/Prompt/2026.10.12-关羽刘备与四武将独立动作.md
  - 场景
  - 正式主路线采用火红/叶绿式二维俯视 TileMap 与即时战斗；Arena.tscn 是当前二维俯视演武场和启动入口，HD2DArena.tscn 仅保留为历史表现技术样板，正式世界地图入口后续独立建立
  - Arena.tscn 当前默认 SoloPractice=false，正常启动直接开启魏延实战，无需参数，魏延显示、AI、碰撞与受击全部启用；仅手动开启 SoloPractice 或运行吕布专用 --walk-test / --pose-test 时隔离对手；战斗/可见性/魏延姿态测试保持对战模式
  - --weiyan-pose-test 显式恢复魏延，检查八方向步态、双手持刀、普攻/技能/闪避/反击及暂停受击；有图形渲染器时在 Build/weiyan-frames 输出连续帧
  - --module-test 验证双方主动/被动技能互换、跨输入槽装备、独立动画/伤害/冷却、共享资源隔离、盔缨与HUD刷新；正常启动仍直接实战，不需要参数
- Demand
  - 一些需求，一些想法文档
- Components
  - 一些复用组件
- addons
  - 一些插件，可能会与其他项目一起公用的插件
  - 通用性非常强的放入其中，类似json读写存档这种的工具方法
- BaseSource
  - 基础资源，不会放很多东西，放一些我的logo之类的
- 待加入模块
  - steam sdk引入
  - 网络通信
  - 本地存档 / 云存档
  - 多语言切换
    - 默认简中
    - 繁中
    - 日语
    - 英文
- Architecture
  - Architecture/下都是架构设计，会有我的输入，还有ai的输入，涉及架构或者比较复杂的内容需要先阅读一下，如果架构有改动，需要更新Architecture/里的文档
- UI
  - UI界面内容

## 注释

- c#脚本尽量写注释，注释主要说明这个变量或者参数做什么的就行，如果有更改，注释也必须更改

## 问答

- 只要是和技术路线、架构、技能、地图或者是需求相关的，都把我的问题也记录下来，归类到根目录/Demand下

# 更新

- 如果资源第一级目录有被更新过，请更新本说明
