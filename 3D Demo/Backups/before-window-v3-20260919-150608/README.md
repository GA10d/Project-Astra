# ASTRA · CABIN 04 — 四面墙飞船舱室 Demo

根据项目 A/B/C/D 四面墙概念图制作的全 3D、固定中央视角、苏联潜艇风格原型。场景不是把四张图片贴在墙上：舱壁、管道、仪表、工具、柜子、窗框和控制器均为真实网格；窗外是独立的程序化 3D 星空与缓慢旋转的行星。

## 直接游玩

打开 `Build/Astra Cabin.exe`。分发时必须连同整个 `Build` 文件夹一起发送，不要只发 exe。

| 操作 | 功能 |
| --- | --- |
| Q / E | 向左 / 右切换至相邻墙面 |
| 移动鼠标 | 同方向小幅观察，鼠标仍可自由指向物品 |
| 鼠标左键 | 操作黄色轮廓高亮的部件 |
| M | 静音 / 恢复 |
| Esc | 暂停、继续或退出 |

玩家固定在舱室中央，不能走动。转墙期间不接受交互，机械动画期间重复点击会被忽略。

## 交互位置

- A 指令终端：照明开关、红色主电源拉杆。主电关闭后 CRT 熄灭，红色应急灯仍保留。
- B 制造与工具：打印机启动按钮，按下回弹，启动后打印头往返运动。
- C 生活物资：收音机旋钮与储物柜门。
- D 观察与交换：下方交换舱门、右侧空气循环阀轮。

## 可编辑源文件

- `UnityProject`：Unity **2022.3.62f1**、Built-in Render Pipeline 工程，无付费插件、无在线 API 依赖。
- `UnityProject/Assets/Astra/Scenes/Cabin04.unity`：游戏主场景，打开后点 Play。
- `Source/Astra_Cabin.blend`：Blender **5.2.2 LTS** 可编辑源文件，四面墙分 Collection，交互部件独立命名，附四个观察相机。
- `Source/build_cabin.py`：可重复运行的模型生成与 FBX 导出脚本。
- `Source/model_manifest.json`：尺寸、坐标、材质、交互节点与导出统计。
- `UnityProject/Assets/Astra/Models/Astra_Cabin.fbx`：游戏导入模型，静态零件按墙面/材质合批，活动部件保留独立转轴。
- `UnityProject/Assets/Astra/Materials`：可直接在 Unity 修改的材质。
- `UnityProject/Assets/Astra/Scripts/Runtime`：交互、镜头、系统、音效、动态星空代码。
- `Source/Audio`：9 个原创合成 WAV；`Source/export_audio.py` 可重导出。运行时也能独立合成，无需联网。
- `QA`：验收报告与实际玩家截图。

Unity 菜单 `Astra > Rebuild cabin scene` 会重新生成本原型主场景。若已手工修改场景，请先另存副本；重建会覆盖 `Cabin04.unity`。菜单 `Astra > Build Windows demo` 会重新生成场景并打包到 `Build`。

## 重建

在 PowerShell 执行 `Rebuild.ps1`。默认只重建 Unity 场景与 exe；加 `-RegenerateModel` 会先运行 Blender 重新生成源模型和 FBX。手改生成结果后再次重建会覆盖这些生成结果，请先另存副本。

## 范围

这是一个可玩的风格化原型，保留参考图的设备布局、铆接舱壁、灰绿钢板、黄铜表头、红色机械手柄和暖色罩灯，不是概念图逐像素还原。旧化材质、CRT 显示、星空与音效均由本项目程序生成，不依赖静态太空背景图。没有使用 Tripo 生成额度。

当前交互是演示性系统，不包含完整生存玩法、存档、真实飞船动力学或真正的 3D 打印模拟。
