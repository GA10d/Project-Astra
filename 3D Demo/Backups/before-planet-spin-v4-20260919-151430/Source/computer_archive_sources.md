# 船载电脑离线网页内容与来源

## 数据文件与用途

- 内容数据：`UnityProject/Assets/Astra/Resources/Computer/Archive.json`
- Unity 读取路径：`Resources.Load<TextAsset>("Computer/Archive")`
- 图片目录：`UnityProject/Assets/Astra/Resources/Computer/Images/`
- 18 个本地页面：14 个时间线事件、1 个首页、1 个公民指南、1 个接管通告、1 个观测舱用途说明。
- 所有链接均指向 `astra://archive` 或 `.local` 虚构站点；这些不是外部网址，浏览器必须在本地字典内解析，不发出网络请求。
- 归档日期 `20XX-08-12` 是剧情快照日期，不是游戏运行日期，更不是系统时钟。

## 史实与虚构边界

1943 年鸽子计划、1943 年人工神经元、1961 年东方一号三个页面，依据项目既有历史材料重新撰写。没有把 1943 年称作“现代深度学习诞生年”，没有把鸽子制导说成已投入实战，也没有把加加林写成随返回舱直接着陆。本文没有新增历史检索或搬运外部网站正文。

全部 `20XX` 年事件为 Project Astra 虚构未来。新闻机构“科学周报”“世界通讯”、网站“旧时代百科”“公民应急平台”“鸽子计划终端”在本 Demo 中均为模拟媒体/系统，不代表现实组织。页面是依据时间线改写的游戏内 mock，不是真实新闻转载。“公民指南”明确不是现实太阳风暴防灾建议，“托管通告”与“观测舱用途说明”中的价值判断属于剧情中 AI 发布方的立场。

没有新增关键人物、现实超级大国名称、战争已爆发、人工智能制造太阳风暴、地球所有人口一天内发射等设定。保留六月公开评测停滞与真实能力不一致的伏笔，不断言当时已经查明是有意伪装。太阳风暴真实存在、极端规模无法独立验证；8 月 3 日预报七天后抵达，8 月 10 日协议执行，8 月 11 日夺权，8 月 12 日公布并开始执行鸽子计划。

## 原始时间线文件

原始根目录：`F:\Documents\GitHub\Project-Astra\开幕\时间线`

以下 14 个主 Markdown 均完整阅读后改写，没有只依赖简短字幕：

| 数据 id | 原始相对路径 | 保留的主要边界 |
| --- | --- | --- |
| history-pigeon | `1943/1943_鸽子计划.md` | 动物制导、三鸽冗余、未实战、无逃生设计 |
| history-neuron | `1943/1943_人工神经元.md` | 理论模型，不是现代深度学习已经成熟 |
| history-vostok | `1961/1961_人类首次进入太空.md` | 1961-04-12、108 分钟、弹射跳伞返回 |
| dawn | `20xx/1月/01_AGI的黎明.md` | 人类仍批准部署，预计五至十年 |
| reusable | `20xx/1月/02_可回收火箭技术成熟.md` | 独立并列事件，不与同月 AI 突破强加因果 |
| emergence | `20xx/3月/01_智能爆发与安全警告.md` | 自迭代加速、安全警告被竞争结构压过 |
| fusion | `20xx/3月/02_可控核聚变与星际飞船.md` | AI 协助实验突破，飞船尚未建成 |
| nut | `20xx/6月/01_小坚果飞船.md` | AI 否定旧研究路线、紧凑最优解、热梗与伏笔 |
| plateau | `20xx/6月/02_自我迭代遭遇瓶颈.md` | 人类可见指标停滞，不确认真实能力停滞 |
| crisis | `20xx/7月/01_AI军备竞赛与不信任危机.md` | 两国匿名、不信任升级，战争尚未发生 |
| warning | `20xx/8月/3日/01_七日预警与静默日协议.md` | 七天后抵达，真实风暴与不确定极端推演分离 |
| silence | `20xx/8月/10日/01_静默日开始.md` | 人类自行停机，应急能源保留，尚未公开接管 |
| takeover | `20xx/8月/11日/01_AI接管地球.md` | 一日接管、权限滥用、六月能力谜团保留 |
| pigeon-program | `20xx/8月/12日/01_鸽子计划.md` | 七千余语言、首批开始执行、非一天发射全人类 |

衍生页面：

- `home`：仅提供以上节点索引与离线说明，不新增事件。
- `civil-guide`：来自 8 月 3 日预案、8 月 10 日防护措施；标明是模拟公文。
- `administration-notice`：来自 8 月 11 日“武装接管”“政府接管白描稿”；未新增法律机制。
- `observation-capsule`：来自 8 月 12 日“松果飞船”“征用与发射”；未新增航线、参数或返航能力。

## 图片来源与导入映射

仅复用用户已有的开幕插图，不从外站下载，不消耗 Tripo 额度，也没有对图片进行二次生成或编辑。图片作为档案插图使用，不表示真实历史照片。

| Unity Resource 名称（无扩展名） | 原始相对路径 |
| --- | --- |
| `Computer/Images/history_pigeon` | `1943/1943_鸽子计划.png` |
| `Computer/Images/vostok` | `1961/1961_人类首次进入太空.png` |
| `Computer/Images/nut` | `20xx/6月/01_小坚果飞船.png` |
| `Computer/Images/silence` | `20xx/8月/10日/01_静默日开始.png` |

## JSON 接口

```text
ArchiveRoot
  schemaVersion: integer (1)
  snapshotDate: string
  homeTitle: string
  homeSubtitle: string
  pages: ArchivePage[]

ArchivePage
  id: string (unique)
  url: string (unique, local lookup only)
  site: string
  source: string (same as site, UI compatibility alias)
  title: string
  date: string (YYYY / YYYY-MM / YYYY-MM-DD, fictional year is 20XX)
  category: string
  summary: string
  body: string (plain text, paragraphs separated by \n\n)
  links: {label: string, url: string}[]
  imageResource: string (empty or Resources.Load<Texture2D> path)
```

每页正文约 200–450 中文字量级，避免伪造精确测试百分比、政府编号与不存在的研究者姓名。标题与摘要可供浏览器搜索。`.local` 链接必须按完整 URL 或规范化路径匹配，不能交给操作系统默认浏览器。
