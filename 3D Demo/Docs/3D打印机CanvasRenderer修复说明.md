# 打印机 CanvasRenderer 修复

2026-11-06。用户截图报告 Contour and fixed pen 缺失 CanvasRenderer。

修复：
- 创建图纸对象时，先显式添加 CanvasRenderer，再添加自定义 Graphic，确保 OnEnable 执行前依赖存在。
- 自定义 FabricatorContourGraphic 增加 RequireComponent(RectTransform, CanvasRenderer)。
- 将 FabricatorContourGraphic 拆入同名脚本文件并交付独立 meta，保留 CabinFabricatorScreen.cs 原 GUID，避免同文件多 MonoBehaviour 的重载识别问题。
- 没有改图纸玩法、实体屏幕布局、存档或 Editor 工具。

实际工程源码已安装。正在 Play 的旧图纸对象不会因新增依赖声明自动补齐；退出 Play、等待脚本导入完成，再重新进入 Play，由修复后的创建流程生成屏幕。

验收文件：build2.log、player.log、checks.txt。隔离验收新增原生 CanvasRenderer 存在、实际网格顶点、禁用后重新启用、独立添加组件自动补齐依赖四项检查。

修改前 CabinFabricatorScreen.cs 备份见本目录 .before 文件。正式 Build 未替换。

最终结果：Unity 原生构建成功；17 项运行检查全部 PASS，包含四项本次专项验证；player.log 未检出 Exception/Error，图纸绘制截图已检查。

