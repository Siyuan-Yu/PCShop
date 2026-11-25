# 美术/音频资源登记（AI 生成必填）

## 2026-11-25 — 四区3D手绘场景、两名人物与制作交接

- 当前资源已接正式运行时：`Assets/Resources/OldTown/` 保存人物、材质与纹理；`Assets/Art/OldTownShopStudy/` 和 `Assets/Art/OldTownStreetStudy/` 保存可编辑样板、网格及预制体。主角棕夹克青年与房东为本轮已接入的两个人物，不能视为全体NPC模型均已替换。
- [全部实装与资产入口](155-四阶段城镇美术与制作交接.md) · [风格与原始参考](art-direction/STYLE_GUIDE.md) · [制作底稿](art-direction/制作底稿) · [纹理提示词与运行时贴图](art-direction/四阶段实机对照/同机位说明.md#纹理制作记录)。底稿保存源图、几何输入、修形结果与历史导入工序；生成来源按这些实际记录追溯，不把概念原图标成实机。
- [四区实机对照](art-direction/四阶段实机对照)保存16个固定机位、13轮测量、改前/本轮原帧与复拍工具。画面结构差距、未转换的人物以及玩法测试欠账均在交接文档明确保留，本次只核对归档，未重新生成美术或运行验收。

## 2026-11-14 — 全体NPC的03动漫头像

- 内置 image_gen 制作125张独立头像：124位固定NPC与1张动态备用图。5张沿用本轮03风格样张，其余120张分别生成，保留性别、年龄和人物气质。
- 位置：`Assets/Resources/PortraitsAnime/`；512导入上限、无mipmap与CPU读写，压缩保留。原始高分辨率PNG保持生成像素，旧头像独立保留。
- [全体提示词、来源和哈希](art/npc-anime-portraits.json) · [风格与接入记录](art/anime-npc-portraits.md)。

## 2026-11-06 — 现有场景的主播与商品透明图层

- 使用内置 image_gen：15 张主播透明半身图和 3 张商品透明图集（6 类电脑配件、12 款模型、8 类服装外形）。参考现有正式 NPC 头像与 `KolStudio/*_demo.png` 的低多边形风格；未改动原三套直播背景。
- 位置：`Assets/Resources/KolStudio/Hosts/` 与 `Products/`。全部 PNG 保留生成源像素，`.meta` 保留透明，角色最大 1024、图集最大 2048，关闭 mipmap/CPU 可读。衣物运行时按商品颜色着色，图集以 UV 选择单件。
- [完整提示词及来源](art/kol-scene-layers-prompts.json)、[运行时接入与验证](75-existing-scenes-and-replaceable-showcase-art.md)。主播和商品已接入原直播间、展台正面及舞台屏幕。
- 同日此前生成的 9 张独立复古漫画场景未被采用，已按用户要求从工程删除，连同 Unity 元数据与空目录一并清理；仅保留[文字生成记录](art/brand-expo-live-prompts.json)，不再展示废弃图片。

## 2026-11-05 — 街坊直播品类布景

- 资源目录为 Unity 的 `Assets/Resources/KolStudio`。`computer`、`clothing`、`models` 各有 `studio` 和 `demo` 两张布景，合计六张 PNG。
- 这是11月5日的历史实现。当前直播沿用三张直播台背景，替换为上文的独立主播及商品图层；三张旧演示图保留作为风格参考，当前运行时不再用它们替代实际主播。
- `OfflinePortrait.shader` 用于直播大厅的离线头像。资源及目录的 Unity `.meta` 一并入库。

## 2026-11-04 — 复古生活漫画NPC头像

- `Assets/Art/PortraitsRetro/`（原 Resources/PortraitsRetro，已移出 Resources 不进游戏包）：120位正式NPC独立头像及1张default，均为内置image_gen生成；初版包含3张已选样本，其余逐人生成。用户后续要求提高颜值，6位已重画，其中5位成年女性仅莫文宁保留眼镜。没有把同一张图复用为多位正式人物。
- 源PNG为1086×1448，复制入项目时未改像素；Unity最大512压缩导入、关闭mipmap和CPU可读。原Unity引用GUID保留，运行时共用按需加载缓存，旧头像资源仍保留兼容。
- 提示词、外观及SHA-256：[清单](art/npc-retro-portraits.json)、[初版提示](art/npc-retro-style-prompt.txt)、[6位修订提示及来源](art/npc-retro-beauty-revision.json)。修订中的去眼镜通过内置图像工具编辑完成，无手工像素加工。
- [图册、实际界面与验收范围](art/retro-npc-portraits.md)。图册是原捕获时版本，最终6位外观以游戏同名资源为准。第三方字体许可和既有资源登记保持原记录。

| 文件 | 来源工具/套餐 | 生成日期 | 提示词/参考 | 人工修改 | 备注 |
|---|---|---|---|---|---|


## 2026-10-30 — 暖色微缩街区

本轮新增美术为项目内程序化制作：`ArtDirection.cs` 中的配色与倒角网格，`Mats.cs` 中的人物/停放车辆/招牌字，`Ui.cs` 中的九宫格圆角卡片，装机配件的 UI 图形。未下载参考游戏资源；既有立绘与字体来源不变。参考方向见 `06-art-and-rendering.md`。

同日追加：`FigureMotion.cs` 的程序关节动作、`AgencyStreetMap.cs` 的现有街景鸟瞰、`PlacementOutline.cs` 与自编 URP Shader 的装修轮廓。均由项目代码产生，没有新增 AI 位图或第三方模型、纹理、动画资源。`docs/previews/` 中的相关图片为 Unity 实际运行截图。


## 2026-11-01 — WebGL 字体与程序化资源归档

- `Assets/Resources/Fonts/NotoSansCJKsc-Regular.otf`：Noto CJK 项目的 Noto Sans CJK SC Regular；来自 `https://github.com/notofonts/noto-cjk/blob/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf`，用于 WebGL 中文显示。SIL Open Font License 1.1 全文随 `Assets/ThirdPartyNotices/NotoSansCJK-LICENSE.txt` 保存。不是 AI 生成资源。
- `Assets/Resources/RuntimeMaterials/`：项目自有 URP Lit/Unlit 引用材质，保留运行时需要的透明/发光变体；无新增第三方纹理或模型。
- `RiceCookerVisual.cs`：灯光、锅盖和蒸汽程序动画；`AgencyBuildingGlow.cs`：uGUI 建筑凸包高亮；`ShopFixtures.cs`：花束/盆栽、餐盒程序模型。均为本项目代码生成，没有复制参考游戏美术或新增 AI 位图。
- `Assets/Plugins/WebGL/PCShopStorage.jslib`：浏览器文件系统同步桥接代码。
- 此轮不生成新验收截图、不运行图形冒烟，也未重打最新 WebGL 包。历史 `docs/previews/` 文件继续作为原版本资料。
