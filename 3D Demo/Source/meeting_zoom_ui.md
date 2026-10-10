# ASTRA LINK：参考 Zoom 的会议界面

更新日期：2026-09-27。

## 调研依据

查看了 Zoom 官方的桌面端发言人视图、会议工具栏截图和参会者说明：

- [视频布局与发言人视图](https://support.zoom.com/hc/en/article?id=zm_kb&sysparm_article=KB0063672)
- [会议控制栏](https://support.zoom.com/hc/en/article?id=zm_kb&sysparm_article=KB0062674)
- [会议参会者面板](https://support.zoom.com/hc/en/article?id=zm_kb&sysparm_article=KB0065566)
- [官方发言人视图截图](https://assets.zoom.us/images/en-us/desktop/generic/in-meeting/video-layout-active-speaker.png)
- [官方底部工具栏截图](https://assets.zoom.us/images/en-us/desktop/generic/in-meeting/attendee-controls.png)

采用的结构是大幅主讲人画面、底部图标与文字工具栏、右上角视图菜单，以及按需展开的参会者和聊天面板。保留 ASTRA 终端的外层窗口与桌面风格。

## 这版界面

- 删除常驻右侧信息栏、独立标题信息块和固定字幕面板。会议应用也不再占用通用文件菜单与状态栏的高度。
- 演出画面按 16:9 占据窗口主体，使用绿色发言人边框与左下角姓名标记。
- 顶部仅保留会议时长、名称及视图菜单。视图菜单可显示或隐藏本地身份小窗。
- 底部提供参会者、聊天、字幕、暂停/继续和离开。麦克风与摄像头显示未接入状态，不采集设备。
- 参会者与聊天面板默认关闭，通过工具栏打开或关闭。聊天记录来自实际播放的台词和已选择的回复。
- 字幕浮在视频下方。需要回答时，出现包含原演出选项和倒计时的浮层；关闭字幕也不会隐藏必须回答的选项。
- 离开后返回导航并保留本次运行内的演出位置；真实 End 节点仍自动结算事件、关闭会议并返回导航。

## 实现位置

- `UnityProject/Assets/Astra/Scripts/Runtime/ConferenceModeView.cs`：界面、图标、字幕、侧栏和会话记录。
- `UnityProject/Assets/Astra/Scripts/Runtime/ConferenceMode.cs`：演出播放、独立渲染和生命周期。
- `UnityProject/Assets/Astra/Scripts/Runtime/CabinComputer.cs`：会议应用使用完整窗口内容区。
- `UnityProject/Assets/Astra/Scripts/Runtime/MeetingVerification.cs`：真实播放、交互和界面状态的验收。

## 验证与体验

使用 Unity 2022.3.62f1 构建 `BuildMeeting/Astra Cabin.exe`，通过 `RunMeetingDemo.cmd` 启动。在第一星域选择“中继视频会议”，点击跃迁进入。

本轮会议运行检查 29 项全部通过，0 失败、0 引擎错误，覆盖原有演出分支、沉默出口、存档失败重试、一次性结算、自动返回导航，以及新增侧栏、字幕开关和聊天记录。实际检查了 1600×900 与 1280×720 截图。

- 构建日志：`QA-Meeting/zoom-build.log`
- 运行报告：`QA-Meeting/ZoomUI/meeting-verification.txt`
- 主画面：`QA-Meeting/ZoomUI/02_Conference.png`
- 交互选项：`QA-Meeting/ZoomUI/03_Interactive_Choice.png`
- 720p 布局：`QA-Meeting/ZoomUI/04_Conference_720p.png`
- 参会者面板：`QA-Meeting/ZoomUI/08_Participants_Drawer.png`
- 聊天面板：`QA-Meeting/ZoomUI/09_Meeting_Chat.png`
- 关闭字幕：`QA-Meeting/ZoomUI/10_Captions_Off.png`
- 关闭字幕时的交互：`QA-Meeting/ZoomUI/11_Choice_With_Captions_Off.png`

此前集成轮次的导航 181 项和星图编辑器 44 项结果见 `performance_meeting_verification.md`；本轮界面更新重跑会议检查。
