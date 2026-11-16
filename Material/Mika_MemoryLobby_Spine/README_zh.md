# 圣园未花 · 记忆大厅 Spine 交付包

从碧蓝档案（国服 / Android 资源）中提取并整理出的**圣园未花记忆大厅（Memorial Lobby）Spine 骨骼**，
包含两套可直接加载的 Spine 资产、可播放动画、扁平化背景图与逐图层顺序表。

- 角色：圣园未花（`CH0069`，三一综合学院 / 茶会，CV：东山奈央 / 龟娘）
- 角色：圣园未花（泳装）（`CH0294`，三一综合学院 / 茶会，CV：东山奈央 / 龟娘）
- 资源来源：`BA_Data/cn/android/`（本项目 `ba-downloader assets sync --region cn` 已下载并解包的产物）
- 本包全部为**复制件**，未改动 `BA_Data/cn/android/` 与 `BA_Data/deliverables/Mika_MemorialLobby/` 中的原始产物

---

## 1. 目录结构

```
Mika_MemoryLobby_Spine/
├── spine/                       # 可直接使用的 Spine 三件套
│   ├── CH0069_home/             #   .skel + .atlas + 4 张纹理页
│   └── CH0294_home/             #   .skel + .atlas + 4 张纹理页
├── background/                  # 背景场景（去掉角色后的扁平渲染 + 图层顺序表）
│   ├── CH0069_home/
│   │   ├── flattened_setup.png  # 3460x2568，setup 姿态
│   │   ├── flattened_t1.5.png   # 3460x2568，Idle_01 @ 1.5s
│   │   └── layers.csv           # 56 个背景图层，按 drawOrder 排序
│   └── CH0294_home/
│       ├── flattened_setup.png  # 4471x3883
│       └── layers.csv           # 13 个背景图层
├── renders/                     # 角色 / 全场景渲染验证帧
│   ├── CH0069_home/             # Idle_01 两帧 + 纯角色帧 + Talk_01_A 帧
│   └── CH0294_home/             # Idle_01 两帧 + 纯角色帧
├── metadata/                    # 骨架完整解析结果（骨骼/插槽/约束/图集/动画/事件）
├── preview/                     # 浏览器预览播放器（esbuild 打包产物）
├── manifest.json                # 机器可读清单（含每个文件字节数与 sha256）
├── Diagnostics/
│   └── checksums.sha256         # 全包 30 个文件的校验和
└── README_zh.md                 # 本文件
```

---

## 2. Spine 角色资产

### 2.1 导出 Spine 版本

| 项目 | CH0069_home | CH0294_home |
| --- | --- | --- |
| **导出版本（骨架头）** | `4.2.33` | `4.2.33` |
| 格式 | 二进制 `.skel` | 二进制 `.skel` |
| 骨架哈希 | `5c8f3b8b-b8ac63e` | `-154e1e85-2ffc2cd1` |
| 帧率 fps | `0`（未写 nonessential） | `30` |
| `imagesPath` / `audioPath` | `null` / `null` | `./images/` / `./VOC_CN/` |
| setup 包围盒 | `x=-1730, y=-315, 3460 x 2568.1` | `x=-2531.2, y=-1619, 4470.5 x 3882.8` |
| 骨骼 / 插槽 / 皮肤 | 743 / 294 / 1 | 441 / 215 / 1 |
| 约束 | IK 20、Transform 94、Path 0、Physics 0 | IK 22、Transform 78、Path 0、Physics 37 |
| attachment 总数 | 414（全部 MeshAttachment） | 264（260 Mesh + 4 Region） |
| 图集区域 / 页 | 380 / 4 | 257 / 4 |
| 动画 / 事件 | 52 / 15 | 31 / 27 |

> **纹理命名已规范化。** 游戏内原始文件名为 `CH0069_home.skel.bytes` / `CH0069_home.atlas.bytes`，
> 本包改为通用的 `.skel` / `.atlas`，第三方可直接加载；纹理页文件名与 `.atlas` 内声明完全一致。

### 2.2 ⚠️ 加载版本要求（本包最关键的兼容性结论）

BA 导出的 `.skel` 在头部 `height` 字段之后**多写了一个 float（值 `100.0`）**，属于官方 4.2 格式的方言。实测：

