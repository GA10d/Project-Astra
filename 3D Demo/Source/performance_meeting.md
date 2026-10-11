# 演出树接入终端视频会议

## 直接体验

双击 [RunMeetingDemo.cmd](../RunMeetingDemo.cmd)。它使用当前 `Build/Astra Cabin.exe`，以 `--astra-meeting-demo` 启动并打开终端导航；先选择 **test** 星球，再在第一星域选择 **中继视频会议**，点击 **跃迁至此信标**。也可以普通启动 `Build/Astra Cabin.exe`，进入 A 墙终端的导航模式，按相同步骤体验。

流程：跃迁扣除一次航行消耗 → 自动打开 **ASTRA LINK** → 实时播放 Tool V2 的角色、动作、镜头、场景和字幕 → 通过按钮选择分支，或等待沉默出口 → 到达演出树 End 节点 → 自动保存结果与奖励、关闭会议、返回导航图。

示例有维修券条件选项、电源条件判断、人类/外星人两条回应分支，以及公开、封存、沉默三个结束节点。完成获得 3 数据。维修券是演出包内部变量，与航行燃料/补给分开。

该演示入口的航程保存在 `Application.persistentDataPath/NavigationMeetingDemo/`，与普通启动 `Build` 使用的正式航程分开。普通入口如果仍载入旧星域配置，可进入 test 星球后点击“导入星域配置”，校验通过后备份旧航程并启用新地图。

会议界面已按 Zoom 的发言人视图重新布局：默认大视频，参会者和聊天按需展开，字幕与选项浮在画面下方。调研来源、界面说明与本轮截图见 [meeting_zoom_ui.md](meeting_zoom_ui.md)。

## 编辑和同步自己的演出

现有会议演出包、运行时和依赖资源已包含在 `UnityProject` 中，直接运行或构建无需外部导入。在 `3D Demo` 运行 `Rebuild.ps1` 即可将当前工程构建到 `Build`。下面的 Tool V2 导出流程仅用于从外部创作工程更新或新增演出包。

1. 在 Tool V2 Unity 工程选择 `Assets/AstraToolV2/Samples/Meeting/RelayMeeting.asset`；用现有时间轴和 Open graph 编辑。示例只在首次缺失时创建，以后导出保留编辑。
2. 也可以使用自己已有的 `ToolPackage`。图需要有效入口、所有选项与沉默出口，以及 End 节点。角色、真实动画和五轨内容均随包导出。
3. 关闭相关 Unity 工程，在 `3D Demo` 运行：

   ```powershell
   .\SyncPerformance.ps1 -ToolRoot 'C:\Path\To\GameDev_Performance_System\Tool\V2' -Package 'Assets/AstraToolV2/Samples/Meeting/RelayMeeting.asset' -PerformanceId relay
   .\Rebuild.ps1
   ```

   将 `-ToolRoot` 替换为实际外部 Tool V2 目录，目录下应有其 `UnityProject`；该参数必须显式提供。同步脚本会调用 Tool V2 导出并复制资产、GUID 和运行时代码，随后 `Rebuild.ps1` 将当前舱室场景构建到 `Build`，保留手工场景修改。同步脚本可用 `-EditorPath` 指定 Unity，构建脚本对应使用 `-UnityEditor`。同步脚本的 `-Build` 选项仍可额外输出辅助 `BuildMeeting`，当前试玩入口使用 `Build`。修改外部内容后需要重新同步并构建；运行程序不读取另一个 Unity 编辑器的内存状态。
4. 星图编辑器中选择事件类型“视频会议 / Tool V2 演出树”，填演出包 ID（本例 `relay`），把事件关联到节点。演出内的交互选项在 Tool V2 编辑；星图里只配置一个无消耗的自动结算项及其航行资源奖励。
5. 将导出的 `campaign.json` 放到 `UnityProject/Assets/StreamingAssets/Navigation/<星系>/`，构建后在对应星球内导入；当前会议示例位于 test。会议使用 `Resources/Performances/<ID>.asset` 查找演出包。

Tool V2 另提供菜单 **Export selected package for cabin meeting**：选中 ToolPackage 后导出一个包含 Assets 的目录，可合并到接收工程。包 ID 默认取资产名的小写（仅英文、数字、短横线、下划线）。原编辑器和独立捏人程序仍正常使用。

## 运行与存档行为

- 暂停、切换应用、最小化、关闭会议窗口、退出终端、系统失焦均停止演出推进及选项倒计时。返回导航点击“返回会议 / 继续”恢复。
- 同一次程序运行内保留当前演出位置。关闭整个游戏后，待处理事件和已扣除的跃迁消耗保留；重新接入从树入口播放。当前不提供逐帧演出存档。
- 只有真实 End 节点可以触发自动结算。缺资源、断开的出口、循环超限或播放器异常保留待处理事件，不发奖励。存储失败时可重试保存结果。
- 结局节点 ID、奖励和事件完成作为单条航行指令原子保存并回放，避免完成与奖励之间出现半提交或重复领奖。不同结局当前使用同一航行奖励，结局 ID 分别保留在日志里。
- 本地模拟视频会议，不连接摄像头、麦克风或网络。字幕和选项来自演出树；示例没有录制配音。
- 会议相机使用 RenderTexture 和独立渲染层 30，会议灯光不照亮舱室。关闭后恢复舱室相机/灯光遮罩并释放渲染纹理。

## 代码入口

- `ToolPlayer.cs`：公开播放状态、字幕、选项和 Completed 事件；支持宿主驱动时间和隐藏自带 UI。
- `ToolV2MeetingExport.cs`：导出实际 ToolPackage 的依赖、运行时、动态加载的面部纹样与特效 Shader。
- `ConferenceMode.cs`：独立画面、暂停及生命周期管理。
- `ConferenceModeView.cs`：参考 Zoom 的发言人视图、底部工具栏、可展开参会者/聊天面板、浮动字幕与选项。
- `PerformanceEventHandler.cs` / `NavigationMode.cs`：导航事件与会议之间的连接。
- `NavigationEngine.cs`：`performance` 指令完成并结算事件。
- `CabinMeetingBuild.cs`：校验配置和演出包后构建当前舱室场景。
- `MeetingVerification.cs`：`--astra-meeting-qa "输出目录"` 使用隔离存档执行真实运行检查并截图。

依赖资源出处见 `PerformanceToolV2_NOTICES.md`。
