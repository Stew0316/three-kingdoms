# 魏延视频参考连斩与侧背面补件

日期：2026-10-27。接续 [魏延V3](WeiYanRigV3.md) 与 [手脚角度修正](WeiYanLimbCorrection.md)。用户要求：“帮我把魏延模仿大刀段落修改，尽量可以1：1的动作，素材该补就补，骨骼动作该修改也修改。”

最新修改：用户看过旋转版本后要求“把旋转的动作去掉，2d人物做旋转太奇怪了”。以下以取消旋转后的版本为当前状态，原视频只继续作为挥斩、腾空和收势节奏参考。

## 参考与动作

参考为用户提供的本地 MP4 `6b7f1e62c2f9dea0f42f3ada7289481f.mp4`，原片约16.07秒、1280×720、30FPS。主要参照约3.2～6.8秒的灰衣大刀人物：旋身横扫、腾空举刀、过顶挥斩、落地弓步、短暂停顿、第二轮回旋与腾空劈斩。抽帧留在 `Build/WeiYanSaberReference`；原视频保持原位置，没有复制整个微信目录或纳入游戏资源。

当前38个关键姿态组成4秒连斩：两次正向挥斩、腾空下劈、落地回架，再接第二轮。胸髋绕竖轴角和相对扭转始终为零，不再旋身、压窄身体或切换侧背面；仅保留少量前倾、沉胯和上下起伏。刀在画面内往复挥落，去掉连续整圈刀角。两脚各留在自身一侧，落地后锁定足底，收势时分脚抬步回到站位。自由手平衡身体，回到站姿后恢复之前确认的身侧垂手。

初版按视频可见姿态重建；当前版按用户最新意见重新编排旋转段落，保留魏延原有体型、绿色甲衣和长柄刀。它不是与演员逐关节1:1的动作，也没有使用原作品骨骼数据。原视频镜头的横向滚动不被当作人物位移。

## 游戏接入

- 第四主动槽为「大刀连斩」，默认键 **F**；完整4秒，6段命中。实战AI近身时也会按装备使用。资源路径与技能ID沿用原值，名称和说明同步去掉“旋身”。
- 普攻改用同一片段的腾空劈斩，前摇、生效、后摇分别映射源动作；普攻原有伤害、范围、冷却和总时长保持原配置。
- 新招数值以 [技能资源](../../Resources/Config/Skills/WeiYanSaberDance.tres) 为源：每段4伤害、六个独立窗口、7秒冷却、64范围；窗口空隙不造成伤害。角色既有强化、反击、突进、闪避和站走仍各用原语义。
- [完整动作片段](../../Resources/Config/Characters/weiyan/Animation/WeiYanSaberDanceClip.tres)、[片段映射](../../Resources/Config/Characters/weiyan/Animation/WeiYanSaberDanceAnimation.tres) 和 [模型采样](../../Resources/Config/Characters/weiyan/weiyan-model.tres) 分别管理动作、规则时间映射和素材。

通用机制见 [骨骼架构](../../Architecture/08-二维表现与角色骨骼架构.md)、[角色组合](../../Architecture/09-武将模型动作UI与技能组合.md) 与 [逐段命中](../../Architecture/03-技能状态与战斗架构.md)。空间坐标只用于美术投影与遮挡；游戏保持二维俯视移动与战斗，腾空表现不会自动赋予无敌。

## 新素材

[WeiYanTurnPartsV4.png](../../Source/People/WeiYan/WeiYanTurnPartsV4.png)：内置 imagegen 生成，输入为已确认风格的 `WeiYanPartsV3.png`。1254×1254、RGBA、Alpha范围0～255，原图完整保留，不抠图、不改像素。前两次请求连接失败，重试成功后复制到项目；没有切换外部API服务。

按行排列的格序：

| 行 | 第1格 | 第2格 | 第3格 | 第4格 |
| --- | --- | --- | --- | --- |
| 1 | 右侧头部 | 后脑盔甲 | 侧胸甲 | 背甲 |
| 2 | 侧腰裙 | 后腰裙 | 后上臂 | 后前臂 |
| 3 | 后大腿 | 后小腿 | 正面靴掌 | 背面靴掌 |
| 4 | 掌面握拳 | 拳背 | 长刀候选 | 背面披风 |

