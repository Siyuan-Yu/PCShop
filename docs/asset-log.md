# 美术/音频资源登记（AI 生成必填）

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