| Runtime | 结果 |
| --- | --- |
| `@esotericsoftware/spine-core` **4.2.33**（与骨架头同版本） | ❌ 解析失败（`Bone name must not be null` / `boneData cannot be null`） |
| `@esotericsoftware/spine-core` **4.2.120** | ✅ 原生解析成功，无需修改字节 |
| `@esotericsoftware/spine-core` 4.3.13 | ❌ 解析失败 |

因此请**使用 4.2 系列中较新的补丁版 runtime**（本包验证于 `4.2.120`）。
使用 Unity / PixiJS / Godot / spine-sdl 等其它官方实现时，同理选择 4.2 分支的最新补丁版本，
而不是与骨架头完全同号的 `4.2.33`。若只能在旧补丁版上运行，可考虑裁掉头部多出的 4 字节，
但本包**未做该改写**，交付的是原始字节。

### 2.3 动画名称（可播放动画）

两套骨架都**不止一个可播放动画**，且均已实测播放（不同时间点渲染出不同像素，见 `renders/`）。

**CH0069_home（52 个，卧室夜景 / 躺卧托腮姿势）**

| 组 | 动画名 | 时长 |
| --- | --- | --- |
| 主循环 | `Idle_01` | 33.3333s |
| 入场 | `Start_Idle_01` | 20s |
| 说话（女声/男声，含国服配音变体） | `Talk_01_A` / `Talk_01_M` / `Talk_01_A_CN` / `Talk_01_M_CN` | 15.6667s |
| | `Talk_02_*` | 18s |
| | `Talk_03_*` | 20s |
| | `Talk_04_*` | 19.3333s |
| | `Talk_05_*` | 35.3333s |
| 抚摸反馈 | `Pat_01_A` / `Pat_01_M`（0s）、`PatEnd_01_A`（0.3333s）、`PatEnd_01_M`（0.3333s） | |
| 视线反馈 | `Look_01_M`（0s）、`LookEnd_01_A` / `LookEnd_01_M`（0.4333s） | |
| 开发用 | `Dev_add` `Dev_BG_Cut2` `Dev_Curtain` `Dev_Curtain2` `Dev_eye` `Dev_flow` `Dev_Hair` `Dev_Hair2` `Dev_Halo` `Dev_Idle_1` `Dev_LookEnd_01_M` `Dev_Look_01_M` `Dev_PatEnd_01_M` `Dev_Pat_01_M` `Dev_pos` `Dev_Set` `Dev_Skut` `Dev_Talk_01_M`…`Dev_Talk_05_M` | 多为 3.3333s |
| 占位 | `Dummy` | 0.0333s |

> `Talk_0N_A` / `Talk_0N_M` / `Talk_0N_A_CN` / `Talk_0N_M_CN` 四者时长相同，差别只在口型与配音事件；
> `A` = 女声版、`M` = 男声（主角）版、`_CN` = 国服配音轨。完整名单与 timeline 数见 `manifest.json`。

**CH0294_home（31 个，海边夜景 / 抱膝坐姿）**

| 组 | 动画名 | 时长 |
| --- | --- | --- |
| 主循环 | `Idle_01` | 12s |
| 入场 | `Start_Idle_01` | 21.6667s |
| 说话 | `Talk_01_A` / `Talk_01_M` / `Talk_01_A_CN` / `Talk_01_M_CN` | 20.6667s |
| | `Talk_02_*` | 18.8333s |
| | `Talk_03_*` | 20.8333s |
| | `Talk_04_*` | 24.1667s |
| | `Talk_05_*` | 13.1667s |
| 抚摸 | `Pat_01_A` / `Pat_01_M`（0.3333s）、`PatEnd_01_A` / `PatEnd_01_M`（1.1667s） | |
| 视线 | `Look_01_M`（0.3333s）、`LookEnd_01_A`（0.6667s）、`LookEnd_01_M`（0.6667s） | |
| 眨眼 | `Eye_Close_01` | 0.3667s |
| 占位 | `Dummy` | 0.0333s |

### 2.4 骨骼事件（配音/音效触发点）

- `CH0069_home`：`CH0069_MemorialLobby_0`、`_1_1`、`_1_2`、`_2_1`、`_2_2`、`_3_1`、`_3_2`、
  `_4_1`、`_4_2`、`_4_3`、`_5_1`、`_5_2`、`_5_3`、`_5_4`、`Talk`
