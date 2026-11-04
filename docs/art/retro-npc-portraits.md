# 复古生活漫画风NPC头像

> 2026-11-04后续归档：用户现已明确要求整理全部当前内容并上传GitHub，本套最终资源随[第59篇完整归档](../59-current-game-content-2026-11-04.md)一并提交。下文“只保留本地等待”是此前交付时的历史要求；本轮检查以[最新联合记录](../validation/59-current-archive-results.md)为准。

本轮又使用当前完整工程重跑Unity图形检查，`code=0/errors=0`，下方6张全体图册及8张人物资料/对话截图已刷新为最终资源版本，包含颜值调整和去眼镜修正。运行记录见[59-current-archive-graphics.txt](../validation/59-current-archive-graphics.txt)。下文初版制作时“尚未重跑界面”的表述保留为历史交付记录。

2026-11-04。用户选定复古生活漫画风，并要求全体NPC接入游戏；后续明确要求完成后保留本地、等待查看，不自行提交或推送GitHub。

## 后续颜值调整（2026-11-04）

用户反馈人物吸引力不足，尤其希望KOL具有更高颜值。已使用内置图像生成工具重画6位：莫文宁、乔雨桐、柯承安、陆栀、温岚、宁汐。其中5位为成年女性，柯承安保留男性身份并调整为帅气主播形象。保留复古纸感和棕色漫画线条，加强眼神、精致五官、发型及穿搭；没有修改人物身份或玩法。

本次已替换同名游戏资源，保留原`.meta`引用及512导入上限。6张均为1086×1448，已检查人物对应、PNG尺寸、源文件一致性及清单哈希；全体121张资源哈希仍各不相同。未重新运行Unity界面测试或打包。此前下方图册及测试结果对应初版，本次新版请查看以下原图。

- [莫文宁](../../Assets/Resources/PortraitsRetro/momo.png) · [乔雨桐](../../Assets/Resources/PortraitsRetro/qiao.png) · [柯承安](../../Assets/Resources/PortraitsRetro/ake.png)
- [陆栀](../../Assets/Resources/PortraitsRetro/life_creator.png) · [温岚](../../Assets/Resources/PortraitsRetro/life_designer.png) · [宁汐](../../Assets/Resources/PortraitsRetro/resident_ningxi.png)

本次完整提示及来源见[npc-retro-beauty-revision.json](npc-retro-beauty-revision.json)，其中乔雨桐、陆栀、宁汐另作去眼镜细节修正。旧图备份在本地`work/retro-portraits/before-beauty-revision/`。没有提交或推送GitHub，等待用户查看。

## 资源与人物

- 正式常驻NPC共120人，每人使用自己的独立头像，按原有稳定PersonId命名。
- 另有一张`default.png`，用于动态人物和编辑器验收人物的同画风备用显示。
- 初版沿用温岚、莫文宁、李秀兰三张复古样本；后续温岚、莫文宁已按上述反馈重画。其余人物依据年龄、职业、人物经历和分别撰写的外观简述，用内置图像生成工具逐张生成。
- 角色清单、外观简述、完成状态、原始输出文件名及SHA-256记录在[npc-retro-portraits.json](npc-retro-portraits.json)。统一风格提示见[npc-retro-style-prompt.txt](npc-retro-style-prompt.txt)。三张沿用样本单独标记`approvedSample`。
- 图像源文件位于`Assets/Resources/PortraitsRetro/{PersonId}.png`。原PNG像素保留，未通过裁图、拼图或重复一张图来代替独立NPC资产。
- 头像为竖版3:4胸像，浅暖纸色背景、深棕线条、低饱和色块与适量印刷网点。成人、老人和儿童采用对应年龄表现；脸型、发型、眼眉和服装按人区分。

## 游戏接入

`GameUi.PortraitOf`优先加载复古资源，供人物资料、统一对话、店内人物图、中介和关系图等入口共用。原有姓名、人物ID、立绘引用键和关系存档不变。已知固定人物全部有专属复古图；旧资源仅保留兼容回退。

未知动态人物使用`PortraitsRetro/default`，空ID也可安全处理；资源结果有缓存。每次开始运行及编辑器导入新头像后会清理查找缓存，避免继续显示之前的缺图结果。

头像导入规则：最大边512、关闭mipmap、关闭CPU读写、使用压缩、sRGB、双线性过滤与Clamp。高分辨率PNG只是源文件，游戏载入的是Unity导入后的贴图；没有新增每帧扫描全体头像的逻辑。

核对身份时修正两处早期占位年龄：退休街坊周国良67岁、大学生陆宇21岁。人物ID、关系和传记进度保持原记录。

## 本地维护

1. 图像生成使用内置工具，一人一个输出；不要用通用头像填满固定人物清单。
2. `tools/prepare-npc-portrait-manifest.py`准备外观清单，并保留已经完成的资源状态。
3. `tools/import-npc-portrait.py --id ID --source 输出PNG路径`只复制已生成的图、核对PNG格式并写入清单。它不调用图像API，也不修改图像像素。
4. Unity菜单`PCShop/Art/Reimport retro NPC portraits`可统一重设导入选项。
5. `GameSceneBuilder.RetroPortraitsFromBatch`生成120人的六页图册和实际人物界面截图；`RetroPortraitTests`验证专属资源、回退、缓存、导入尺寸和年龄。

## 验证记录

最终结果保存在`docs/validation/retro-npc-portraits-results.txt`，截图位于`docs/previews/retro-npc-portraits/`。验证使用专用隔离工程`D:/UnityProjects/PCShop/_ci_retro_portraits`，由`de37420`和本轮头像接入改动组成，没有把同时进行的MCN、采购和主线修改当作本次头像测试已验证的内容。

最终相关检查41/41通过，图形验收code=0/errors=0。已核对全部120人的六页图册，另保留7张人物资料和1张对话截图。121张PNG的SHA-256均一致、互不重复，源图比例均为3:4。

图形批次同时载入120张正式人物头像，`Profiler.GetRuntimeMemorySizeLong`合计约22.6 MiB。该值是贴图对象的运行时内存测量，不是全游戏内存，也不等同于单独显存；日常界面按需加载并缓存。

不读取玩家正式存档。本次不重新打Windows发行包；资源、源码、清单和验证材料均保留本地，等待用户查看。

## 完整图册

每页20位，按照实际人物清单顺序排列：

- [第1页：商户与街坊](../previews/retro-npc-portraits/catalogue-01.png)
- [第2页：熟人、档口与KOL](../previews/retro-npc-portraits/catalogue-02.png)
- [第3页：员工、创作者与行业人物](../previews/retro-npc-portraits/catalogue-03.png)
- [第4页：主线人物与居民](../previews/retro-npc-portraits/catalogue-04.png)
- [第5页：社区及花园生活圈](../previews/retro-npc-portraits/catalogue-05.png)
- [第6页：居民与生活服务](../previews/retro-npc-portraits/catalogue-06.png)

游戏界面：[人物资料](../previews/retro-npc-portraits/profile-keeper_agent.png)、[新补居民头像](../previews/retro-npc-portraits/profile-resident_hanzheng.png)、[实际对话](../previews/retro-npc-portraits/dialogue-momo.png)。