采样窗口使用模型中的 `TurnRegions`，不按图片均分线直接切割。当前固定朝向版本继续使用V3正面身体、拳和靴掌及原有长柄刀；V4侧背面身体补件保留在素材库，不在这套连斩中切换。V4披风仍用于领口固定的衣物网格。补件不代表独立八方向站走美术已齐全。

成功的内置生图提示词：

> Generate a production transparent RGBA 4x4 sprite parts atlas. The attached image is the approved Wei Yan parts atlas: preserve his exact stocky pixel-painted style, dark green cloth, dark iron lamellar armor, aged brass trim, coarse dark outlines, short beard, and helmet without feathers. These are missing side and rear views for a spinning skeletal animation. EXACTLY 16 separate standalone parts, centered in equal cells with transparent gutters, no text, no labels, no checkerboard pixels, no assembled people. Row1: (1) complete head strict right side profile, (2) complete head directly from behind, no face, (3) chest/scarf armor right side profile without arms/head, (4) chest armor directly from behind without cape. Row2: (5) waist/belt and short skirt right side, no legs, (6) waist/belt skirt from behind, (7) rear view upper arm shoulder-to-elbow only, axis straight down, (8) rear forearm elbow-to-wrist only, straight down, no hand. Row3: (9) rear upper thigh hip-to-knee only straight down, (10) rear shin knee-to-ankle only no foot, (11) black brass-edged boot foot front view, ankle at top flat sole bottom, (12) same boot foot rear view. Row4: (13) closed gripping fist palm view wrist at top, (14) fist knuckle view wrist top, (15) dark single-edged curved heavy saber tip up, leather two-hand hilt at bottom blade upper70% hilt30%, no effects, (16) broad green cape from behind collar attachment centered at top, heavy folds and ragged bottom no body. Rear parts must actually show rear construction, not front faces. Each part fully isolated inside its cell. Match reference precisely in color and visual texture, no realism, no extra ornaments, genuine transparent alpha.

## 验证与预览

真实角色通过技能入口录制，预览复制最终绘制层、换图、深度顺序和披风网格，没有另外拼装展示动画。正常速度和分阶段画面用于复查，关节检查不能替代用户对观感的判断。

- 当前固定朝向版本：[完整动作MP4](../../Build/WeiYanSaberNoSpin/WeiYanSaberNoSpin.mp4)／[GIF](../../Build/WeiYanSaberNoSpin/WeiYanSaberNoSpin.gif)／[动作分镜](../../Build/WeiYanSaberNoSpin/ContactSheet.png)。
- [当前动作专项日志](../../Build/WeiYanSaberNoSpin/Output.log)。
- 历史旋转版（已替换）：[原视频并排对照](../../Build/WeiYanSaberMotion/ReferenceComparison.mp4)、[专项](../../Build/WeiYanSaberMotion/FinalOutput.log)、[既有姿态](../../Build/WeiYanSaberMotion/PoseOutput.log)、[可见性](../../Build/WeiYanSaberMotion/VisibilityOutput.log)、[战斗](../../Build/WeiYanSaberMotion/Combat.log)、[角色换装](../../Build/WeiYanSaberMotion/ModuleFinal.log)。

当前版本编译0警告、0错误，33项专项实机检查通过：整段保持朝向、刀不整圈轮转、双脚不交叉换位，实际绘制不切换侧背身体贴图；同时复查骨长、握持、足点、肘膝、连续采样、六段伤害、窗口间隙、正反向、暂停、打断及恢复。骨长最大误差0.00001，握柄与足点误差在日志精度内为0；肘最大140.7°、膝最大111.0°。已检查15格实机分镜，导出720×640、5秒MP4及GIF。历史旋转版曾通过专项30项、既有姿态148项、可见性49项、战斗88项和角色换装27项，这些旧结果保留追溯，不作为新版观感已获用户认可的声明。
