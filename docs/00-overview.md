# PCShopSell（工作名）

电脑城创业故事：从商业街最偏的一间小电脑店起家，手装机赚第一桶金，靠行情、人脉和签约主播做大，最后建成自己的电脑乐园（园区）。

> 当前已实现内容以 [10-current-content.md](10-current-content.md) 和 [12-update-2026-10-31.md](12-update-2026-10-31.md) 为准，试玩与验证见 [07-vs1-acceptance.md](07-vs1-acceptance.md)，历次记录见 [08-current-status.md](08-current-status.md)。`02-design.md`、`03-decisions.md`、`04-3c-and-world.md`、`05-player-life.md` 保留设计目标与历史决策；它们不代表所有规划均已实现。美术当前方向见 `06-art-and-rendering.md` 开头，后续计划见 `01-roadmap.md`。

## 定位与差异化
- 开局在出租屋、现金 ¥10,000、尚无店铺；到中介看商业街鸟瞰图并租铺，自己装修、守店、收现金、手装机。最便宜的是街尾小铺，雇人后可离店经营；后期逐步扩展到分店和园区。
- 玩法核心：硬件价格受虚构宏观因子驱动（币圈热度、AI 需求、新品周期、库存缺货、政策），按"新闻 → 宏观指标 → 品类价格"可读，玩家据此决定进货/装机时机。
- 形态：3D 低多边形 + 固定斜俯视相机；世界层（一张连续大地图，街上走动、看到店铺与地产）+ 店内层（固定镜头，守店、装机）+ UI 面板（经营决策；弹出窗口自动暂停，店铺侧栏所有页签服从顶部暂停/继续）。
- 时间：一条连续世界时钟，1× 静止经营时一天约 5.6 分钟，7 天一周。目标采用 365/730/1095 天，当前日历每年 360 天，差异已列待修。

## 技术栈
- Unity 2022.3.6f1（`D:\UnityEditor\2022.3.6f1`），URP 14.0.8；当前界面使用 uGUI Text 与系统中文字体回退，TextMeshPro 中文字体资产仍列在路线待办中。
- Windows 先行，后接 Steamworks；手游以后再考虑，微信小游戏不做。
- 逻辑分层：
  - `Assets/Scripts/Core`：纯 C# 逻辑（价格模型、经济、存档数据），**不引用 UnityEngine**，可单元测试。
  - `Assets/Scripts/Game`：Unity 层（UI、场景、输入、存档读写）。
  - `Assets/Tests/EditMode`：核心规则、真实 JSON 和部分 Unity 动作/移动回归测试。
  - 当前地图配置位于 `Assets/Resources/Data`；商品、事件等部分数据仍在代码定义中，完整数据化属于后续工作。

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
  Resources/Data/    当前地图等运行时配置
  Art/               美术（AI 生成资源需在 docs/asset-log.md 登记）
  Tests/EditMode/    单元测试
docs/                设计与规划文档
```
