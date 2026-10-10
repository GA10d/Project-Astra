# 星系关卡开发交接

导航入口先显示 `test`、`zyl`、`gzw` 三颗星球。每颗星球对应一份独立的 Campaign；Campaign 内仍用原来的星域、分支信标和事件构成爬塔航程。点击 `test` 进入现有全部关卡，包括档案交接会议分支。`zyl`、`gzw` 尚未配置时显示待开发面板。

## Unity 开工步骤

在 Unity Hub 添加本交付目录下的 [UnityProject](../UnityProject/)，使用 Unity **2022.3.62f1** 打开。打开 `Assets/Astra/Scenes/Cabin04.unity` 并点 Play，进入 A 墙终端的导航模式，检查星系总览与 test 参考关卡。

日常开发保留当前场景。Unity 菜单 **Astra > Build current scene (development)**，或在 `3D Demo` 目录运行 `Rebuild.ps1`，会校验星系配置并输出到 `Build`；`VerifyGalaxies.ps1` 默认检查该试玩版。完整入口说明见 [项目 README](../README.md)。

## 两人分工与合并

| 开发者 | 主要工作目录（相对 UnityProject） | 交付内容 |
| --- | --- | --- |
| zyl | `Assets/StreamingAssets/Navigation/zyl/` | `campaign.json` 及该星系必要的资源、脚本和对应 `.meta` |
| gzw | `Assets/StreamingAssets/Navigation/gzw/` | `campaign.json` 及该星系必要的资源、脚本和对应 `.meta` |

各自从完整交付工程开始，分别使用 [星域编辑器](../Tools/StarMapEditor/README.md) 导出配置到自己的星系目录。没有配置时该星球显示待开放；放入有效配置后重开导航入口即可加载。只修改各自星系所需的文件；需要调整共享运行时、主场景或共享演出索引时，两人先统一变更。

脚本可分别放在 `Assets/Astra/Scripts/Galaxies/Zyl/` 与 `Assets/Astra/Scripts/Galaxies/Gzw/`，命名空间分别用 `AstraCabin.Galaxies.Zyl`、`AstraCabin.Galaxies.Gzw`。Unity 新建或导入资源后，将生成的 `.meta` 随资源一起交付；关卡配置和自有资源合并进同一工程即可接入相应星球。

## 两位开发者的交付范围

| 星系 | 独立交付文件（相对 UnityProject） | 当前状态 |
| --- | --- | --- |
| test | `Assets/StreamingAssets/Navigation/test/campaign.json` | 原有完整地图；根目录 `Navigation/campaign.json` 保留作兼容副本 |
| zyl | `Assets/StreamingAssets/Navigation/zyl/campaign.json` | 预留给 zyl 开发者，尚无关卡配置 |
| gzw | `Assets/StreamingAssets/Navigation/gzw/campaign.json` | 预留给 gzw 开发者，尚无关卡配置 |

两位开发者分别维护自己的目录，不需要改星系选择器、航行引擎或中心注册表。使用 [星域编辑器](../Tools/StarMapEditor/README.md) 编辑并导出一份有效的 `schemaVersion: 1` 配置，将导出的 `campaign.json` 放入自己的目录，重开导航入口即可接入对应星球。不要为了占位复制 test 的关卡。

在已经运行过该星系的游戏中，替换发布文件后需先进入对应星球，再点击“导入星域配置”。导入只替换当前星系的配置并重开其航程；已有配置与存档会备份，其他星系的进度不受影响。构建分发目录使用相同结构：`Astra Cabin_Data/StreamingAssets/Navigation/<星系>/campaign.json`。

## 数据边界

- 每颗星球独立加载、校验和运行一份 Campaign，事件不会跨星系查找。Campaign ID、星域 ID、事件 ID 等可以在不同星系重复。
- 同一 Campaign 内仍执行原有约束：资源、事件和星域 ID 各自唯一；同一星域内节点 ID 唯一；同一事件内选项 ID 唯一；引用、无环航路、可达性和数值校验不变。
- 沿用现有 `choice`、`setpiece`、`performance` 事件类型即可配置关卡。完整字段、数量限制和存档恢复规则见 [导航架构](navigation_architecture.md)。
- `test` 继续使用旧玩家目录下的 `Navigation/campaign.json` 和 `Navigation/voyage.json`，保留当前玩家进度。`zyl`、`gzw` 分别使用 `Navigation/zyl/`、`Navigation/gzw/` 下同名文件；这里的 Navigation 是玩家存档目录，不是 StreamingAssets。

## 新玩法与共同文件

若需要自定义脚本，建议放在各自的脚本目录（例如 `Assets/Astra/Scripts/Galaxies/Zyl/`、`Galaxies/Gzw/`），使用 `AstraCabin.Galaxies.Zyl` 或 `AstraCabin.Galaxies.Gzw` 命名空间及对应前缀，避免同名类冲突。

新增事件类型仍需协调 `EventRegistry`、序列化字段和编辑器校验；这属于共享运行时扩展，不是普通关卡接入步骤。`performance` 演出包的注册表/资源索引也是共享的，新增演出包时请两位开发者协商唯一包 ID（建议 `zyl_...`、`gzw_...`）及注册位置。配置里的事件 ID 可以跨星系重复，共享演出包 ID 则不应互相覆盖。

交付前用编辑器通过配置校验，再在自己的星球内检查进入、跃迁、事件结算、返回星系入口和退出续玩。两位开发者各自交付自己的 `campaign.json` 和必要的玩法资源即可。
