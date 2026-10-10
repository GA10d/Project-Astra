# Tool V2 终端会议验收

验收日期：2026-09-27。Unity 2022.3.62f1，Windows x64，实机 Direct3D 11。

- `SyncPerformance.ps1 -Build`：实际运行成功，完成 Tool V2 导出、资产同步及 BuildMeeting 构建。
- 会议运行检查：23 项 PASS，0 失败、0 引擎错误。
- 原导航系统回归：181 项 PASS，0 失败、0 引擎错误，包含 3 条完整航程、9 种原舱室事件、资源收支、存档恢复、导入失败保护和战斗入口。
- 星图编辑器：44 项 PASS，新增会议包 ID、自动结算项与非法路径验证。
- 720p/900p 实机截图已检查：选项完整可见，字幕可读，会议结束返回导航；任务栏无标题溢出。
- 两个工程中的 ToolPlayer.cs SHA-256 相同：`011453B8F1ADDA97B1D6A3D939A5411933D1057602E3C25D6E771B40A5F06DAF`。
- `git diff --check` 无空白错误。原舱室场景、原示例演出树和正式 Build 未覆盖。

## 会议覆盖项

- QA saves isolated
- Campaign and imported ToolPackage validate
- Warp opens meeting inside terminal
- Travel charged exactly once
- No early reward
- Switching apps freezes timeline
- Manual pause freezes timeline
- Exiting terminal retains unresolved meeting
- Unaffordable choice stays on current node
- Choice effects and condition branch use ToolPlayer
- Global choice is interactive
- Failed durable save retains meeting and no reward
- Completed result can retry without replay
- End closes software and returns navigation
- Completion reward committed once
- Private rendering cleaned up
- Completion and result survive reload
- Silence follows authored branch
- Alternate branch reaches final choice
- Global silence reaches End and settles
- Reload leaves interrupted meeting pending
- Broken graph cannot complete event
- Existing setpiece integration retained

## 产物

- 程序：`BuildMeeting/Astra Cabin.exe`
- 入口：`RunMeetingDemo.cmd`
- 导出日志：`QA-Meeting/export.log`
- 构建日志：`QA-Meeting/build.log`
- 会议报告：`QA-Meeting/meeting-verification.txt`
- 导航报告：`QA-Meeting/NavigationRegression/navigation-verification.txt`
- 截图：`QA-Meeting/01_Map.png` 至 `07_Alien_Branch.png`

未实现跨进程的演出中途快照；重新运行后可接入仍待处理的事件，从演出树入口重播。会议信号、字幕、角色与交互在本地运行，不包含真实网络通话或录制配音。


## Zoom 风格界面更新验收（同日后续轮次）

- 主讲人画面扩展、移除常驻信息栏、底部工具栏、按需展开参会者/聊天、浮动字幕与交互选项。
- `CabinMeetingBuild.Build` 重新构建成功；程序仍为 `BuildMeeting/Astra Cabin.exe`。
- 最终构建会议检查 **29 项 PASS，0 失败、0 引擎错误**；包含上面的 23 项及新增 6 项界面行为。
- 新增覆盖：默认无侧栏、参会者面板开关、实际演出台词进入聊天、字幕开关保留会话、关闭字幕仍可选择分支、选中回复进入聊天。
- 1600×900 / 1280×720 实机截图已检查；侧栏关闭按钮、字幕、选项和底部工具栏均可见。
- 本轮构建日志：`QA-Meeting/zoom-build.log`；报告与截图：`QA-Meeting/ZoomUI/`。
- 上述导航 181 项与星图编辑器 44 项是前一集成轮次的结果；本轮重跑会议检查。
- 调研来源和布局说明见 [meeting_zoom_ui.md](meeting_zoom_ui.md)。
