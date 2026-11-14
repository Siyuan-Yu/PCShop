# NPC 头像统一为 03 现代恋爱游戏立绘风

2026-11-14。用户选定第二轮样张中的 03 画风，要求重画全体 NPC 并直接接入游戏。

## 美术约定

- 清楚的二维动漫五官、细致线条、适量头发高光与赛璐璐阴影，避免写实皮肤、复古纸纹和棕色排线。
- 简洁浅色背景，不摆放复杂场景或职业道具；人物采用自然站姿或坐姿。
- 女性角色区分温柔、甜美、成熟、清冷、运动、知性等气质；保留各自年龄、身份、脸型与发型，避免同脸。
- 男性、老人和儿童保持对应性别和年龄特征，统一绘制语言；儿童单独采用适龄的日常动漫描述。
- 温音采用用户确认的长双马尾、黑色蝴蝶结和黑粉地雷系穿搭。

## 范围与来源

当前名单为 124 位固定人物，加 1 张动态人物备用图，共 125 张。包含旧清单之后新增的 4 位车友：许星澜、沈知夏、陆景行、顾若宁。陆栀、顾宁、温岚、宁汐、温音沿用用户刚确认方向的 03 样张，其余人物分别生成。

新资源为 `Assets/Resources/PortraitsAnime/{PersonId}.png`，旧资源保留。完整逐人提示词、来源、完成状态、尺寸与哈希见 [npc-anime-portraits.json](npc-anime-portraits.json)。所有新图使用内置 image_gen 生成，导入脚本仅复制图像，不改变像素。

## 接入与验收

已完成125张资源并接入。头像专项6/6通过，全体124位固定NPC资源可独立加载，最终图形验收code=0/errors=0。已检查七页全体图册与真实人物界面；首次截图被开局做饭教学拦截，因此补充独立验收档的真实做饭、吃饭、打包流程后重新截图，并增加人物头像确实显示的断言。[最终验证记录](../validation/anime-npc-portraits-results.md)

1. `GameUi.PortraitOf` 优先读取 `PortraitsAnime`，资料、对话、商店人物卡、中介与 KOL 头像共用此入口。未知动态人物使用同画风 `default`。
2. 贴图最大边保持 512，关闭 mipmap 与 CPU 读写，启用压缩，保留高分辨率 PNG 原图。
3. `AnimePortraitTests` 验证当前人物的独立资源、缓存、回退与导入设置。历史复古资源检查保留兼容性。
4. `GameSceneBuilder.AnimePortraitsFromBatch` 在隔离 Unity 工程运行，生成全体图册与真实资料/对话界面截图。

维护导入命令：`python tools/import-anime-npc-portrait.py --id PERSON_ID --source GENERATED_PNG`。脚本以稳定 ID 更新独立资源和生成清单；不得用一个通用人物覆盖所有固定 NPC。

## 图册与游戏截图

- 全体图册：[1](../previews/anime-npc-portraits/catalogue-01.png) · [2](../previews/anime-npc-portraits/catalogue-02.png) · [3](../previews/anime-npc-portraits/catalogue-03.png) · [4](../previews/anime-npc-portraits/catalogue-04.png) · [5](../previews/anime-npc-portraits/catalogue-05.png) · [6](../previews/anime-npc-portraits/catalogue-06.png) · [7：车友](../previews/anime-npc-portraits/catalogue-07.png)。
- 资料：[陈启明](../previews/anime-npc-portraits/profile-keeper_agent.png)、[韩征](../previews/anime-npc-portraits/profile-resident_hanzheng.png)、[李秀兰](../previews/anime-npc-portraits/profile-landlady.png)、[陆栀](../previews/anime-npc-portraits/profile-life_creator.png)、[温音](../previews/anime-npc-portraits/profile-life_singer.png)。
- 对话：[莫文宁](../previews/anime-npc-portraits/dialogue-momo.png)。
