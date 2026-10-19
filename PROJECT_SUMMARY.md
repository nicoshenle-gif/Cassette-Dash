# 🎵 《Cassette Dash!》项目完整工作内容总结与开发文档

> **项目名称**：Cassette Dash!  
> **开发环境**：Unity 6000.4.3f1 (URP)  
> **工程目录**：`d:\unityproject\Cassette Dash!`  
> **文档整理日期**：2026年10月  

---

## 目录
1. [项目概述与玩法核心](#一项目概述与玩法核心)
2. [关卡程序化生成系统](#二关卡程序化生成系统)
3. [角色控制器与跑酷手感调优](#三角色控制器与跑酷手感调优)
4. [跑酷障碍物与机关体系](#四跑酷障碍物与机关体系)
5. [垂直攀爬与天窗逃脱系统](#五垂直攀爬与天窗逃脱系统)
6. [磁带顶部铁轨与火车通关演出](#六磁带顶部铁轨与火车通关演出)
7. [模型资产处理与 PBR 渲染工作流](#七模型资产处理与-pbr-渲染工作流)
8. [核心技术参数与关键代码映射](#八核心技术参数与关键代码映射)
9. [Unity CLI 自动化与研发工具链](#九unity-cli-自动化与研发工具链)

---

## 一、项目概述与玩法核心

《Cassette Dash!》是一款将**复古磁带（Audio Compact Cassette）**微观机械构造转化为物理跑酷平台的横版动作过关游戏。

玩家化身为一名跑酷者，在高速运转的磁带内部奔跑，利用磁带表面的运动惯性跳跃躲避伸缩尖刺与悬垂机械机关，最终顺着右侧齿轮上方的垂直爬梯穿越外壳天窗，抵达磁带外壳顶部，搭乘复古磁带列车开往终点过关。

```mermaid
graph LR
    A["起始：左齿轮跑酷段"] --> B["中段：磁头与悬垂陷阱区"]
    B --> C["高潮：右齿轮引带区"]
    C --> D["垂直转移：攀爬梯子穿越天窗"]
    D --> E["终点：登上磁带列车"]
    E --> F["结算：列车鸣笛开走通关"]
```

---

## 二、关卡程序化生成系统

核心生成逻辑由 `Assets/Editor/CassetteLevelGenerator.cs` 实现，通过 Unity 顶部菜单 `Tools -> Generate Cassette Level` 可一键全自动化重建。

### 1. 真实物理尺寸还原
关卡依照现实工业标准磁带等比例放缩建模：
- **磁带外壳（Shell）**：长宽深约为 $37.5\text{ m} \times 16.3\text{ m} \times 2.38\text{ m}$；
- **左右齿轮转盘（Reels）**：中心距 $19.04\text{ m}$，半径 $2.4\text{ m}$，采用 96 边平滑圆弧网格，彻底消除低多边形阶梯卡脚问题；
- **滚轴与磁头底座（Guide Rollers & Head Base）**：底部配备倒带滚轴与磁带读写头机械底垫。

### 2. 动态走带系统（`Assets/Scripts/CassetteTape.cs`）
- 走带路径由贝塞尔平滑线与齿轮圆周切线动态结合生成网格；
- 磁带表面挂载物理碰撞体，支持玩家在带面上奔跑；
- 具备左右轮带卷动态消长（随着运行左轮带圈缩小、右轮增大）与速度联动。

---

## 三、角色控制器与跑酷手感调优

由 `Assets/Scripts/CassettePlayer.cs` 驱动，结合胶囊体碰撞和物理运动学状态机：

1. **基础机能**：
   - 地面行走、冲刺加速、带面反向阻力与顺带加速计算；
   - 梯子攀爬状态机（挂载于梯子时冻结刚体受力，改为方向键直接位移）；
   - 列车搭乘状态机（登车后挂载为列车子物体并隐藏碰撞）。
2. **跳跃与手感反复调优**：
   - 修正了早期版本中在凹陷平台“跳不上去”的问题，优化了跳跃初速度、最高高度曲线与起跳边缘的 Coyote Time（土狼时间）容错判定。
3. **自适应视口跟随（`Assets/Scripts/CassetteCameraFollow.cs`）**：
   - 平滑阻尼跟随玩家水平与垂直位移；
   - 限制在磁带框体安全可视边界内，并在登车后无缝切入远景大画幅跟随。

---

## 四、跑酷障碍物与机关体系

所有机关均集成于 `Assets/Scripts/CassetteParkourObstacleBuilder.cs` 统一生成：

| 机关组件 | 所在位置 | 行为机制与设计目的 |
| :--- | :--- | :--- |
| **伸缩尖刺（`CassettePinSpikeObstacle`）** | 底部磁带跑道 | 周期性由下往上顶出尖刺，考验起跳与滑铲时机 |
| **磁头障碍（`CassetteHeadObstacle`）** | 磁带正中底部 | 模拟磁头与压带轮复杂凹槽地形，阻挡无脑贴地冲刺 |
| **投射物陷阱（`CassetteHazardProjectile`）** | 中段空中通道 | 周期发射规避弹道，增加滞空规避难度 |
| **悬垂压制陷阱（`CassetteOverheadDropper`）** | 顶部走带正上方 | 带有机械警示色和周期升降压板，迫使玩家精确控制起跳节奏 |

---

## 五、垂直攀爬与天窗逃脱系统

为了使玩家能从磁带内部进入外部顶层，设计了垂直逃脱动线：

```
           [ 磁带外壳顶部 / 铁轨与列车 ]
====================[ 天窗开孔 ]====================
                        | |
                        | | 梯子 (3段模块化，无缝拼接)
                        | |
                 (O) 右侧齿轮转盘
```

1. **梯子交互（`Assets/Scripts/CassetteLadder.cs`）**：
   - 锚定在右齿轮正上方（$X = 9.52\text{ m}$），底端紧贴带面（$Y = 5.25\text{ m}$），顶端穿出天窗（$Y = 8.65\text{ m}$），落差 $3.40\text{ m}$；
   - 交互指令：靠近按 `F` 键即可吸附上梯，按 `W` 向上攀爬、`S` 向下，爬至顶端自动翻身登上顶壳平层。
2. **模块化拼装与角色比例契合**：
   - 针对原始 FBX 梯子单段横档过稀过大的问题，重构算法采用 **3 段模块化拼装**，横档踏步高度约为 $0.227\text{ m}$，梯宽 $0.585\text{ m}$，与主角（宽 $0.50\text{ m}$、高 $0.75\text{ m}$）的肩宽与步伐完美契合。
3. **天窗开口精准规整**：
   - 外壳顶部 `Wall_Top` 自动切割为左右两段，留下宽 $0.90\text{ m}$（$X \in [9.07, 9.97]$）的整齐天窗，梯子居中穿出，两侧留有精致边缘间隙。

---

## 六、磁带顶部铁轨与火车通关演出

由 `Assets/Scripts/CassetteRoofTrain.cs` 统一驱动，构成终点与过关结算系统：

1. **铁轨布局**：
   - 严格在天窗右边缘外侧（$X = 10.30\text{ m}$）起始，向右铺设 15 节窄轨（每节长 $2.35\text{ m}$，总长 $35+\text{ m}$），完全避开天窗洞口，并在轨面上附带行走碰撞体。
2. **列车规格与朝向**：
   - 蒸汽机车头 + 磁带造形后车厢，尺寸定标为窄轨列车比例（长 $2.27\text{ m}$，高 $0.85\text{ m}$，宽 $0.65\text{ m}$），略高于角色头顶，驾驶室舷窗与角色视线持平；
   - 修正模型本地旋转为 `Euler(0, 180, 0)`，排障器与烟囱正确朝向右侧（$+X$ 前进方向）。
3. **通关电影化演出**：
   - 玩家靠近车头时屏幕显示按键提示，按 `F` 登车；
   - 火车鸣笛启动，沿铁轨持续加速（最高航速 $16\text{ m/s}$）；
   - 烟囱根据新朝向实时向斜后上方生成带有物理飘散与渐隐的蒸汽粒子（`SteamPuff`）；
   - 主摄像机平滑切换至机车跟随视野，伴随列车驶向远方，完成通关结算。

---

## 七、模型资产处理与 PBR 渲染工作流

### 1. 解决 FBX 材质混淆隐患
- **问题排查**：梯子、铁轨、火车 3 款 FBX 导出时内部贴图重名，导致 Unity 自动绑定时错误检索并占用了主角角色的服装与鞋子贴图；
- **贴图抽离**：编写专用脚本从 FBX 二进制流中独立提取出全部 12 张高精度贴图并分类存放：
  - `Assets/Models/Ladder_Textures/`（梯子专用 Albedo / Normal / Metallic）
  - `Assets/Models/Railway_Textures/`（铁轨专用 Albedo / Normal / Metallic）
  - `Assets/Models/Train_Textures/`（火车专用 Albedo / Normal / Metallic）

### 2. 自动化材质绑定管线（`Assets/Editor/SetupModelMaterials.cs`）
- 自动设置法线贴图的 NormalMap 导入类型；
- 批量生成 URP Lit 专用材质球（`Assets/Models/Ladder_Material.mat`、`Assets/Models/Railway_Material.mat`、`Assets/Models/Train_Material.mat`）；
- 提供 `Tools -> Setup Model Materials and Rebuild` 菜单项，一键完成“贴图修正 -> 导入单位修正 -> 关卡重新生成 -> 场景自动保存”。

---

## 八、核心技术参数与关键代码映射

### 1. 角色与场景比例对齐参数表

| 元素 | X 坐标范围 | Y 坐标范围 | 物理尺寸（宽 $\times$ 高 $\times$ 深） | 作用说明 |
| :--- | :--- | :--- | :--- | :--- |
| **玩家角色** | 起点居中 | $Y \approx 5.6\text{ m}$ | $0.50\text{ m} \times 0.75\text{ m} \times 0.57\text{ m}$ | 物理基准尺度 |
| **右侧齿轮** | 中心 $X = 9.52$ | 中心 $Y = 0.0$ | 直径 $\approx 4.8\text{ m}$ | 爬梯登梯点（带面 $Y = 5.25$） |
| **梯子** | $X \in [9.23, 9.81]$ | $Y \in [5.25, 8.65]$ | $0.585\text{ m} \times 3.40\text{ m} \times 0.207\text{ m}$ | 3段拼接，踏步间距 $0.227\text{ m}$ |
| **外壳天窗** | $X \in [9.07, 9.97]$ | $Y = 8.16\text{ m}$ | 净宽 $0.90\text{ m}$，厚 $0.51\text{ m}$ | 顶部平层逃脱口 |
| **屋顶铁轨** | 起点 $X = 10.30$ | 轨面 $Y = 8.16$ | 轨距 $0.516\text{ m}$，15节总长 $35.3\text{ m}$ | 位于天窗右侧，附带行走碰撞体 |
| **磁带列车** | 初始 $X = 12.00$ | 底盘 $Y = 8.16$ | $2.27\text{ m} \times 0.851\text{ m} \times 0.651\text{ m}$ | 车头正对 $+X$，带排障器与烟囱粒子 |

### 2. 关键代码文件清单

- 关卡生成：`Assets/Editor/CassetteLevelGenerator.cs`
- 材质流水线：`Assets/Editor/SetupModelMaterials.cs`
- 玩家控制器：`Assets/Scripts/CassettePlayer.cs`
- 梯子交互：`Assets/Scripts/CassetteLadder.cs`
- 火车与铁轨系统：`Assets/Scripts/CassetteRoofTrain.cs`
- 走带物理系统：`Assets/Scripts/CassetteTape.cs`
- 摄像机跟随：`Assets/Scripts/CassetteCameraFollow.cs`
- 机关构建器：`Assets/Scripts/CassetteParkourObstacleBuilder.cs`

---

## 九、Unity CLI 自动化与研发工具链

本项目在迭代调试中深度依托 **Unity CLI**（端口 `7800`），实现了闭环研发流程：
1. **静默编译与状态监控**：通过 `unity command recompile` 与 `recompile_status` 确保脚本语法零报错；
2. **菜单指令自动化**：通过 `unity command menu --path "Tools/..."` 无缝执行生成器与材质配置；
3. **C# 探针脚本运算**：使用 `unity command eval_file` 在不中断编辑器运行的前提下，实时测量并校验场景中各物体的绝对 Bounds、世界坐标对齐度与碰撞间距；
4. **视口截屏与视觉验收**：通过 `unity command screenshot --view scene` 自动捕获特定视角渲染图，保证每次尺寸微调都拥有真实直观的画质与比例确认。

---

> 📌 **交付状态**：所有代码、模型配置、材质资产及场景均已编译通过并保存入库，关卡可直接运行游玩。
