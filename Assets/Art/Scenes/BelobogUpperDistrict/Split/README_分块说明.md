# 贝洛伯格分块版本

已生成并导入 Unity：1,886 个普通 Mesh，847 个空间区域。没有添加 Collider。

## Unity 中使用

项目中：Assets/Art/Scenes/BelobogUpperDistrict/Split/BelobogUpperDistrict_Split.prefab

当前场景已放置分块预制体，原 BelobogUpperDistrict 已保留并停用，场景未自动保存。确认后请自行保存。

展开预制体及区域，在包含 MeshFilter / Mesh Renderer 的末级物体上添加 MeshCollider。
固定地图使用 Convex 关闭、Is Trigger 关闭；Mesh 一栏应指向该物体 MeshFilter 的网格。
可多选这些末级物体批量添加组件，完成后逐个抽查 Mesh 是否自动填入。
父级 Tile / Far 是整理用空物体，不要只给父级添加空的 MeshCollider。

## 命名

- Tile_X..._Y..._H...：Blender 坐标下的空间区域，主要区域按 64 米水平、32 米高度分组。
- Far_X...：远景使用更大的分组。
- UpFacing：朝上的面，包含地面，也可能包含屋顶、台面等。
- DownFacing：朝下的面。
- Walls：接近竖直的面。
- Inclined：其他斜面。
- Transparent：使用透明材质的面，可能包含远景贴片。

这是空间和几何朝向分组，不是自动识别建筑、道路或楼梯。跨区域物体可能被分成多个 Mesh。
没有切割或删除三角形，没有减面，保留 UV、材质和分裂法线。边界处顶点会在各 Mesh 中分别保存。
Unity 导入后为 1,206,565 个三角形，与原版 Unity 网格相同。

## 材质与便携

FBX 和 prefab 依赖上一层 Materials/URP 及 Textures，239 个材质已配置外部重映射。
迁移到其他项目时复制整个 BelobogUpperDistrict 文件夹并保留 .meta，不要只复制 Split。
Blender 可编辑版本位于 BlenderSource~/BelobogUpperDistrict_Split.blend。

## 性能与碰撞

分块增加了 Renderer 数量，并没有降低总几何复杂度；此版本优先方便手动选择和配置。
先给角色附近的地面和墙壁添加 Collider。远景、透明装饰不宜全部自动添加。
楼梯行走效果、接缝与角色落地需要你添加 Collider 后在 Play Mode 中测试。

原资源的使用限制仍以 SOURCE_README.txt 为准。
