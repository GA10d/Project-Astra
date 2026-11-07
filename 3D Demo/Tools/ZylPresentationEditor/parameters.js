window.ZYL_PARAMETERS={
  "groups": [
    "10 · 撞击与维修",
    "11 · 传送门时序",
    "11 · 行星与运动",
    "12 · 来客与通话",
    "12 · 巨兽逼近与动画",
    "12 · 通话画面与照明",
    "胃内 · 加载与淡入",
    "13 · 泰德野里_肚中会谈",
    "米霍尔 · 皮下倒计时",
    "14 · 泰德野里_米霍尔需要打印构件",
    "15 · 泰德野里_都是傻瓜 · 上坡与镜头",
    "16 · 泰德野里_逃出生天",
    "17 · 泰德野里_别离"
  ],
  "fields": [
    {
      "group": "10 · 撞击与维修",
      "key": "repairAlarmDelay",
      "label": "二次撞击与报警延迟",
      "value": 3,
      "min": 0,
      "max": 15,
      "unit": "秒",
      "desc": "进入演出10后开始断电报警的时间。"
    },
    {
      "group": "10 · 撞击与维修",
      "key": "repairStrikeDuration",
      "label": "单次维修挥锤时长",
      "value": 0.48,
      "min": 0.2,
      "max": 2,
      "unit": "秒",
      "desc": "每处裂痕的维修动画时长；三处都必须修好。"
    },
    {
      "group": "10 · 撞击与维修",
      "key": "repairImpactStrength",
      "label": "二次撞击晃动强度",
      "value": 0.95,
      "min": 0,
      "max": 1,
      "unit": "比例",
      "desc": "只影响演出10二次撞击的镜头晃动。"
    },
    {
      "group": "10 · 撞击与维修",
      "key": "repairImpactDuration",
      "label": "二次撞击晃动时长",
      "value": 1.35,
      "min": 0.05,
      "max": 5,
      "unit": "秒",
      "desc": "不影响引擎堵塞持久状态。"
    },
    {
      "group": "11 · 传送门时序",
      "key": "portalApproachDuration",
      "label": "接近门体时长",
      "value": 20,
      "min": 5,
      "max": 60,
      "unit": "秒",
      "desc": "从初始距离移动至门前的时间，也是吸入开始时刻。"
    },
    {
      "group": "11 · 传送门时序",
      "key": "portalInitialDistance",
      "label": "门体初始距离",
      "value": 38,
      "min": 10,
      "max": 150,
      "unit": "世界单位",
      "desc": "门体沿舷窗前方X轴的起始位置。"
    },
    {
      "group": "11 · 传送门时序",
      "key": "portalNearDistance",
      "label": "门前距离",
      "value": 2.8,
      "min": 2,
      "max": 8,
      "unit": "世界单位",
      "desc": "接近阶段结束时门体的位置。"
    },
    {
      "group": "11 · 传送门时序",
      "key": "portalInitialOpening",
      "label": "初始门体开度",
      "value": 0.2,
      "min": 0.05,
      "max": 1,
      "unit": "比例",
      "desc": "相对于原门体的初始缩放。"
    },
    {
      "group": "11 · 传送门时序",
      "key": "portalFinalOpening",
      "label": "展开后门体开度",
      "value": 1.65,
      "min": 1,
      "max": 4,
      "unit": "比例",
      "desc": "完成展开时的门体缩放。"
    },
    {
      "group": "11 · 传送门时序",
      "key": "portalOpenStart",
      "label": "开始展开时刻",
      "value": 5,
      "min": 0,
      "max": 30,
      "unit": "秒",
      "desc": "从点击查看开始计时。"
    },
    {
      "group": "11 · 传送门时序",
      "key": "portalOpenEnd",
      "label": "完成展开时刻",
      "value": 15,
      "min": 1,
      "max": 50,
      "unit": "秒",
      "desc": "必须早于开始吸入。"
    },
    {
      "group": "11 · 传送门时序",
      "key": "portalWarpStart",
      "label": "隧道出现时刻",
      "value": 21.5,
      "min": 5,
      "max": 70,
      "unit": "秒",
      "desc": "门与隧道从这里开始重叠。"
    },
    {
      "group": "11 · 传送门时序",
      "key": "portalGateEnd",
      "label": "门体消失时刻",
      "value": 23,
      "min": 6,
      "max": 75,
      "unit": "秒",
      "desc": "应晚于隧道出现，早于抵达。"
    },
    {
      "group": "11 · 传送门时序",
      "key": "portalArrival",
      "label": "抵达行星时刻",
      "value": 32,
      "min": 10,
      "max": 100,
      "unit": "秒",
      "desc": "停止隧道，显示目的地行星。"
    },
    {
      "group": "11 · 传送门时序",
      "key": "portalComplete",
      "label": "演出完成时刻",
      "value": 35,
      "min": 11,
      "max": 120,
      "unit": "秒",
      "desc": "需晚于抵达；导航仍要求campaign中duration也达到。"
    },
    {
      "group": "11 · 传送门时序",
      "key": "portalTunnelSpeed",
      "label": "隧道流动速度",
      "value": 24,
      "min": 1,
      "max": 80,
      "unit": "单位/秒",
      "desc": "影响隧道环运动。"
    },
    {
      "group": "11 · 传送门时序",
      "key": "portalShake",
      "label": "穿越晃动强度",
      "value": 0.18,
      "min": 0,
      "max": 1,
      "unit": "比例",
      "desc": "穿越阶段每帧维持的晃动。"
    },
    {
      "group": "11 · 行星与运动",
      "key": "planetDiameter0",
      "label": "近处红色行星直径",
      "value": 46,
      "min": 1,
      "max": 80,
      "unit": "世界单位",
      "desc": "保持原有位置，调整可见大小。"
    },
    {
      "group": "11 · 行星与运动",
      "key": "planetSpin0",
      "label": "近处红色行星自转速度",
      "value": 1.2,
      "min": 0,
      "max": 10,
      "unit": "度/秒",
      "desc": "抵达后持续自转；暂停或电脑开启时停止。"
    },
    {
      "group": "11 · 行星与运动",
      "key": "planetDiameter1",
      "label": "远处灰色行星直径",
      "value": 7,
      "min": 1,
      "max": 80,
      "unit": "世界单位",
      "desc": "保持原有位置，调整可见大小。"
    },
    {
      "group": "11 · 行星与运动",
      "key": "planetSpin1",
      "label": "远处灰色行星自转速度",
      "value": 0.45,
      "min": 0,
      "max": 10,
      "unit": "度/秒",
      "desc": "抵达后持续自转；暂停或电脑开启时停止。"
    },
    {
      "group": "11 · 行星与运动",
      "key": "planetDiameter2",
      "label": "最远褐色行星直径",
      "value": 4.5,
      "min": 1,
      "max": 80,
      "unit": "世界单位",
      "desc": "保持原有位置，调整可见大小。"
    },
    {
      "group": "11 · 行星与运动",
      "key": "planetSpin2",
      "label": "最远褐色行星自转速度",
      "value": 0.18,
      "min": 0,
      "max": 10,
      "unit": "度/秒",
      "desc": "抵达后持续自转；暂停或电脑开启时停止。"
    },
    {
      "group": "12 · 来客与通话",
      "key": "visitorArrivalDuration",
      "label": "来客飞船接近时长",
      "value": 4,
      "min": 1,
      "max": 15,
      "unit": "秒",
      "desc": "点击查看后侧入舷窗的平滑移动时间。"
    },
    {
      "group": "12 · 来客与通话",
      "key": "visitorInvitationTime",
      "label": "通话邀请出现时刻",
      "value": 5.2,
      "min": 1,
      "max": 30,
      "unit": "秒",
      "desc": "从点击查看开始，不能早于飞船接近完成。"
    },
    {
      "group": "12 · 来客与通话",
      "key": "actorBlendDuration",
      "label": "人物动作过渡时长",
      "value": 0.55,
      "min": 0.1,
      "max": 2,
      "unit": "秒",
      "desc": "讲话、解释、思考、疑惑之间的混合时间。"
    },
    {
      "group": "12 · 巨兽逼近与动画",
      "key": "beastScale",
      "label": "巨兽统一缩放",
      "value": 640,
      "min": 80,
      "max": 1000,
      "unit": "倍",
      "desc": "保留原嘴部比例；路径口部偏移按比例同步。"
    },
    {
      "group": "12 · 巨兽逼近与动画",
      "key": "initialClearance",
      "label": "初始口部前向距离",
      "value": 18000,
      "min": 1000,
      "max": 30000,
      "unit": "世界单位",
      "desc": "巨兽与米霍尔飞船之间的口部前向间距。"
    },
    {
      "group": "12 · 巨兽逼近与动画",
      "key": "finalClearance",
      "label": "逼近曲线终点距离",
      "value": 120,
      "min": 20,
      "max": 200,
      "unit": "世界单位",
      "desc": "必须小于吞入和加载阈值，否则可能永远无法转场。"
    },
    {
      "group": "12 · 巨兽逼近与动画",
      "key": "approachDuration",
      "label": "逼近曲线时长",
      "value": 44,
      "min": 15,
      "max": 120,
      "unit": "秒",
      "desc": "对数距离曲线的时间；含起步缓动，实际略长。"
    },
    {
      "group": "12 · 巨兽逼近与动画",
      "key": "mouthTriggerDistance",
      "label": "开始张嘴的三维距离",
      "value": 16500,
      "min": 1000,
      "max": 30000,
      "unit": "世界单位",
      "desc": "按巨兽根节点到米霍尔飞船的三维距离触发，与口部前向距离不是同一量。"
    },
    {
      "group": "12 · 巨兽逼近与动画",
      "key": "mouthPlaybackSpeed",
      "label": "张嘴动画播放速度",
      "value": 0.5,
      "min": 0.2,
      "max": 1,
      "unit": "倍速",
      "desc": "6秒素材默认播放12秒；完整张嘴后才能吞入。"
    },
    {
      "group": "12 · 巨兽逼近与动画",
      "key": "swallowDistance",
      "label": "开始吞入的口部距离",
      "value": 180,
      "min": 30,
      "max": 1000,
      "unit": "世界单位",
      "desc": "完整张嘴且口部锚点到飞船的X方向间距不大于此值时开始吞入运动。"
    },
    {
      "group": "12 · 巨兽逼近与动画",
      "key": "swallowDuration",
      "label": "吞入推进时长",
      "value": 8,
      "min": 1,
      "max": 20,
      "unit": "秒",
      "desc": "吞入动作的平滑推进时间；转场由加载距离独立触发。"
    },
    {
      "group": "12 · 巨兽逼近与动画",
      "key": "swallowAdvance",
      "label": "吞入推进距离",
      "value": 160,
      "min": 10,
      "max": 600,
      "unit": "世界单位",
      "desc": "吞入阶段沿X方向额外推进的距离。"
    },
    {
      "group": "12 · 巨兽逼近与动画",
      "key": "concernDistance",
      "label": "疑惑台词三维距离",
      "value": 3750,
      "min": 1500,
      "max": 10000,
      "unit": "世界单位",
      "desc": "已开始张嘴且三维根节点距离达到阈值后，强制打断任意自由对话或待选回复；不等待台词计时结束。"
    },
    {
      "group": "12 · 通话画面与照明",
      "key": "videoFov",
      "label": "通话镜头视野",
      "value": 72,
      "min": 40,
      "max": 100,
      "unit": "度",
      "desc": "固定16:9通话镜头的垂直视场角。"
    },
    {
      "group": "12 · 通话画面与照明",
      "key": "videoFarClip",
      "label": "通话镜头远裁剪",
      "value": 40000,
      "min": 20000,
      "max": 80000,
      "unit": "世界单位",
      "desc": "需要覆盖巨兽和通话专用星空。"
    },
    {
      "group": "12 · 通话画面与照明",
      "key": "starCount",
      "label": "通话星点数量",
      "value": 1400,
      "min": 100,
      "max": 4000,
      "unit": "个",
      "desc": "仅通话专用星空；整数。"
    },
    {
      "group": "12 · 通话画面与照明",
      "key": "starSize",
      "label": "通话星点大小",
      "value": 42,
      "min": 5,
      "max": 100,
      "unit": "世界单位",
      "desc": "星点在远处星空中的绘制尺寸。"
    },
    {
      "group": "12 · 通话画面与照明",
      "key": "coldLight",
      "label": "远方冷光强度",
      "value": 0.42,
      "min": 0,
      "max": 2,
      "unit": "强度",
      "desc": "越接近口部越弱。"
    },
    {
      "group": "12 · 通话画面与照明",
      "key": "mouthSearchLight",
      "label": "近处口腔探照强度",
      "value": 2.2,
      "min": 0.3,
      "max": 5,
      "unit": "强度",
      "desc": "随着靠近和张嘴逐步增强。"
    },
    {
      "group": "12 · 通话画面与照明",
      "key": "mouthBounceLight",
      "label": "口腔暖色反射强度",
      "value": 0.65,
      "min": 0,
      "max": 3,
      "unit": "强度",
      "desc": "同时受距离和张嘴程度影响。"
    },
    {
      "group": "胃内 · 加载与淡入",
      "key": "loadDistance",
      "label": "开始黑屏加载的口部距离",
      "value": 250,
      "min": 50,
      "max": 1500,
      "unit": "世界单位",
      "desc": "以采样动画后的Mouth_Center锚点与米霍尔飞船的X方向间距判断。达到阈值后全屏黑，下一渲染帧之后才启动加载。"
    },
    {
      "group": "胃内 · 加载与淡入",
      "key": "blackHoldDuration",
      "label": "最短全黑停留",
      "value": 1,
      "min": 0.3,
      "max": 5,
      "unit": "秒",
      "desc": "从黑屏触发到允许结束的最短时间。加载更慢则继续保持全黑。"
    },
    {
      "group": "胃内 · 加载与淡入",
      "key": "fadeInDuration",
      "label": "胃内环境淡入时长",
      "value": 2,
      "min": 0.5,
      "max": 8,
      "unit": "秒",
      "desc": "胃内环境准备、通话和电脑关闭、朝向舷窗墙之后揭开黑幕。"
    },
    {
      "group": "12 · 来客与通话",
      "key": "greetingText",
      "label": "米霍尔 · 接通问候",
      "value": "你好，我是米霍尔。",
      "type": "text",
      "maxLength": 300,
      "unit": "台词",
      "desc": "接通通话后、等待玩家回复时显示。建议1–80字。"
    },
    {
      "group": "12 · 来客与通话",
      "key": "playerReplyText",
      "label": "我 · 回复选项",
      "value": "你要做什么？",
      "type": "text",
      "maxLength": 160,
      "unit": "台词",
      "desc": "玩家点击的回复按钮文字，同时记入通话记录；点击后仍按原流程开始巨兽逼近。建议1–40字。"
    },
    {
      "group": "12 · 来客与通话",
      "key": "concernText",
      "label": "米霍尔 · 远处疑惑（强制打断）",
      "value": "你怎么了？你看起来好像不太好",
      "type": "text",
      "maxLength": 1000,
      "unit": "台词",
      "desc": "巨兽达到疑惑距离条件时立即打断自由对话，取消待选回复并显示本句；直到加载转场。"
    },
    {
      "group": "13 · 泰德野里_肚中会谈",
      "key": "stomachWaterHold",
      "label": "转舱前水面观察停留",
      "value": 1.5,
      "min": 0,
      "max": 10,
      "unit": "秒",
      "desc": "从用户现有停靠锚点朝向开始。此项仅控制停留时间，不修改初始朝向。"
    },
    {
      "group": "13 · 泰德野里_肚中会谈",
      "key": "stomachTurnDuration",
      "label": "太空舱转向米霍尔时长",
      "value": 6,
      "min": 1,
      "max": 30,
      "unit": "秒",
      "desc": "从初始水面朝向平滑转向米霍尔位置锚点。米霍尔的位置和朝向在Unity场景中编辑。"
    },
    {
      "group": "米霍尔 · 皮下倒计时",
      "key": "countdownEnabled",
      "label": "显示皮下效果（0关／1开）",
      "value": 1,
      "min": 0,
      "max": 1,
      "unit": "开关",
      "desc": "关闭时恢复原皮肤材质显示，不停止计时逻辑。"
    },
    {
      "group": "米霍尔 · 皮下倒计时",
      "key": "countdownInitialSeconds",
      "label": "初始剩余时间",
      "value": 300,
      "min": 0,
      "max": 5999,
      "unit": "时:分:秒",
      "desc": "首次创建航程倒计时时使用，续玩保留存档时间。",
      "type": "time"
    },
    {
      "group": "米霍尔 · 皮下倒计时",
      "key": "countdownInitiallyVisible",
      "label": "初始可见（0否／1是）",
      "value": 0,
      "min": 0,
      "max": 1,
      "unit": "开关",
      "desc": "默认初遇阶段隐藏；肚中会谈开始时显示。"
    },
    {
      "group": "米霍尔 · 皮下倒计时",
      "key": "countdownStartOnTalk",
      "label": "肚中会谈自动显示并启动（0否／1是）",
      "value": 1,
      "min": 0,
      "max": 1,
      "unit": "开关",
      "desc": "本航程第一次进入会谈时启动；重入不重置，已发生操作不重复。"
    },
    {
      "group": "米霍尔 · 皮下倒计时",
      "key": "countdownWarningSeconds",
      "label": "进入警告颜色的剩余时间",
      "value": 30,
      "min": 0,
      "max": 5999,
      "unit": "时:分:秒",
      "desc": "剩余时间低于此值时使用警告色。",
      "type": "time"
    },
    {
      "group": "米霍尔 · 皮下倒计时",
      "key": "countdownBrightness",
      "label": "皮下发光强度",
      "value": 2.5,
      "min": 0,
      "max": 8,
      "unit": "强度",
      "desc": "同时影响数字与皮下散光。建议1–4。"
    },
    {
      "group": "米霍尔 · 皮下倒计时",
      "key": "countdownHalo",
      "label": "皮下散光柔和度",
      "value": 0.08,
      "min": 0.01,
      "max": 0.3,
      "unit": "比例",
      "desc": "越大边缘散光越明显。建议0.04–0.12。"
    },
    {
      "group": "米霍尔 · 皮下倒计时",
      "key": "countdownWidth",
      "label": "数字显示宽度",
      "value": 0.12,
      "min": 0.04,
      "max": 0.2,
      "unit": "模型单位",
      "desc": "限定在原胸口破口内，建议0.10–0.14。"
    },
    {
      "group": "米霍尔 · 皮下倒计时",
      "key": "countdownHeight",
      "label": "数字显示高度",
      "value": 0.032,
      "min": 0.012,
      "max": 0.07,
      "unit": "模型单位",
      "desc": "建议0.025–0.04。"
    },
    {
      "group": "米霍尔 · 皮下倒计时",
      "key": "countdownOffsetX",
      "label": "胸口数字水平偏移",
      "value": 0,
      "min": -0.05,
      "max": 0.05,
      "unit": "模型单位",
      "desc": "只移动数字，不移动角色或破口。"
    },
    {
      "group": "米霍尔 · 皮下倒计时",
      "key": "countdownOffsetY",
      "label": "胸口数字上下偏移",
      "value": 0,
      "min": -0.05,
      "max": 0.05,
      "unit": "模型单位",
      "desc": "只移动数字，不移动角色或破口。"
    },
    {
      "group": "米霍尔 · 皮下倒计时",
      "key": "countdownColor",
      "label": "正常发光颜色",
      "value": "#FF4D2E",
      "type": "color",
      "unit": "颜色",
      "desc": "六位十六进制RGB颜色。",
      "maxLength": 7
    },
    {
      "group": "米霍尔 · 皮下倒计时",
      "key": "countdownWarningColor",
      "label": "警告发光颜色",
      "value": "#FF160B",
      "type": "color",
      "unit": "颜色",
      "desc": "六位十六进制RGB颜色。",
      "maxLength": 7
    },
    {
      "group": "15 · 泰德野里_都是傻瓜 · 上坡与镜头",
      "key": "foolsStopHold",
      "label": "STOP 停留时长",
      "value": 4.5,
      "min": 1,
      "max": 30,
      "unit": "秒",
      "desc": "菌群完全组成STOP后停留，再开始回到裂口。"
    },
    {
      "group": "15 · 泰德野里_都是傻瓜 · 上坡与镜头",
      "key": "foolsReturnDuration",
      "label": "菌群飞回时长",
      "value": 4,
      "min": 1,
      "max": 15,
      "unit": "秒",
      "desc": "从STOP字形飞回原胃壁裂口，结束后切到已离船的米霍尔。"
    },
    {
      "group": "15 · 泰德野里_都是傻瓜 · 上坡与镜头",
      "key": "foolsWalkSpeed",
      "label": "米霍尔上坡速度",
      "value": 0.85,
      "min": 0.2,
      "max": 2,
      "unit": "单位/秒",
      "desc": "按真实坡面路径长度推进；与上坡台词时长相互独立，到坡顶立即打断。"
    },
    {
      "group": "15 · 泰德野里_都是傻瓜 · 上坡与镜头",
      "key": "foolsFrontHold",
      "label": "坡顶正面特写停留",
      "value": 2,
      "min": 0.5,
      "max": 15,
      "unit": "秒",
      "desc": "坡顶新台词开始时切正面特写，停留后开始绕头。"
    },
    {
      "group": "15 · 泰德野里_都是傻瓜 · 上坡与镜头",
      "key": "foolsOrbitDuration",
      "label": "正面绕至后脑时长",
      "value": 7,
      "min": 2,
      "max": 30,
      "unit": "秒",
      "desc": "正面到后脑的半圈旋转时长；转到约90度时同时开始镜头后拉。"
    },
    {
      "group": "15 · 泰德野里_都是傻瓜 · 上坡与镜头",
      "key": "foolsPullbackDuration",
      "label": "绕头后半程拉开时长",
      "value": 5,
      "min": 1,
      "max": 20,
      "unit": "秒",
      "desc": "从半圈旋转的中点（约90度）开始计时，与剩余旋转重叠；若更长，则旋转结束后补齐剩余后拉时间。"
    },
    {
      "group": "15 · 泰德野里_都是傻瓜 · 上坡与镜头",
      "key": "foolsFinalHold",
      "label": "最终背影画面停留",
      "value": 3,
      "min": 1,
      "max": 30,
      "unit": "秒",
      "desc": "拉开完成后至少停留此时长；坡顶台词尚未播完则继续等待。"
    },
    {
      "group": "15 · 泰德野里_都是傻瓜 · 上坡与镜头",
      "key": "foolsSceneFadeDuration",
      "label": "转入最后对白 · 渐黑/显现时长",
      "value": 1.2,
      "min": 0.2,
      "max": 5,
      "unit": "秒",
      "desc": "上坡结尾先渐黑，全黑实际绘制后切到新站位，再以相同时长显现。"
    },
    {
      "group": "15 · 泰德野里_都是傻瓜 · 上坡与镜头",
      "key": "foolsSettlementFadeDuration",
      "label": "傻瓜结算 · 渐黑/显现时长",
      "value": 1,
      "min": 0.2,
      "max": 5,
      "unit": "秒",
      "desc": "手动结算时先渐黑，全黑后关闭导航并回到舱内面向舷窗，再显现。"
    },
    {
      "group": "16 · 泰德野里_逃出生天",
      "key": "escapeFlightDuration",
      "label": "牵引逃亡时长",
      "value": 58,
      "min": 24,
      "max": 180,
      "unit": "秒",
      "desc": "从进入战斗画面到抵达出口。玩家不能操纵，米霍尔沿既定路线领航。"
    },
    {
      "group": "16 · 泰德野里_逃出生天",
      "key": "escapeExitDistance",
      "label": "初始出口距离",
      "value": 12000,
      "min": 1000,
      "max": 100000,
      "unit": "米",
      "desc": "随航程递减到零，仅用于演出显示。"
    },
    {
      "group": "16 · 泰德野里_逃出生天",
      "key": "escapeHullLoss",
      "label": "米霍尔累计损伤",
      "value": 36,
      "min": 5,
      "max": 70,
      "unit": "点",
      "desc": "由几次实际开路撞击分摊，最低保留船体，不触发普通战斗结算。"
    },
    {
      "group": "16 · 泰德野里_逃出生天",
      "key": "escapeFadeDuration",
      "label": "黑屏淡入淡出时长",
      "value": 1.2,
      "min": 0.2,
      "max": 5,
      "unit": "秒",
      "desc": "墓前转战斗、战斗退出时每次渐黑或显现的时长。"
    },
    {
      "group": "17 · 泰德野里_别离",
      "key": "farewellChestLeadTime",
      "label": "胸口爆炸提前量",
      "value": 0.45,
      "min": 0,
      "max": 3,
      "unit": "秒",
      "desc": "倒计时还剩多少秒时，胸口内的能量开始扩张。0 表示归零时开始。台词仍在归零时打断。"
    },
    {
      "group": "17 · 泰德野里_别离",
      "key": "farewellChestDuration",
      "label": "胸口能量扩张",
      "value": 2.8,
      "min": 0.5,
      "max": 10,
      "unit": "秒",
      "desc": "倒计时归零后，胸口能量继续扩张到信号切断的时长。"
    },
    {
      "group": "17 · 泰德野里_别离",
      "key": "farewellSignalHold",
      "label": "通话中断停留",
      "value": 1.3,
      "min": 0.5,
      "max": 8,
      "unit": "秒",
      "desc": "显示连接中断后自动关闭电脑并转向舷窗。"
    },
    {
      "group": "17 · 泰德野里_别离",
      "key": "farewellPlanetDuration",
      "label": "星球能量扩张",
      "value": 8,
      "min": 3,
      "max": 30,
      "unit": "秒",
      "desc": "从视频信号中断开始计时。若设得比转向和近窗过程更短，会延长至近窗后仍能看到扩张与强闪。"
    },
    {
      "group": "17 · 泰德野里_别离",
      "key": "farewellDebrisHold",
      "label": "碎石停留",
      "value": 4,
      "min": 1,
      "max": 20,
      "unit": "秒",
      "desc": "先观察星球碎裂，再让黑白照片飘过。"
    },
    {
      "group": "17 · 泰德野里_别离",
      "key": "farewellPhotoDuration",
      "label": "黑白照片飘过",
      "value": 8,
      "min": 3,
      "max": 30,
      "unit": "秒",
      "desc": "照片横向飘过舷窗，结束返回导航，手动结算。"
    },
    {
      "group": "17 · 泰德野里_别离",
      "key": "farewellShake",
      "label": "碎裂峰值震动强度",
      "value": 0.35,
      "min": 0,
      "max": 1,
      "unit": "强度",
      "desc": "控制整段震动的最大幅度。碎石显现时达到峰值，0 关闭。"
    },
    {
      "group": "17 · 泰德野里_别离",
      "key": "farewellSurfaceWaveAmplitude",
      "label": "球面起伏幅度",
      "value": 1.63,
      "min": 0,
      "max": 6,
      "unit": "%",
      "desc": "胸口和行星能量球的凸起与凹陷，按各层球体半径的百分比计算。0 让球面保持光滑，明暗波纹仍会流动。"
    },
    {
      "group": "17 · 泰德野里_别离",
      "key": "farewellInnerWaveHz",
      "label": "球面起伏频率",
      "value": 1.3,
      "min": 0.1,
      "max": 6,
      "unit": "Hz",
      "desc": "控制胸口和行星能量球的球面起伏与明暗波纹速度。单位为 Hz，值越大，起伏越快。"
    },
    {
      "group": "17 · 泰德野里_别离",
      "key": "farewellHaloWaveHz",
      "label": "外光晕波动频率",
      "value": 0.55,
      "min": 0.1,
      "max": 6,
      "unit": "Hz",
      "desc": "连续外光晕独立起伏，默认比内圈慢。"
    },
    {
      "group": "17 · 泰德野里_别离",
      "key": "farewellHaloIntensity",
      "label": "外光晕亮度",
      "value": 1.05,
      "min": 0,
      "max": 1.5,
      "unit": "强度",
      "desc": "同时控制胸口和行星光球外侧的柔亮光晕。"
    },
    {
      "group": "17 · 泰德野里_别离",
      "key": "farewellFractureStart",
      "label": "开始解体的时机",
      "value": 0.64,
      "min": 0.58,
      "max": 0.82,
      "unit": "进度",
      "desc": "按行星扩张时长的比例计算。0.64 表示扩张到 64% 时在光球下开始碎裂。"
    },
    {
      "group": "17 · 泰德野里_别离",
      "key": "farewellFinalFlashDuration",
      "label": "末段核心强闪时长",
      "value": 0.7,
      "min": 0.1,
      "max": 2,
      "unit": "秒",
      "desc": "扩张结束前白亮核心增强的时间。若超出解体后剩余时长，则从解体时开始。"
    },
    {
      "group": "17 · 泰德野里_别离",
      "key": "farewellFinalFlashIntensity",
      "label": "末段核心强闪强度",
      "value": 1,
      "min": 0.2,
      "max": 1,
      "unit": "强度",
      "desc": "影响末段白亮核心的范围和亮度。"
    },
    {
      "group": "17 · 泰德野里_别离",
      "key": "farewellShakeSustain",
      "label": "持续震动比例",
      "value": 0.42,
      "min": 0.1,
      "max": 0.85,
      "unit": "比例",
      "desc": "归零到近窗期间占峰值震动的比例，星球扩张阶段继续增强。"
    },
    {
      "group": "17 · 泰德野里_别离",
      "key": "farewellImpactDecay",
      "label": "碎裂峰值震动衰减",
      "value": 1.4,
      "min": 0.15,
      "max": 3,
      "unit": "秒",
      "desc": "碎石显现时达到峰值，随后在该时间内平滑回落。"
    }
  ],
  "defaults": {
    "schemaVersion": 1,
    "repairAlarmDelay": 3,
    "repairStrikeDuration": 0.48,
    "repairImpactStrength": 0.95,
    "repairImpactDuration": 1.35,
    "portalApproachDuration": 20,
    "portalInitialDistance": 38,
    "portalNearDistance": 2.8,
    "portalInitialOpening": 0.2,
    "portalFinalOpening": 1.65,
    "portalOpenStart": 5,
    "portalOpenEnd": 15,
    "portalWarpStart": 21.5,
    "portalGateEnd": 23,
    "portalArrival": 32,
    "portalComplete": 35,
    "portalTunnelSpeed": 24,
    "portalShake": 0.18,
    "planetDiameter0": 46,
    "planetSpin0": 1.2,
    "planetDiameter1": 7,
    "planetSpin1": 0.45,
    "planetDiameter2": 4.5,
    "planetSpin2": 0.18,
    "visitorArrivalDuration": 4,
    "visitorInvitationTime": 5.2,
    "actorBlendDuration": 0.55,
    "beastScale": 640,
    "initialClearance": 18000,
    "finalClearance": 120,
    "approachDuration": 44,
    "mouthTriggerDistance": 16500,
    "mouthPlaybackSpeed": 0.5,
    "swallowDistance": 180,
    "swallowDuration": 8,
    "swallowAdvance": 160,
    "concernDistance": 3750,
    "videoFov": 72,
    "videoFarClip": 40000,
    "starCount": 1400,
    "starSize": 42,
    "coldLight": 0.42,
    "mouthSearchLight": 2.2,
    "mouthBounceLight": 0.65,
    "loadDistance": 250,
    "blackHoldDuration": 1,
    "fadeInDuration": 2,
    "greetingText": "你好，我是米霍尔。",
    "playerReplyText": "你要做什么？",
    "concernText": "你怎么了？你看起来好像不太好",
    "freeDialogue": [
      {
        "text": "我想……我可能要请你离开这里。我没有恶意，但是这件事说起来十分复杂，",
        "duration": 10,
        "replies": []
      },
      {
        "text": "我需要想想这件事应该从哪说起？也许应该从十年前，我居住的地方非常糟糕……",
        "duration": 600,
        "replies": []
      }
    ],
    "stomachWaterHold": 1.5,
    "stomachTurnDuration": 6,
    "stomachDialogue": [
      {
        "text": "我们被吞进它的肚子里了。",
        "duration": 5,
        "replies": [
          "这里到底是什么地方？"
        ]
      },
      {
        "text": "先别慌，我们得想办法出去。",
        "duration": 6,
        "replies": [
          "先看看周围吧。"
        ]
      }
    ],
    "countdownEnabled": 1,
    "countdownInitialSeconds": 300,
    "countdownInitiallyVisible": 0,
    "countdownStartOnTalk": 1,
    "countdownWarningSeconds": 30,
    "countdownBrightness": 2.5,
    "countdownHalo": 0.08,
    "countdownWidth": 0.12,
    "countdownHeight": 0.032,
    "countdownOffsetX": 0,
    "countdownOffsetY": 0,
    "countdownColor": "#FF4D2E",
    "countdownWarningColor": "#FF160B",
    "printingDialogue": [
      {
        "text": "【占位台词，请在HTML中填写米霍尔的正式对白。】",
        "duration": 5,
        "replies": []
      }
    ],
    "foolsStopHold": 4.5,
    "foolsReturnDuration": 4,
    "foolsWalkSpeed": 0.85,
    "foolsFrontHold": 2,
    "foolsOrbitDuration": 7,
    "foolsPullbackDuration": 5,
    "foolsFinalHold": 3,
    "foolsGroundDialogue": [
      {
        "text": "【占位：请填写米霍尔离开飞船后的台词。】",
        "duration": 4,
        "replies": []
      }
    ],
    "foolsWalkDialogue": [
      {
        "text": "【占位：请填写米霍尔边上坡边说的台词；到达坡顶后会被打断。】",
        "duration": 600,
        "replies": []
      }
    ],
    "foolsCrestDialogue": [
      {
        "text": "【占位：请填写到达坡顶时打断旧对白的新台词。】",
        "duration": 5,
        "replies": []
      }
    ],
    "foolsSceneFadeDuration": 1.2,
    "foolsSettlementFadeDuration": 1,
    "foolsClosingDialogue": [
      {
        "text": "【占位：请在HTML填写黑屏转场后米霍尔的台词。】",
        "duration": 5,
        "replies": []
      }
    ],
    "escapeFlightDuration": 58,
    "escapeExitDistance": 12000,
    "escapeHullLoss": 36,
    "escapeFadeDuration": 1.2,
    "escapeGravesDialogue": [
      {
        "text": "【占位：请在HTML填写三座墓碑前米霍尔的台词。】",
        "duration": 6,
        "replies": []
      }
    ],
    "escapeEncouragement": [
      {
        "text": "抓紧，我来带路！",
        "duration": 8,
        "replies": []
      },
      {
        "text": "跟着我的航线，别担心！",
        "duration": 8,
        "replies": []
      },
      {
        "text": "前面有障碍，我来撞开！",
        "duration": 8,
        "replies": []
      },
      {
        "text": "还撑得住，我们继续！",
        "duration": 8,
        "replies": []
      },
      {
        "text": "就快到出口了，坚持住！",
        "duration": 8,
        "replies": []
      },
      {
        "text": "看到亮光了，我们能出去！",
        "duration": 8,
        "replies": []
      },
      {
        "text": "最后一段路，抓稳！",
        "duration": 8,
        "replies": []
      }
    ],
    "farewellChestDuration": 2.8,
    "farewellSignalHold": 1.3,
    "farewellPlanetDuration": 8,
    "farewellDebrisHold": 4,
    "farewellPhotoDuration": 8,
    "farewellShake": 0.35,
    "farewellDialogue": [
      {
        "text": "【占位：请填写别离视频通话的台词，可添加玩家回复和倒计时操作。倒计时归零将立即打断。】",
        "duration": 600,
        "replies": []
      }
    ],
    "farewellChestLeadTime": 0.45,
    "farewellSurfaceWaveAmplitude": 1.63,
    "farewellInnerWaveHz": 1.3,
    "farewellHaloWaveHz": 0.55,
    "farewellHaloIntensity": 1.05,
    "farewellFractureStart": 0.64,
    "farewellFinalFlashDuration": 0.7,
    "farewellFinalFlashIntensity": 1,
    "farewellShakeSustain": 0.42,
    "farewellImpactDecay": 1.4
  },
  "rules": [
    [
      "finalClearance",
      "swallowDistance"
    ],
    [
      "finalClearance",
      "loadDistance"
    ],
    [
      "swallowDistance",
      "loadDistance"
    ],
    [
      "loadDistance",
      "initialClearance"
    ],
    [
      "portalOpenStart",
      "portalOpenEnd"
    ],
    [
      "portalOpenEnd",
      "portalApproachDuration"
    ],
    [
      "portalApproachDuration",
      "portalWarpStart"
    ],
    [
      "portalWarpStart",
      "portalGateEnd"
    ],
    [
      "portalGateEnd",
      "portalArrival"
    ],
    [
      "portalArrival",
      "portalComplete"
    ],
    [
      "portalNearDistance",
      "portalInitialDistance"
    ],
    [
      "portalInitialOpening",
      "portalFinalOpening"
    ],
    [
      "visitorArrivalDuration",
      "visitorInvitationTime"
    ]
  ]
};
