# 吕布：混元高精度骨骼与二维投影试验

日期：2026-11-05。独立 Godot 样例，供判断动画与画风；未替换正式战斗角色。

最新修订：空手侧略抬高并弯肘，待机、行走和挥戟同步；披风从肩背到下摆统一为暗红布料，保留褶皱和光照明暗。

## 打开与操作

在 Godot 打开 `Scene/LvBuHybridPreview.tscn`，按 F6。也可双击 `Build/LvBuHybridV1/OpenPreview.cmd`。

- 左侧放大检查，右侧是在二维地面上的游戏尺寸。
- WASD / 方向键移动，J 挥戟。
- 按钮切换待机、行走、挥戟、骨骼、慢放、暂停和俯视角。
- “二维帧图”切换实时三维投影与纯二维图集；帧图为下、左、上、右四方向，固定22°俯视角。
- 实时投影可选八方向。“俯视角”按钮切回实时模式。

## 资产

| 文件 | 内容 |
| --- | --- |
| `LvBuHunyuanRig.glb` | 1,493,928三角面、746,950顶点、29骨骼，含 Idle / Walk / Attack 动画及材质 |
| `LvBuAlbedo.png` | 2048×2048原图投射和侧背补色纹理 |
| `LvBuIdleFourDirections.png` | 每方向24帧、2.4秒；四行 |
| `LvBuWalkFourDirections.png` | 每方向24帧、1.2秒；四行 |
| `LvBuAttackFourDirections.png` | 每方向28帧、1.4秒；四行 |
| `Authoring/LvBuHunyuanRig.blend` | 完整高精度模型、骨架、动画和已打包纹理 |

图集每格192×192，按下、左、上、右排列四行，横向为时间，真实透明背景。各方向由同一模型渲染。

原始模型为 `G:\3D\Outputs\LvBuUltra_20261104_203934\Model.glb`：本地混元2.0、512分辨率、50步、种子12345。Blender导入时移除6个退化面，其余保留最高精度网格；没有使用Meshy模型。

材质从 `LvBuRightFacingV1.png` 投射并补侧背色块，未运行混元Paint。单张参考缺少侧背纹样，部分方向仍有纹理拉伸。动作是程序创建的基础样例，不是动作捕捉或已验收的正式技能。三维脸部未重雕，二维靠原图纹理辨识五官。

## 编辑与复现

桌面快捷方式对应 Blender：`G:\GitProgram\build_windows_x64_vc17_Release\bin\Release\blender.exe`，本次版本5.4.0 Alpha。

作者脚本顺序：Blender运行 `InspectSource.py` → `ExtractGeometry.py` → 本地混元Python运行 `PrepareBinding.py` → Blender运行 `BuildRig.py` → `ValidateRig.py`。
缓存与检查报告在 `Build/LvBuHybridV1`。交付的 `.blend` 已打包纹理，单独打开不依赖缓存。

Godot实际渲染录制：本场景运行时在 `--` 后添加 `--capture-lvbu-hybrid`。详细结果与限制见 [制作记录](../../../../Demand/美术/LvBuHybridV1.md)。
