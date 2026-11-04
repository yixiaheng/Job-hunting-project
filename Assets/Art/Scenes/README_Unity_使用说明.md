# 原神养成界面背景 / 猫猫糕

本次生成日期：2026-11-03。经 Blender 导入、整理及 FBX 导出，并在独立 Unity 6000.3.23f1 Built-in 工程中完成导入、材质、动画形变和相机渲染检查。

## 导入 Unity

1. 将 `CatCake`、`GenshinCharacterBackground`、`Shaders`、`Editor` 四个目录及其 `.meta` 文件一并复制到项目的同一资源目录，例如 `Assets/ConvertedResources/`。
2. 需要示例时，同时复制 `ConvertedResources_Demo.unity` 及其 `.meta` 文件，打开该场景即可查看组合效果。
3. 单独使用时，直接将 `CatCake/CatCake.prefab`、`CatCake/CatCake_Animated.prefab` 或 `GenshinCharacterBackground/GenshinCharacterBackground.prefab` 拖入场景。
4. 若项目使用 URP，执行菜单 **Tools > Converted Resources > Rebuild Prefabs**，按当前管线重新生成猫猫糕材质和预制体。实际自动验证使用 Built-in；URP/HDRP 未进行本次运行验证。
5. 本目录原有的“流萤的秘密基地Fin.fbx”保持原样，不属于本次转换范围。

## 猫猫糕：CatCake

- `CatCake.fbx` / `CatCake.prefab`：原始 FBX 骨架版本，保留 91 根骨骼、2687 个顶点和 UV。默认使用 04 号成品颜色贴图。
- `CatCake_Animated.fbx` / `CatCake_Animated.prefab`：由 PMX 和附带 VMD 经 Blender 转换，保留 MMD 版本的 92 根骨骼，含 Idle（待机）、Bounce（弹弹）两段 30 fps 动画。
- `CatCake.controller`：默认播放 Idle；Bounce 状态已建立，可通过 Animator.Play("Bounce") 播放。两段动画的顶点形变已在 Unity 中采样检查。
- 两个版本的骨架不同，请将动画版 FBX、Avatar 和预制体配套使用，不要直接把其动画当作原始 91 骨骼模型的兼容动画。
- `Textures/`：保留源包全部 62 张 PNG；`Materials/` 提供对应编号材质。02–35 是可直接使用的颜色贴图；00、01 的 RGB/A 是原游戏着色器使用的颜色/遮罩数据，相应材质仅供查看原始数据，不等于完成游戏着色器还原的皮肤。
- 其他 Decal / UI3D 贴图按原名保留，未擅自混合进主体颜色。
- MMD 物理未导出为 Unity 物理系统。

## 原神养成界面背景：GenshinCharacterBackground

- `GenshinCharacterBackground.fbx`：天空穹顶、地面、相机和原始灯光；Unity 预制体另配可用材质、灯光和粒子。
- `GenshinCharacterBackground.prefab`：配置好的天空材质、渐隐环境反射地面和 Unity 上升星光粒子。附带相机已修正近裁剪距离。
- `Textures/Genshin_Sky.exr`：由原始 Blender 程序化世界材质在第 120 帧烘焙的 2048×1024 HDR 星空。
- `Background_1920x1080.png`：在 Blender 中渲染的静态背景，可直接作为 Unity UI 背景图片；导入为 Sprite 后用于 Image，或用 Texture 配合 RawImage。
- 背景采用作者提供的简化版场景。完整版引用的外部 Alembic 文件不在源包中，未虚构或补造该文件。
- Blender 世界节点与粒子缓存不能随 FBX 在 Unity 中执行。Unity 版本使用静态烘焙星空和重新配置的 Unity 粒子；不是逐帧、逐节点完全一致的动态特效。
- 地面 Shader 反射烘焙环境并随距离渐隐；不提供角色的实时平面镜像。需要角色倒影时，应按目标渲染管线另接平面反射方案。
- 原工程依赖外部 ACES 色彩配置，当前输出使用 Standard / sRGB，显示色调可能与作者的 ACES 环境略有差异。

## Blender 源文件

- `CatCake/BlenderSource~/CatCake.blend`
- `CatCake/BlenderSource~/CatCake_Animated.blend`
- `GenshinCharacterBackground/BlenderSource~/GenshinCharacterBackground.blend`

源文件可用 Blender 2.93.18 打开。贴图路径指向各资源自身的 `Textures` 目录，背景粒子缓存已随附。请保持文件夹相对位置。目录名末尾的 `~` 用于阻止 Unity 自动重复导入 `.blend`；Unity 使用配套 FBX 即可。

## 验证与来源

`UnityPreview.png`、`UnityPreview_Original.png` 为独立 Unity 工程实际相机截图；`verification_report.txt` 为检查摘要。

猫猫糕来源及限制请查看 `CatCake/SOURCE_LICENSE.txt`：来源 miHoYo、模型编辑葵岚；源说明明确禁止商业用途和二次配布，并要求完整署名。转换不改变原作者限制。背景作者说明保存在 `GenshinCharacterBackground/SOURCE_README.txt`。
