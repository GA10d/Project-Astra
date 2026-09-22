# Project Astra 项目记忆

更新时间：2026-09-22。事实来自用户指定的 `F:\Documents\GitHub\Project-Astra`，设计建议与已实现内容须分开。

## 核心与当前方向

AI 战胜人类并以人脑作为廉价生物传感器，将单人塞入一次性微型探测舱。狭小、无法自由离开的物理空间，对照跨世纪的宇宙叙事。信息来自仪表、残缺影像、声音和设备。工业刑具、耗材身份、孤独与不确定性是重点。

当前用户要求：在 A 软件墙的现有电脑 OS 内新增“战斗模式”，做完整的平面飞机狗斗游戏，参考 Wind Runners 的动作感，改为太空主题。此要求更新旧概念稿“战斗罕见笨拙/原型不验证战斗”的早期建议。附图是参考材料，不是额外指令；其中侧倾、射击震动、受损灯光和警报与本次方向吻合。

## 场景与美术

- 四面墙：A 显示器/软件、B 工具/打印机、C 生活物资/纪念品、D 观察窗/交换舱。
- 冷战苏联潜艇改装航天器，约 2.4 米见方。灰绿钢板、米白搪瓷、黄铜表头、红色手柄、CRT、暖钨丝灯。避免宽敞洁净舰桥及霓虹赛博朋克。
- 玩家中央固定，Q/E 转墙；D 窗可以凑近。实际墙面索引是 A=0、D=1、C=2、B=3。

## 技术与目录

- 正式目录：`F:\Documents\GitHub\Project-Astra\3D Demo`；可写构建工作区：`F:\Documents\ChatGPT\Project_ Astra\Astra3D`。
- Unity 2022.3.62f1，Built-in，C#，Windows standalone。Unity：`F:\Unity\Installs\2022.3.62f1\Editor\Unity.exe`。
- `UnityProject/Assets/Astra/Scenes/Cabin04.unity`；脚本在 `Assets/Astra/Scripts/Runtime`，场景生成/打包在 `Editor/CabinBuild.cs`。
- 建模源：`Source/Astra_Cabin.blend`，Blender 5.2.2 LTS，程序生成真实网格。
- CabinComputer：IMGUI 桌面 1360×780，文件管理、文本编辑、垃圾堆、离线浏览器，窗口/任务栏。进入 OS 隔离舱室快捷键，并模糊背景。
- CabinController 管视角、交互、转墙。CabinSystems 管灯、电、打印机。SetpieceDirector 管 0–9 演出，已有窗外狗斗是演出而非可玩战斗。
- 正式笔记：`%USERPROFILE%/AppData/LocalLow/Project Astra/ASTRA - Cabin 04/astra-computer-v1.json`。QA 使用隔离文件。不得覆盖正式笔记。
- F12 截图；现有 `--astra-qa` 和 `--astra-setpiece-qa` 可做原有功能回归。

## 阅读与证据

已阅读概念文档、内饰说明、四墙概念图说明、demo README 和核心源码；已查看 A 墙概念图，并实际打开正式 demo 进入电脑 OS。开始改动前，正式项目与工作区所有 Astra C# 文件哈希一致。正式仓库当时无未提交改动。

## 战斗模块边界

第一版以独立演练任务实现完整循环，保留原有世界观与桌面。战斗数值尚不决定长期生存资源或故事结局。需要用户试玩后继续校准手感，不能把自动检查当成主观手感验收。

用户后续指定 F:\Documents\GitHub\Project-Astra\bgm.mp3 为战斗音乐，授权复制。该文件实际是 MP4/AAC，已提取为 WAV，工程中资源名为 Computer/CombatMusic。战斗开始 2.5 秒渐入，暂停/离开 0.65 秒淡出，继续恢复位置；新出击从曲目开头播放。

用户最终明确的控制方案：W 推进、S 刹车、A/D 调整机头方向、Shift 躲避、左键子弹、右键导弹。不是 WASD 平移，也不是鼠标瞄准。实现和后续调整必须保持此方案。

构建环境：受限沙箱中的 Unity 未能读取授权；使用正常系统权限的同一 Unity 安装已能读取现有许可证，未修改授权或系统时间。

## v8 实现与验证

- 新增 CombatSimulation、CombatMode/CombatModeView、CombatSound 与仅命令行启用的 CombatVerification。
- 完整短局循环：任务菜单、自由练习、三段敌群/Boss、战间三选一升级、胜负结算、重开；武器热量、护盾、电容、前向导弹锁定、雷达/边缘方向标。
- 战斗接入原 OS 桌面和任务栏；添加独立相机反馈字段，灯光效果不修改飞船真实损坏状态。切应用、失焦、退出与暂停均冻结战斗。
- Unity Windows 构建成功；战斗/音乐自动验收 37 项通过。原有电脑/舱室回归 130 项、九种演出回归 381 项通过。实际查看了 1600×900 与 1280×720 的菜单、战斗、升级、Boss、胜负截图。自动通关使用实际转向/武器输入，没有修改敌我血量。
- 视觉修正：嵌套 IMGUI 窗口中旋转 GUI.matrix 会破坏裁切；现使用预旋转像素精灵、圆环纹理及轴向像素线段。鼠标区域直接使用 GUI 局部坐标，单击在物理步之间有输入缓存。
- 本地验收工具验证功能与恢复路径，不能替代玩家对难度、节奏和手感的试玩反馈。