- `CH0294_home`：`CH0294_MemorialLobby_1_1`…`_5_2`（13 个）+ 同名 `Sound/…` 前缀事件 13 个 + `Talk`

对应的 `.ogg` 语音已在同仓库的另一份交付中，未在本包内重复存放：
`BA_Data/deliverables/Mika_MemorialLobby/voice_cn/`、`voice_jp/`（各 27 个文件，与事件名一一对应）。
`CH0294_home` 骨架头里的 `audioPath = ./VOC_CN/` 即游戏运行时的语音挂载目录。
大厅环境音与特效音（海浪、流星、星铃等）在 `BA_Data/deliverables/Mika_MemorialLobby/shared_lobby_fx/sfx/`，
例如 `SFX_Spine_CH0294_OceanWave.ogg`。

---

## 3. 背景场景

### 3.1 是否已包含在 Spine 中？——**是，完全内嵌**

两套记忆大厅的背景**不是独立于角色的资源**，而是同一骨架内的普通插槽（slot）：

- `CH0069_home`：294 个插槽中 56 个属于场景件（壁纸 `BG/BG_01`、窗与月光 `BG2/Window_*`、
  床与枕头 `BG_Bed` / `BG_Pillow`、书桌 `Table_*`、暖气片、`BG_screen_01`、`mood_Light` 等），
  其余 238 个为角色与手持特效。
- `CH0294_home`：215 个插槽中 13 个属于场景件（`BG_02` / `BG_03` 海面与夜空、`BG_Wave1`…`BG_Wave5`
  五层浪、`BG_fx03` 光带、`FX_light_01`…`FX_light_04`  additive 光斑），其余 202 个为角色。

验证方式：把角色类插槽全部置空后渲染，房间 / 海边场景仍然完整（见 `background/*/flattened_setup.png`）。
所以**不需要额外的背景图资源**。

在工作区里可核对到的来源：记忆大厅资源位于 Unity 包组 `Assets/_MX/SpineLobbies/<骨架名>/`，
其依赖包为 `-_mxdependency-{textassets, textures, materials, timelines, assets, prefabs}`，
其中 `textassets` 存放 `.skel.bytes` / `.atlas.bytes`，`textures` 存放图集页——**没有独立于骨架的大厅背景贴图**。
骨架之外的场景侧美术只有 Unity 粒子网格（`BA_Data/deliverables/Mika_MemorialLobby/CH0294_home/Effect/*.glb`、
`BA_Data/deliverables/Mika_MemorialLobby/shared_lobby_fx/FX_LobbyCH0069.glb`），它们不是背景本体。

### 3.2 分辨率

| 项目 | CH0069_home | CH0294_home |
| --- | --- | --- |
| 场景画布（setup 包围盒，Spine 单位＝像素 @1x） | **3460 x 2568** | **4471 x 3883** |
| 扁平背景图（本包 PNG） | `background/CH0069_home/flattened_setup.png` 3460x2568 RGBA | `background/CH0294_home/flattened_setup.png` 4471x3883 RGBA |
| 图集页 | `CH0069_home.png` 2048x2048（305 区域）<br>`CH0069_home2.png` 2048x2048（6）<br>`CH0069_home3.png` 2048x2048（43）<br>`CH0069_home4.png` 2048x2048（26） | `CH0294_home.png` 2048x2048（199）<br>`CH0294_home_2.png` 2048x1024（1）<br>`CH0294_home_3.png` 2048x2048（8）<br>`CH0294_home_4.png` 2048x1024（49） |
| 游戏内实际显示分辨率 | 由 Unity 侧 Camera/Canvas 决定，骨架本身不含输出分辨率 | 同左 |

> 场景画布比图集页大得多：背景由多块贴图在骨骼变换下拼合而成，**单个图层的实际显示尺寸远大于其在图集里的像素尺寸**
> （例如 `CH0294_home` 的 `BG_03` 图集区域 1581x1008，场景中覆盖 4148x2566）。放大倍率来自骨骼 scale，不是贴图分辨率。

### 3.3 图层顺序

`background/<骨架>/layers.csv` 按 **drawOrder 升序（数值越小越靠底）** 列出全部背景图层，字段：

