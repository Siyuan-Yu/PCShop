# PCShopSell（工作名）

创业人生模拟：从"手工干活"的小生意（电脑装机、小吃摊等）起家，逐步雇人、扩张、跨行业，最终建成商业园区；贯穿人脉关系网、签约主播、行情判断。

> 最新设计见 `docs/02-design.md`，路线见 `docs/01-roadmap.md`，决策见 `docs/03-decisions.md`，美术与渲染见 `docs/06-art-and-rendering.md`。以下为早期概述，若有冲突以 02-design.md 为准。

（早期概述）电脑硬件代理商模拟经营游戏。核心是"读行情、囤货/出货/装机赚钱"，赚到钱后可扩张分店、开其他业态、请主播推广、与主播培养关系。

## 定位与差异化
- 视角：代理商/渠道（B2B 价格、库存、现金流博弈），不是"摆货架"零售店。
- 玩法核心：硬件价格受多因素驱动（币圈热度、AI 需求、新品周期、库存缺货、政策），玩家据此决定进货/装机时机。
- 形态：3D 低多边形 + 固定斜俯视相机；世界层（街上走动、看到店铺与地产）+ 店内层（固定镜头，装机等手操）+ UI 面板（经营决策，参考"东京股神"）。

## 技术栈
- Unity 2022.3.6f1（`D:\UnityEditor\2022.3.6f1`），uGUI。
- 3D（URP），Windows 先行，后接 Steamworks；手游以后再考虑，微信小游戏不做。
- 逻辑分层：
  - `Assets/Scripts/Core`：纯 C# 逻辑（价格模型、经济、存档数据），**不引用 UnityEngine**，可单元测试。
  - `Assets/Scripts/Game`：Unity 层（UI、场景、输入、存档读写）。
  - `Assets/Tests/EditMode`：针对 Core 的测试。
  - 数据配置放 `Assets/Data`（JSON/ScriptableObject），商品、事件、主播等尽量数据化。

## 合规原则（上架前必看）
- 不使用真实品牌、型号、Logo；全部虚构。
- 主播、恋爱对象全部原创，不影射真人（脸、名字、梗、声音）。
- AI 美术：商用条款留档；保存提示词/原图/工具/日期；统一风格并做人工修整；Steam 内容调查如实披露预生成 AI 内容。
- 不使用实时生成 AI 内容。
- 不照搬竞品（含东京股神）的 UI 布局、角色、文案。

## 目录
```
Assets/
  Scripts/Core/      纯逻辑（无 UnityEngine）
  Scripts/Game/      Unity 层
  Data/              配置数据
  Art/               美术（AI 生成资源需在 docs/asset-log.md 登记）
  Tests/EditMode/    单元测试
docs/                设计与规划文档
```
