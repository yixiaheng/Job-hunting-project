# 贝洛伯格·上层区：Blender / FBX 导出结果

日期：2026-11-03。由原始 Belobog.pmx、Tramcar.pmx 经 Blender 3.6.23 导入后导出。

## 文件

- BelobogUpperDistrict.fbx：主场景，含基础材质及内嵌贴图。
- Tramcar.fbx：独立有轨电车，含基础材质及内嵌贴图。
- Textures/：保留源包全部 893 张 PNG，包括未直接用于基础颜色的贴图。
- BlenderSource~/：可继续编辑的两个 .blend；使用相对贴图路径。目录末尾的 ~ 避免 Unity 自动重复导入 .blend。
- SOURCE_README.txt：原始作者说明，按源编码原样保留。
- export_report.json、texture_manifest.json、verification_report.json：导出数据和验证记录。

## 导入 Unity

1. 将本目录整体复制到 Unity 项目的 Assets 下，保持目录结构。
2. 使用 FBX 导入 Unity；先在 Blender 中查看时切换“材质预览”。
3. 如需独立 .mat，在 Unity 的 FBX Inspector > Materials 中提取材质；内嵌贴图也可用 Extract Textures 提取。
4. URP 项目检查材质 Shader 为 Universal Render Pipeline/Lit，并检查带透明通道的材质。
5. 未生成 Unity 碰撞体、导航网格或交互逻辑。

## 验证结果

- BelobogUpperDistrict：1850657 顶点，1206598 三角面，239 材质，239 张内嵌图片；UV 存在，.blend 可重新打开，使用的图片路径无缺失。
- Tramcar：22370 顶点，17977 三角面，5 材质，5 张内嵌图片；UV 存在，.blend 可重新打开，使用的图片路径无缺失。

采用 0.08 的 PMX 导入比例；电车长约 8.96 米。保留源模型空间关系，未对整张地图做归一化缩放。
导入器自动清理了主场景中 233 个无效/重复面。
本次完成 Blender 导入/导出、FBX 数据检查和 .blend 重新加载检查；未在 Unity 中执行实际导入或运行验证。

## 材质转换边界

保留基础颜色贴图、透明度、双面设置、UV 和几何；材质转换为基础 Principled 结构，供 FBX 传递。
MMD 专用 toon、sphere map、描边、材质变形和物理未重建为 Unity 系统，不能将本结果视作原游戏渲染效果的完整还原。
主场景约 121 万三角面，包含一整张大地图；本次未做分区、LOD 或性能优化。

## 源包使用说明

原始说明第 5 项写明：仅允许用于星穹铁道 MMD 视频，并禁止其他领域用途（例举 VRChat、游戏 MOD）。另有限制二次配布和商业使用的条款。
格式转换不会改变这些使用限制；源说明已随本目录保留。
