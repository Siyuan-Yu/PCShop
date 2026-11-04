# 美术/音频资源登记（AI 生成必填）

## 2026-11-04 — 复古生活漫画NPC头像

- `Assets/Resources/PortraitsRetro/`：120位正式NPC独立头像及1张default，均为内置image_gen生成；初版包含3张已选样本，其余逐人生成。用户后续要求提高颜值，6位已重画，其中5位成年女性仅莫文宁保留眼镜。没有把同一张图复用为多位正式人物。
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
