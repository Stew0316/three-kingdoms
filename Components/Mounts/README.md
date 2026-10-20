# 马匹组件使用说明

完整入口是 [Horse.tscn](Horse.tscn)。它组合独立的动作、外观和骑乘连接，可在没有城市、玩家或战斗角色的场景中运行。骨架使用程序采样和连续蒙皮，动作仍是现有的 Muybridge 参考步态。

## 直接使用

把 [ChestnutHorse.tscn](ChestnutHorse.tscn) 或 [DappleHorse.tscn](DappleHorse.tscn) 拖进场景。默认站立；勾选根节点的 `AutoPlayPreview` 可以原地预览动作，`PreviewSpeed` 控制演示节奏，`PreviewFaceLeft` 控制朝向。这个速度不会移动马，也不会改变游戏里的探索速度。

打开 [HorseComponentDemo.tscn](../../Scene/Examples/HorseComponentDemo.tscn)，按 F6 运行：空格播放/暂停，R 站立，左右键切换朝向。上方放大，下方是游戏尺寸。

## 可分别复用的部分

| 入口 | 内容与接口 |
| --- | --- |
| [Horse.tscn](Horse.tscn) / `HorseActor` | 组合入口；`Appearance`、`MotionSource`、`RiderContacts`、`ResetGait()`、`SetPreviewPose()` |
| [HorseAnimation.tscn](HorseAnimation.tscn) / `HorseAnimationController` | 实际路程相位、起停权重、左右朝向、物理帧间插值；自身不读取输入或自动推进 |
| [HorseVisual.tscn](HorseVisual.tscn) / `HorseVisual` | 骨架实例、连续皮肤、鞍具、阴影、姿态缓存；`SetPose(phase, weight, facing)` |
| [HorseRig.tscn](HorseRig.tscn) / `HorsePaintedRig` | 32节点骨架、身体/四腿/尾/鞍具七张网格；`ApplyPose()`、`SaddleWorld`、`BridleWorld`、`HoofWorld()` |
| [HorseRiderAttachments.tscn](HorseRiderAttachments.tscn) | 缰绳、马镫；可绑定手脚标记或 `IHorseRiderContacts` |
| [CityHorse.tscn](CityHorse.tscn) / `CityHorse` | 现有 `Combatant` 的适配层，读取实际移动信息并绑定骑手；通用组件不依赖此层 |
| `HorseLocomotion` / `HorseBodyMotion` | 无状态的蹄迹、固定骨长关节求解、躯干/颈头/鞍点采样 |
| `HorseRiderPose` / `HorseShadow` | 无状态的骑手坐姿与阴影采样，可由其他表现器使用 |
| `HorseSkeletonDefinition` | 固定骨骼语义、索引和父链；不保存实例变换 |
| `HorseAppearanceCatalog` | 两种内置外观的只读寻址与生命周期；不保存动作状态 |

脚本主要位于 [Scripts/Presentation/Mounts](../../Scripts/Presentation/Mounts)，骨架定义和预设目录分别位于 `Scripts/Definitions/Animation`、`Scripts/Definitions/Catalogs`。

## 代码创建与固定姿态

```csharp
var horse = HorseActor.Create(HorseAppearanceCatalog.Dapple);
horse.Position = new Vector2(300, 200);
AddChild(horse);
horse.SetPreviewPose(.25f, 1f, -1f); // 相位0—1，权重0—1，朝左。
```

`SetPreviewPose` 在进入场景树、组件就绪后调用；绑定运动来源时由来源驱动。无来源的马保留场景摆放坐标。每匹马有独立的时钟、骨骼与网格，只读原图和材质可以共享。

## 接入真实移动

让宿主实现 `IHorseMotionSource`，返回 `HorseMotionSample`：

- `WorldPosition`：碰撞移动后的世界位置。
- `Distance`：碰撞后累计的实际路程；顶墙时不增长，不用按键速度积分替代。
- `Velocity`：实际移动速度，用于判断是否收回站姿。
- `Facing`：宿主朝向；纯上下移动保留上一左右朝向。
- `Enabled`：是否启用骑乘动作。

创建时先指定 `MotionSource`，再加入场景树；运行中更换来源后调用 `ResetGait()`。主动解除来源也调用该方法，马保留当前位置。宿主传送或累计路程归零会清除旧插值段。

默认宿主物理优先级为0；马以物理优先级10在宿主移动后取样，以绘制优先级−10先于骑手更新。自定义宿主的优先级也须满足这个顺序。只对马的表现位置插值，不写回宿主碰撞体；不要再对同一表现节点叠加引擎物理插值。现有首城进入时关闭 `PhysicsJitterFix`，退出恢复；其他宿主自行决定全局时间步设置。

动作相位与实际路程按128像素一轮对应。正式移动使用1倍比例；任意缩放用于展示，其他体型的真实接地需要同时校准步长和宿主速度。单独复用控制器时，物理帧调用 `CapturePhysics()`，绘制帧调用 `AdvanceMotion()`，再将显示相位、权重、朝向传给外观。

## 骑手与配件

完整组件已把 `RiderAttachments.Horse` 绑定到自身 `Visual`。手脚接触点可以通过 `RiderContacts` 提供，也可以在场景中给配件组件的 `ReinTarget` / `FootTarget` 赋值为 `Marker2D`；标记优先。端点读取最终画出来的手脚世界位置，绘制时读取，避免晚一帧。

拆开使用时，让外观与配件保持同一父节点、相同局部原点和比例，再设置 `Horse` 引用。无人骑乘时显示垂下的马镫，不画连接到空中手部的缰绳。现有武将通过 `CharacterRig.RidingMount` 显式绑定坐骑，不再按 `RidingHorse` 节点名查找。

`HorseRiderPose` 输出腰、胸、头、手脚目标及披风/冠翎角度。手脚目标使用当前武将骨架单位，其他骨架需做比例和关节映射；武将的固定骨长、IK和衣物绘制仍由其自身表现器处理。

## 外观与资产边界

外观类型为 [HorseAppearance.cs](../../Scripts/Config/World/HorseAppearance.cs)，两份配置位于 `Resources/Config/World/horse-chestnut.tres`、`horse-dapple.tres`，原图位于 `Source/Animals/Horses`。运行时换装应替换 `Appearance` 资源，外观组件会重建唯一骨架；不要原地改写共享预设。单独使用骨架时在加入场景树前提供有效外观，换装由上层 `HorseVisual` 负责。

两套内置外观由目录保留强引用，直接拖入预设也会登记，防止跨场景加载与托管资源终结竞争。自定义外观由持有者管理，不加入无限增长的全局缓存。

当前蒙皮和原图锚点对应 V2 四件布局；更换为其他布局时需同步标定 `HorseArtAtlas`，不能只替换图片。组件保留肩胯连接修复、七网格遮挡、Mipmap与显示插值；当前仍为左右侧身镜像，没有新增八向贴图或骑战。若迁移到另一个 Godot C# 项目，须一并迁移上述场景、脚本、外观资源、原图与导入设置；这些组件尚未包装为独立插件。

架构边界见 [马匹组件与动作复用](../../Architecture/13-马匹组件与动作复用.md)，本次检查见 [抽取与验收记录](../../Demand/Prompt/2026.10.20-马匹组件抽取与验收.md)。
