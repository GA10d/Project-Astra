# zyl 星系开发目录

此目录预留给 zyl 开发者。当前没有 `campaign.json`，游戏显示待开发面板。

使用 `Tools/StarMapEditor` 导出有效的 `schemaVersion: 1` 配置，保存为本目录的 `campaign.json`，即可接入 zyl 星球，无需修改星系选择器或航行引擎。游戏导入只更新 zyl；玩家配置和航程独立存放在 `Navigation/zyl/`。

交付范围与共享代码约定见 [开发交接](../../../../../Source/galaxy_development.md)，编辑步骤见 [星域编辑器](../../../../../Tools/StarMapEditor/README.md)。
