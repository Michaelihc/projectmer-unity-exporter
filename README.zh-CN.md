# Project MER Unity 导出器

[English](README.md)

这是一个 Unity 编辑器包，用于在 Unity 中导入、制作、检查并导出 [Project MER（Map Editor Reborn）](https://github.com/Michal78900/ProjectMER) JSON 蓝图（Schematic）。

它可以把由受支持的基础几何体、灯光、文字和空物体组成的 Unity 层级导出为 `.mer.json`，也可以把已有的 Project MER JSON 蓝图重新载入 Unity 继续编辑。

> [!IMPORTANT]
> 这是独立的社区工具，不是 Project MER 或 Northwood Studios 的官方项目。它不会替你在 SCP:SL 服务器上安装 Project MER。

## 支持内容

- Unity 内置的球体、胶囊体、圆柱体、立方体、平面和 Quad
- 用作根节点、父节点或动画标记的空 GameObject
- 聚光灯、方向光和点光源
- 通过元数据组件配置的 Project MER 文字块
- 本地坐标、旋转、缩放、父子关系、名称和固定对象 ID
- 几何体颜色、可见、碰撞和静态/可移动标志
- Project MER Animator 名称字段
- 导入现有 Project MER JSON 进行编辑或检查
- 导入并再次导出时，无损保留尚未建模的属性和不支持的游戏逻辑块类型
- 写入文件前的严格检查

## 不支持内容

- 导入的 FBX、OBJ、GLB 或其他任意网格
- `SkinnedMeshRenderer` 蒙皮网格
- 贴图、UV、自定义 Shader 或多材质外观
- Unity 脚本、Animator Controller、动画片段、粒子、音频或物理组件
- 普通 SCP:SL 客户端本身无法渲染的资源

Project MER 蓝图描述的是可通过网络同步的 SCP:SL 对象，不包含任意网格顶点，也不是 AssetBundle。自定义模型必须先使用六种受支持的基础几何体重新搭建或近似还原，才能导出。

## 环境要求

- Unity 2021.3 或更新版本
- 使用 Unity Package Manager 的 Git URL 安装时，需要本机安装 Git
- 若要在游戏中使用，需要另外准备安装了兼容版 [Project MER](https://github.com/Michal78900/ProjectMER) 的 SCP:SL 服务器

本包依赖 Unity 的 `com.unity.nuget.newtonsoft-json`，Unity Package Manager 会自动安装该依赖。

## 安装方法

### 推荐：使用固定版本 Git URL

1. 打开 Unity 项目。
2. 选择 **Window → Package Manager**。
3. 点击左上角的 **+**。
4. 选择 **Install package from git URL…**。
5. 输入：

   ```text
   https://github.com/Michaelihc/projectmer-unity-exporter.git#v0.1.1
   ```

6. 点击 **Install**，等待 Unity 编译完成。
7. 确认顶部菜单中出现 **Tools → ProjectMER**。

使用 `#v0.1.1` 可以确保朋友或团队成员安装完全相同的版本。如果希望直接跟随最新 `main` 分支，可以删除版本后缀，但不建议在正式项目中这样做。

### 修改 `Packages/manifest.json` 安装

在项目 `Packages/manifest.json` 的 `dependencies` 对象中加入：

```json
"com.scpsl.projectmer-authoring": "https://github.com/Michaelihc/projectmer-unity-exporter.git#v0.1.1"
```

如果它不是第一项，注意上一项末尾必须有英文逗号，保证 JSON 格式正确。

### 安装下载到本地的副本

1. 下载并解压仓库或 Release 源码压缩包。
2. 在 Unity 中打开 **Window → Package Manager**。
3. 点击 **+**，选择 **Install package from disk…**。
4. 选择解压目录根部的 `package.json`。

通过 UPM 安装后，不要再把第二份代码放入 `Assets/`，否则重复的程序集定义会引发编译错误。

## 制作并导出蓝图

### 第一步：建立干净的层级

1. 新建空 GameObject，并将名称改为希望导出的蓝图名称。
2. 如果没有特殊需求，根物体使用本地坐标 `(0, 0, 0)`、旋转 `(0, 0, 0)`、缩放 `(1, 1, 1)`。
3. 通过 **GameObject → 3D Object** 创建 Unity 内置基础几何体作为子物体。
4. 可以继续使用空 GameObject 对模型进行分组。
5. 使用子物体的本地坐标相对于父物体进行排布。

被选中的根物体固定导出为对象 ID `0`。其他对象会自动获得稳定 ID，除非你通过元数据手动指定。

### 第二步：配置 Project MER 专用属性

需要覆盖默认行为时，选中对象，在 Inspector 的 **Add Component** 中添加 **ProjectMER → Export Metadata**。

`Block Kind` 决定对象如何导出：

| 值 | 行为 |
| --- | --- |
| `Auto` | 自动识别内置几何体、受支持的 Unity 灯光或空节点。 |
| `Empty` | 只导出坐标和层级节点，适合父节点或标记点。 |
| `Primitive` | 导出为 Project MER 基础几何体。没有可识别的内置网格时，需要启用 `Override Primitive Type`。 |
| `Light` | 把 Unity Light 导出为 Project MER 灯光块。 |
| `Text` | 使用元数据中的文字和显示尺寸导出文字块。 |
| `Ignore` | 忽略当前对象及其全部子树；被选中的根物体不能设为 Ignore。 |

常用字段说明：

- `Object Id`：保留 `-1` 即可自动分配。手动填写的子对象 ID 必须大于 0 且不能重复。
- `Animator Name`：写入 Project MER 的 Animator 名称字段；它不会导出 Unity 动画片段或 Controller。
- `Override Color`：启用后使用元数据颜色，而不是 Renderer 中第一个可用材质颜色。
- `Visible`：启用几何体的可见标志。
- `Collidable`：启用 Project MER 碰撞；为了避免意外生成大量碰撞，该选项默认关闭。
- `Static`：如果该部件需要在运行时移动，应关闭此项。
- `Light Shape`：选择 MER 灯光源形状元数据。
- `Text` 和 `Display Size`：配置文字内容和显示尺寸。

### 第三步：检查

1. 在 Hierarchy 中选中模型的根物体。
2. 选择 **Tools → ProjectMER → Validate Selected Hierarchy**。
3. 在 Console 中查看警告和错误。
4. 修复全部错误后再导出。

检查器会发现自定义网格、蒙皮网格、不支持的灯光类型、重复 ID、父节点缺失和非法坐标等常见问题。

### 第四步：导出

1. 保持根物体处于选中状态。
2. 选择 **Tools → ProjectMER → Export Selected Hierarchy…**。
3. 保存生成的 `<名称>.mer.json` 文件。

检查失败时不会写入文件。

### 第五步：放入 SCP:SL 服务器

1. 按照 [Project MER 官方仓库](https://github.com/Michal78900/ProjectMER)的说明，在 SCP:SL 服务器上安装并启动 Project MER。
2. 把导出的文件复制到：

   ```text
   SCP Secret Laboratory/LabAPI/configs/ProjectMER/Schematics/
   ```

3. 如果插件没有立即识别蓝图，重启服务器。
4. 使用当前 Project MER 版本支持的命令加载或生成该蓝图。

当前 Project MER 文档把地图和蓝图目录放在 `LabAPI/configs/ProjectMER`。不同版本的服务器命令可能变化，因此加载命令应以你实际安装版本的文档或对应 Discord 公告为准。

## 导入现有 Project MER 蓝图

1. 选择 **Tools → ProjectMER → Load JSON…**。
2. 选择 `.json` 或 `.mer.json` 文件。
3. 工具会在当前场景中建立可编辑的 Unity 层级。
4. 在 Console 中检查警告。

受支持的块会还原为 Unity 基础几何体、灯光、文字预览或空节点。未知块类型会成为只有 Transform 的占位节点并给出警告，但其原始块类型和属性会存入隐藏元数据，以便再次导出。受支持块上的额外属性也会保留，因此 `MovementSmoothing`、灯光闪烁设置以及未来新增的 Project MER 属性都能通过导入/导出往返保存。预览材质只在内存中临时创建，不会写入项目资源。

## 常见问题

### 看不到 `Tools → ProjectMER` 菜单

- 等待 Unity 完成编译。
- 打开 **Window → General → Console**，修复所有编译错误，包括与本包无关的项目错误。
- 在 Package Manager 中确认本包已经出现。
- 删除 `Assets/` 或其他本地路径中的重复副本。
- 如果使用 Git URL，确认从启动 Unity 的环境中可以调用 Git。

### Unity 提示无法添加包

- 使用包含 `.git` 的完整 URL。
- 确认电脑可以访问 GitHub。
- 优先使用带版本标签的 URL，而不是 `main`。
- 仍然失败时，下载 Release 源码压缩包，然后使用 **Install package from disk…**。

### 导出提示网格不受支持

该对象没有使用 Unity 原始的六种内置几何体网格。请用内置几何体重新搭建；如果它只应作为标记点，可以设为 `Empty`；如果完全不需要导出，可以设为 `Ignore`。仅把任意网格重命名为 “Cube” 并不会转换几何数据，也不是受支持的做法。

### 游戏中的颜色和 Unity 不同

Project MER 基础几何体只有一种颜色，而 Unity Renderer 可能拥有多个材质和复杂 Shader。启用颜色覆盖时，导出器使用元数据颜色；否则使用第一个可用材质颜色。贴图和 Shader Graph 不会被导出。

### 游戏中的碰撞和 Unity 不同

Unity 会自动给内置基础几何体添加 Collider，但本工具把 Project MER 碰撞设为明确启用。给需要碰撞的物体添加元数据组件并启用 `Collidable`。不要给纯装饰零件全部开启碰撞，否则会增加服务器和客户端负担。

### 导入结果与游戏中的蓝图不完全一致

导入功能主要用于制作和预览。不支持的 Project MER 块会成为占位节点，无法在 Unity 中可视化配置，但其源属性会保留以便再次导出。若将占位节点主动转换为其他块类型，其原有类型专属数据会被替换。Unity 的材质与灯光也无法完全复现 SCP:SL 的最终画面。发布前必须在私人 SCP:SL 测试服务器中进行最终检查。

## 自动化 API

其他 Editor 脚本可以不打开保存窗口直接导出：

```csharp
using Scpsl.ProjectMer.Authoring.Editor;
using UnityEngine;

ProjectMerExportResult result = ProjectMerSceneExporter.ExportHierarchyToFile(
    rootGameObject,
    @"C:\schematics\example.mer.json");

if (!result.Success)
{
    foreach (string error in result.Errors)
        Debug.LogError(error);
}
```

如果只需要经过检查的 JSON 字符串而不写入文件，可以调用 `BuildHierarchyJson(rootGameObject)`。

## 运行测试

1. 打开 **Window → General → Test Runner**。
2. 选择 **EditMode**。
3. 如果当前 Unity 版本默认隐藏包测试，请启用 package tests。
4. 运行 `Scpsl.ProjectMer.Authoring.Editor.Tests` 程序集。

## 开源协议

[MIT](LICENSE)

SCP: Secret Laboratory、Project MER、Unity 和 GitHub 的商标及项目权利归各自所有者。本仓库与 Northwood Studios、Project MER 维护者或 Unity Technologies 没有从属或官方授权关系。
