# 资源出处

本版复用 Demo/V2 已下载的 Quaternius 开源资源，并新增 Universal Base Characters 免费 Standard 包，不含付费 Pro 素材，也不含 Mouthwashing 或 Arctic Eggs 的资产。

- [Ultimate Modular Characters](https://quaternius.com/packs/ultimatemodularcharacters.html)：实际源文件 `Source/Downloads/Humans_Master_Humanoid.blend`；SHA-256 `1c4bd98437d0611be72498e8caf402adbd6f11002b244b35e8d9cc22e6a541bd`。源许可证 `Modular_License.txt`，CC0 1.0。
- [Universal Animation Library](https://quaternius.com/packs/universalanimationlibrary.html)：使用免费 Standard 包 `UAL1_Standard.fbx`，本地实际为 42 动作 + 1 T Pose。原下载 ZIP SHA-256 `cc73fc4e495b82958207316596317a3f40b9fa38065bde1027937452da537724`；源许可证 `Animation_License.txt`，CC0 1.0。
- 作者与来源：Quaternius。原 CC0 模型允许修改和商业使用；本任务保留来源以便追溯。外星人、半兽人、新增配件及本版形态键是衍生修改，不能标成作者原包的成品。
- 4 个原 Demo/V2 FBX 来自该版本的衍生角色；模块化 FBX 由本版 Blender 脚本从公开源 Blend 重建。
- ASTRA Shader、场景与工具代码沿用或修改用户项目内容，不因模型采用 CC0 而将用户项目整体改为 CC0。
- Windows 字体通过系统字体 API 加载，没有将系统字体文件打包；Unity 运行库遵循自身许可。

CC0 原文：[Creative Commons Zero 1.0](https://creativecommons.org/publicdomain/zero/1.0/)。


## Universal Base Characters（2026-09-25 新增）

- 来源：[Quaternius 官方资源页](https://quaternius.com/packs/universalbasecharacters.html)、[作者 itch.io 下载页](https://quaternius.itch.io/universal-base-characters)。使用公开免费的 Standard 下载，没有购买或包含付费 Source 版。
- 原始包：`Source/Downloads/UniversalBaseCharacters_Standard.zip`，128,968,391 字节；SHA-256 `fdbf1804c90dfc1ea03e992bff7da2dfd1a79318e13270a660180f9308455f40`。
- 包内许可证：`Source/Downloads/UniversalBaseCharacters/Universal Base Characters[Standard]/License_Standard.txt`，明确标注 CC0 1.0 Universal、作者 Quaternius。
- 使用 `Hairstyles/Rigged to Head Bone/FBX (Unity)` 中 Hair_Long、Hair_Buns、Hair_SimpleParted、Hair_Buzzed、Hair_BuzzedFemale 五个 FBX。头发朝向、大小、长度、绑定和形态键适配由本项目修改；原始发束拓扑保留，映射与面数见 `Source/ubc_hair_manifest.json`。
- 本次外星人新头型 / 颅冠为本项目自行制作的几何，游戏参考仅用于形态设计调研。没有引入 No Man’s Sky、Halo 或 Stellaris 的模型和贴图。

## 太空服装衍生修改（2026-09-25）

ID 10–21 的 36 个身体 / 裤装 / 鞋靴模块由既有 Quaternius CC0 原件重塑并加入原创几何，来源表见 `QA/space-outfits-source.json`，生产脚本见 `Tools/space_outfits.py`。这些不是 Quaternius 原包中的官方套装。Starfield、Star Citizen、Elite Dangerous 仅用于用途与设计调研；本次未下载或打包其模型、贴图、商标或服装资源。原始 ZIP 保留本地并在 Git 中忽略，许可证和重建所需原始模型继续追踪。


## Ultimate Modular Women（2026-09-27 新增）

- 作者：[Quaternius 官方资源页](https://quaternius.com/packs/ultimatemodularwomen.html)，页面明确列出 10 个模块化女性角色、24 个源动画、Humanoid 版本及 CC0 许可。
- 原始 glTF：`Source/Downloads/UltimateModularWomen/`，包含 Adventurer、Casual、Formal、Medieval、Punk、SciFi、Soldier、Suit、Witch、Worker，以及随包 HowToUse.txt、License.txt。
- 官方 Google Drive 在下载时返回配额超限。本次从[公开素材镜像](https://github.com/agentkaerf/FreeModels/tree/db3df04d1e4714298a09510b26fb6de6645138a2/Ultimate%20Modular%20Women%20-%20April%202022)取得同名包的自包含 glTF，固定提交 `db3df04d1e4714298a09510b26fb6de6645138a2`。逐文件 URL、大小、SHA-256 存于 `provenance.json`。未执行镜像仓库代码。
- 随包 License.txt 的标题写成 Ultimate Modular Males，原文原样保留；女性包的 CC0 许可同时以作者官方资源页为依据。
- 本项目修改：源网格骨骼权重映射与静置姿态适配、补全后脑、发型贴合、ASTRA 配色、受限形态键、面饰和 UV。未替换现有 UAL 动作，未使用此包的源动画。衍生结果不冒称作者原包。
