# 可交互太空船：AstraPlayerShip

1. 把本目录的 `AstraPlayerShip.prefab` 拖入环境场景，摆好位置和旋转。
2. 根对象 `Usage` 保持 `PlayerShip`，根对象及父级缩放保持 `(1,1,1)`。
3. 按 Play，玩家直接从舱内中央开始。Q/E 转墙，鼠标点击设备和电脑，Esc 暂停/返回，点击观察窗凑近，右键退出观察。

完整包含原项目的九个交互点、船载电脑、导航、会议、独立战斗APP、制造机、电源/灯光/警报、音效、仪表及演出调度。无需手工逐个接事件引用。没有新增船外入口、自由行走或飞行驾驶控制。

- 不包含自带星空或行星；舷窗直接观察当前场景。数字0–9不会触发演出，也不显示这些快捷键提示。导航配置明确引用的剧情演出接口仍可用。
- 一个场景只使用一艘 `PlayerShip`；其他船设为 `Scenery`，只显示静态模型，不启用电脑、相机、声音和交互。缩放不为1的玩家船会报错并降为陈设。
- 不要在已有独立CabinController的可玩舱室场景中重复叠加此预制体。可从空场景或原静态环境场景开始。

菜单 `Astra > Ships`：
- `Place interactive player ship`：添加一艘船。
- `Replace selected static cabin`：把选中的静态Astra_Cabin模型替换为交互船，保留位置和旋转，支持Undo。
- `Validate ships in current scene`：检查玩家船数量、缩放及重复控制器。
- `Build reusable ship prefab`：从磁盘已保存的Cabin04读取临时副本，重新生成预制体；不修改、保存或关闭用户已打开的源场景。

现有航行/演出运行时依赖世界坐标。为保持这些能力，进入Play时组件会以船为原点，整体重定位飞船所在场景的根对象；同时处理方向、重力和肉壁shader坐标。停止Play后编辑场景恢复原摆放。此方案适用于当前单玩家固定视角原型，不支持运行中持续移动船根对象或多艘玩家船同时运行，也不会自动处理其他加法加载场景。

脚本接口 `AstraCabin.Ships.AstraShipRig`：
- `onReady`：原舱室完成首帧初始化后调用，可挂接关卡初始化。
- `AuthoredPointToRuntime` / `RuntimePointToAuthored`：转换原摆放坐标与运行坐标。
- `IAstraShipWorldRebaseAware.OnShipWorldRebased(Matrix4x4)`：外部系统需要调整自存的世界坐标时实现此接口；通知发生在常规Awake之前。场景Transform会自动移动，不应再次移动。

示例：`Assets/Astra/Scenes/ShipExamples/StomachShipPlacement.unity`。原始环境、原可玩场景、主场景与存档不因本预制体安装而改变。