| 列 | 含义 |
| --- | --- |
| `drawOrder` | 插槽在 `data.slots` 中的序号，即 setup 绘制顺序（动画可用 `DrawOrder` 时间线改变） |
| `slot` / `bone` | 插槽名 / 所挂骨骼名 |
| `blendMode` | `normal` / `additive` / `multiply` / `screen` |
| `attachment` / `region` | setup 姿态下的 attachment 名 / 图集区域名（含 `BG/`、`BG2/`、`01/` 等子目录前缀） |
| `atlasPage` / `atlasX` / `atlasY` / `atlasWidth` / `atlasHeight` / `regionRotated` | 该图层源像素在图集页中的矩形（spine-core 解析结果） |
| `sceneX` / `sceneY` / `sceneWidth` / `sceneHeight` | setup 姿态下该图层世界坐标包围盒（**Spine 坐标系 Y 轴向上**，`sceneX/sceneY` 为左下角） |

`CH0069_home` 底部起始顺序示例：`0 BG_01`（壁纸）→ `1 Window_02` → `2 Window_01` → `3.. Window_Flower_*`
→ … → `48 BG_Box` → `49 BG_Bed` → `50 BG_Pillow` → `51 Char_BG_Screen_02` → `52 BG_screen_01`，
最顶为 `293 FX_Screen_01`（additive 屏幕光）。

> **分类是启发式的。** 场景/角色按「插槽名或图集目录命中 `BG|Box|Bed|Pillow|Curtain|Window|Table|radiator|Wall|mood|Wave|Sky|Screen|fx0|_Light`」判定，
> 判据集中在 `BA_Data/spine-lab/bg_pattern.mjs`。个别名字含 `BG` 的插槽其实挂在角色骨骼上
> （如 `Char_BG_Screen_02` 挂在 `Hip`），需要时可在此处调整。

### 3.4 本包未提供「逐图层独立 PNG」的原因

BA 大厅的 attachment 几乎全是 **MeshAttachment**（CH0069 为 414/414，CH0294 为 260/264）。
网格在场景中是被骨骼变形过的（如 `BG_Bed` 是一张被压成透视角度的床罩），
按场景空间裁出的图层是**已变形的结果**而非可复用的矩形素材；
真正的原始图层素材已经完整保存在 `spine/*/*.png` 图集页内，`layers.csv` 给出了每一层的精确矩形位置。
如需批量拆图，按 `atlasPage + atlasX/atlasY/atlasWidth/atlasHeight + regionRotated` 裁切即可。

---

## 4. 预览与验证

预览播放器已随包提供（`preview/`，spine-canvas 4.2.120 + esbuild 打包）。

```shell
# 在本仓库根目录
node BA_Data/spine-lab/serve.mjs            # 重新打包并起本地服务
# 打开 http://127.0.0.1:8471/preview/index.html?skeleton=CH0069_home&anim=Idle_01
```

URL 参数：`skeleton=CH0069_home|CH0294_home`、`anim=<动画名>`、`t=<秒>`、
`view=full|bg|char`（全场景 / 仅背景 / 仅角色）、`loop=0|1`、`scale=<倍率>`、`bg=<CSS 颜色>`。

已实测通过的项目：

- 两套骨架均能被 spine-core **4.2.120** 完整解析，414 / 264 个 attachment **全部在图集中命中，0 缺失**。
- `Idle_01` 在不同时间点（0.5s / 8.0s，CH0294 为 0.5s / 6.0s）渲染出**不同像素**，动画确实在播放。
- `Talk_01_A`（CH0069 @ 2.0s）与 `Idle_01` 均正常出帧，见 `renders/`。
- `view=bg` 去角色后场景完整；`view=char` 去场景后角色完整。

校验包完整性（在本目录内执行，30 个文件）：

```shell
sha256sum -c Diagnostics/checksums.sha256
```

> 渲染器说明：`spine-canvas` 的 `SkeletonRenderer` 默认 `triangleRendering=false`，只画 RegionAttachment，
> 对 BA 大厅（全网格）会**什么都不画**；本包的 `BlendAwareRenderer` 打开了三角渲染，
> 并按 drawOrder 中连续相同 `blendMode` 分段设置 `globalCompositeOperation`，
> 以近似 Unity 端的 additive / screen 混合。这是**预览近似**，与游戏内 Unity 渲染不保证逐像素一致。

