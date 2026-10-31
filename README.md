# PCShopSell（工作名）

电脑城创业故事：从商业街最偏的一间小电脑店起家，手装机赚第一桶金，靠行情、人脉和签约主播做大，最后建成自己的电脑乐园。3D 低多边形（固定斜俯视相机）模拟经营游戏（世界层 + 店内层 + 面板层 + 手操层），目标平台 Steam。

## 文档
- [`docs/13-employees-and-procurement.md`](docs/13-employees-and-procurement.md)：员工无固定职业、自由安排岗位与门店、次日跨店、进货清单、车辆数量与占用、底栏叫货车
- [`docs/12-update-2026-10-31.md`](docs/12-update-2026-10-31.md)：最新工程快照、全部系统范围、物流/存储/多店/暂停规则、441 项测试及待修问题
- [`docs/11-update-2026-10-30.md`](docs/11-update-2026-10-30.md)：此前更新记录、当时的交互规则、截图及验证方法
- [`docs/10-current-content.md`](docs/10-current-content.md)：当前已实现内容总览
- [`docs/00-overview.md`](docs/00-overview.md)：概述与目录
- [`docs/02-design.md`](docs/02-design.md)：设计文档（定位、竞品与差异化、成长曲线、系统、垂直切片）
- [`docs/03-decisions.md`](docs/03-decisions.md)：决策记录（已定、已否决、默认假设、待办）
- [`docs/04-3c-and-world.md`](docs/04-3c-and-world.md)：3C、世界层与地产设计
- [`docs/05-player-life.md`](docs/05-player-life.md)：玩家角色、生活模拟、目标与失败
- [`docs/06-art-and-rendering.md`](docs/06-art-and-rendering.md)：美术路线、光影方案、可替换性约定
- [`docs/01-roadmap.md`](docs/01-roadmap.md)：开发路线
- [`docs/07-vs1-acceptance.md`](docs/07-vs1-acceptance.md)：VS1 验收说明（怎么玩、验收清单、数值位置）
- [`docs/08-current-status.md`](docs/08-current-status.md)：当前状态（已完成、代码结构、怎么跑测试、已知问题、下一步）
- [`docs/09-campaign-and-balance.md`](docs/09-campaign-and-balance.md)：三年长线目标、结局、店面扩建、固定人物与交情、数值校准
- [`docs/asset-log.md`](docs/asset-log.md)：AI 美术资源登记

## 技术
- Unity 2022.3.6f1，URP 14.0.8（3D 低多边形）；当前 UI 为 uGUI Text 与系统中文字体回退，TextMeshPro 中文字体资产仍待完成
- `Assets/Scripts/Core`：纯 C# 逻辑（无 UnityEngine 依赖）
- `Assets/Scripts/Game`：Unity 层
- `Assets/Tests/EditMode`：单元测试

运行测试（命令行）：

```
Unity.exe -batchmode -nographics -projectPath <项目路径> -runTests -testPlatform EditMode -testResults <结果.xml>
```

编辑器开着工程时，用 `tools/run-tests.ps1`、`tools/run-smoke.ps1`（在旁边的镜像目录里跑，见 `docs/08-current-status.md`）。

## 状态
打开 `Assets/Scenes/Main.unity` 点 Play。当前包含单店经营流程、多家可进入的自营电脑店，以及直供账期、企业批单、委托门店、园区、创作者/MCN、私人关系、车辆与住房、物业及指数投资的首版规则。长期内容量与三年平衡仍需深化。

本轮已加入按阶段派发的当前任务、人物步行动作、暖色微缩街区美术、房产中介三维鸟瞰选址，以及保留家具原材质的绿/红发光轮廓。任务引导只说明操作，不提供一键前往；只有街道步行减速 50%，室内保持原速。2026-10-31 新增与调整包括多店配送、购物车、纸箱和三天临时存放、家具跨房间摆放，以及全部店铺侧栏统一服从暂停/继续、暂停时角色不能走动。随后新增统一员工管理、自由安排岗位与门店、次日跨店、员工采购、多人独占车辆与底栏“叫货车”。员工无固定职业，不擅长只影响效率，同店可即时换岗。本次 EditMode 测试 469/469 通过；员工专项图形冒烟 code=0 errors=0，原有全流程冒烟未重跑。最新规则与待修问题见[本轮更新记录](docs/12-update-2026-10-31.md)，历史截图见[此前更新](docs/11-update-2026-10-30.md)，试玩步骤见[验收说明](docs/07-vs1-acceptance.md)。

冒烟测试（需图形，不能加 `-nographics`）：

```
Unity.exe -batchmode -projectPath <项目路径> -executeMethod PCShopSell.EditorTools.GameSceneBuilder.SmokeFromBatch -logFile <日志>
```

专项验收同样可通过镜像运行：

```powershell
./tools/run-smoke.ps1 -Method PCShopSell.EditorTools.GameSceneBuilder.AgencyFromBatch
./tools/run-smoke.ps1 -Method PCShopSell.EditorTools.GameSceneBuilder.PlacementFromBatch
```

同一镜像内的测试和冒烟应串行运行。镜像、缓存及测试存档保留在本地，不提交到仓库。
