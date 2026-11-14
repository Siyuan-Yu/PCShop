# NPC 美术更新与交接（2026-11-14）

本篇补充 [116：当前全部游戏内容](116-current-game-content-2026-11-14.md)。全部经营、生活、城市、人物、金融、赛车、钓鱼、旧物市场及随机事件继续以 116 和其中链接的专项文档为准；本轮新增的是全体头像更新，以及待选的 3D 人物风格方案。

## 已完成并接入：全体 NPC 头像

- 采用用户选择的 **03 现代恋爱游戏立绘风**，共 **124 位固定 NPC＋1 张动态人物备用图**。保留性别、年龄及角色差异，背景简洁，姿态自然。温音保留用户确认的长双马尾地雷系形象。
- 125 张独立 PNG 与 Unity 导入配置位于 `Assets/Resources/PortraitsAnime`。5 张复用用户确认的样张，120 张分别生成。源图保留，运行时导入最大边 512、压缩、关闭 mipmap 与 CPU 读写。
- 人物资料、对话及共用头像入口的商店、中介、KOL 界面优先读取新资源；未知人物使用同风格备用图。历史头像仍保留回退。
- [美术规范与维护说明](art/anime-npc-portraits.md)；[全部人物、生成提示词、来源与哈希](art/npc-anime-portraits.json)。

## 风格探索档案

历史预览保留用于对照，不代表全部采用：

| 目录 | 内容与结论 |
| --- | --- |
| [首轮十种风格](previews/portrait-style-study/README.md) | 最初画风比较；之后继续探索更明显的二维动漫风格 |
| [08 初版五人](previews/portraits-anime-08/README.md) | 场景及姿态较复杂，后来调整 |
| [简背景与类型区分](previews/portraits-anime-08-v3/README.md) | 区分人物气质；温音短发版弃用，长双马尾版留作备用 |
| [第二轮十种风格](previews/wenyin-anime-style-study/README.md) | 提出 03 与 05 等二维方向 |
| [五人 03／05 对比](previews/five-npcs-style-03-05/README.md) | 用户最终确认 03，作为全体头像方向 |
| [全体图册与游戏截图](previews/anime-npc-portraits/) | 七页图册、11 张资料截图、1 张实际对话截图、运行时人物清单 |

各探索目录保留原始提示词，正式采用资源以 `PortraitsAnime` 和逐人清单为准。

## 验证与交付范围

- 源文件审核：125 张、125 个独立图像哈希、125 个独立 GUID，无错误；运行时 124 位固定人物的 ID、姓名、年龄与清单一致。
- 头像专项与旧头像兼容测试合计 **6／6 通过**，并非宣称全部游戏测试通过。
- 隔离 Unity 工程实际图形运行 **code=0、errors=0**。资料窗口逐个断言确实显示目标新头像；完成开局做饭教学后重新拍摄资料及对话界面。
- 全体固定头像纹理对象合计约 **23.33 MiB**；这不是全游戏内存。
- 用户已收到陈启明、韩征、李秀兰、温音、莫文宁的游戏截图及一页男女头像对照图。
- [完整验证结果](validation/anime-npc-portraits-results.md)、[测试 XML](validation/anime-npc-portraits-tests.xml)、[图形记录](validation/anime-npc-portraits-graphics.txt)、[源文件审核](validation/anime-npc-portraits-source-audit.json)。

本轮上传 Unity 源工程、资源、探索档案、工具、文档与验证材料，没有重新制作 Windows／WebGL 发行包。

## 待选：场景人物 3D 风格

用户要求先讨论，不直接制作接入。已核查当前几何体小人、服装分类和动作实现，形成 [十种 3D 风格与推荐组合](art/3d-npc-style-proposal.md)。推荐以半 Q 版日系生活卡通为造型基础，配柔和赛璐璐明暗；此项仅为建议，尚未确认、生成或替换任何模型。

下一步由用户选择方向，再做独立人物样例，比较正侧背面、站立行走及当前游戏镜头下的效果，确认之后才扩展全体人物。