---

## 5. 兼容性与风险

| 风险 | 说明 | 处置建议 |
| --- | --- | --- |
| **runtime 补丁版本** | 骨架头写 `4.2.33`，但同版本号 runtime 解析失败，只有 `4.2.120` 通过 | 固定使用 4.2 分支最新补丁版 runtime；不要按骨架头版本号选包 |
| **两套 atlas 方言不同** | `CH0069_home.atlas` 为 3.8 风格（`xy/size/orig/offset/rotate: true`）；`CH0294_home.atlas` 为 4.x 风格（`bounds/offsets/rotate: 90\|270/scale: 0.54`） | spine-core 4.2 两者都能解析（已验证）；第三方 atlas 解析器需同时支持 `rotate: 270` 与 `bounds` 写法 |
| **`scale: 0.54` 被忽略** | spine-core 4.2 的 atlas 解析器不读 `scale` 字段。区域 UV 由 `bounds` 与页尺寸推出，**不影响渲染正确性**（已验证） | 仅作为导出缩放信息记录，无需处理 |
| **nonessential 数据缺失** | `CH0069_home` 的 fps=0、`imagesPath/audioPath=null`、多数 mesh 的 `width/height=0` | 需要 fps 时按 30 处理（`CH0294_home` 头部即 30，且游戏内动画曲线按 30 帧导出） |
| **物理约束** | `CH0294_home` 有 37 个 PhysicsConstraint（头发/裙摆摆动） | runtime 必须支持 4.2 Physics；不支持时摆动物件会保持 setup 姿态 |
| **混合模式 / PMA** | 图集为**非预乘 alpha**（`filter: Linear,Linear`，无 `pma: true` 声明）；canvas 预览的 additive/screen 为近似 | 在 Unity / 自绘管线中按 `atlas` 的 pma 状态设置纹理采样，并逐插槽应用 `blendMode` |
| **动画语义命名** | `Dev_*`、`Dummy`、`Look_01_M`（时长 0s）等并非全部面向玩家 | 集成时只取 `Idle_01` / `Start_Idle_01` / `Talk_*` / `Pat*` / `Look*` 系列 |
| **版权** | 素材属 Nexon / Yostar / 星啸，仅限学习用途 | 不要公开再分发或用于商业项目 |

---

## 6. 复现本包的步骤

1. 本包**没有重新向服务器下载**，素材来自工作区已有的国服产物。若需重新获取该包组：
   ```shell
   ba-downloader assets sync --region cn --filter "path~spinelobbies"
   ```
   对应的 Unity 路径为 `Assets/_MX/SpineLobbies/CH0069_home/` 与 `.../CH0294_home/`。
   注意 `--filter` 模式下 Bundle 间依赖不会被处理，纹理与 TextAsset 需按上面的包组分别取回。
2. 从解包结果中取出 `CH0069_home` / `CH0294_home` 的 `.skel.bytes`、`.atlas.bytes` 与纹理页，
   规范化扩展名后放入 `spine/`（本包为复制件，原文件仍在 `BA_Data/deliverables/Mika_MemorialLobby/`）。
3. 用 4.2.120 runtime 解析并导出元数据与图层表：
   ```shell
   cd BA_Data/spine-lab
   node export_metadata.mjs CH0069_home <skel> <atlas> <out.json>
   node export_layers.mjs   CH0069_home <skel> <atlas> <layers.csv>
   ```
4. 起预览服务，用浏览器播放器导出 `renders/` 与 `background/` 的 PNG（工具脚本见 `BA_Data/spine-lab/`）。
5. 最后重建索引与校验和（它会连本文档一起哈希，因此必须放在最后一步）：
   ```shell
   node export_manifest.mjs ../deliverables/Mika_MemoryLobby_Spine
   ```

工具脚本：`inspect_spine.mjs`（骨架头与字段探查）、`export_metadata.mjs`、`export_layers.mjs`、
`export_manifest.mjs`（manifest.json + Diagnostics/checksums.sha256）、
`bg_pattern.mjs`（场景/角色判据）、`serve.mjs`（打包 + 静态服务 + `/save` 落盘）、`player/player.mjs`。
