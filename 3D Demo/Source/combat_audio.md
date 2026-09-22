# 战斗音乐

- 来源：用户在 2026-09-22 明确指定并授权复制的 F:\Documents\GitHub\Project-Astra\bgm.mp3。
- 原文件实为 MP4 容器，视频 H.264、音频 AAC 44.1kHz 双声道，约 4 分 10 秒；Unity 的 MP3 解码器拒绝导入。
- 已保留根目录原文件，并将逐字节副本归档为 Source/Audio/CombatBGM.original.mp4。使用本机现有 FFmpeg 提取音轨为 UnityProject/Assets/Astra/Resources/Computer/CombatMusic.wav（PCM 16-bit 44.1kHz 双声道），播放内容和长度保留；构建时再压缩为 Vorbis 流式资源。
- 播放：Streaming / Vorbis，循环，初始音量为零；战斗开始 2.5 秒 smoothstep 渐入。暂停/失焦/切应用/退出/结算时 0.65 秒淡出后暂停音轨，继续时恢复原播放位置。重开任务重置到开头。
- 满音量：0.46 × 现有主音量，保留战斗音效辨识度；M 静音同时控制音乐及战斗音效。
- 开火、命中、受击警报、推进等声音由本项目程序合成；不从参考游戏提取素材。
- 未独立查验用户音乐的外部分发许可，本文只记录来源，不作授权归属判断。
