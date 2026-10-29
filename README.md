# PCShopSell（工作名）

电脑城创业故事：从商业街最偏的一间小电脑店起家，手装机赚第一桶金，靠行情、人脉和签约主播做大，最后建成自己的电脑乐园。3D 低多边形（固定斜俯视相机）模拟经营游戏（世界层 + 店内层 + 面板层 + 手操层），目标平台 Steam。

## 文档
- [`docs/00-overview.md`](docs/00-overview.md)：概述与目录
- [`docs/02-design.md`](docs/02-design.md)：设计文档（定位、竞品与差异化、成长曲线、系统、垂直切片）
- [`docs/03-decisions.md`](docs/03-decisions.md)：决策记录（已定、已否决、默认假设、待办）
- [`docs/04-3c-and-world.md`](docs/04-3c-and-world.md)：3C、世界层与地产设计
- [`docs/05-player-life.md`](docs/05-player-life.md)：玩家角色、生活模拟、目标与失败
- [`docs/06-art-and-rendering.md`](docs/06-art-and-rendering.md)：美术路线、光影方案、可替换性约定
- [`docs/01-roadmap.md`](docs/01-roadmap.md)：开发路线
- [`docs/07-vs1-acceptance.md`](docs/07-vs1-acceptance.md)：VS1 验收说明（怎么玩、验收清单、数值位置）
- [`docs/08-current-status.md`](docs/08-current-status.md)：当前状态（已完成、代码结构、怎么跑测试、已知问题、下一步）
- [`docs/asset-log.md`](docs/asset-log.md)：AI 美术资源登记

## 技术
- Unity 2022.3.6f1，URP 14.0.8（3D 低多边形），uGUI + TextMeshPro
- `Assets/Scripts/Core`：纯 C# 逻辑（无 UnityEngine 依赖）
- `Assets/Scripts/Game`：Unity 层
- `Assets/Tests/EditMode`：单元测试

运行测试（命令行）：

```
Unity.exe -batchmode -nographics -projectPath <项目路径> -runTests -testPlatform EditMode -testResults <结果.xml>
```

编辑器开着工程时，用 `tools/run-tests.ps1`、`tools/run-smoke.ps1`（在旁边的镜像目录里跑，见 `docs/08-current-status.md`）。

## 状态
VS1（一个月流程）可玩：打开 `Assets/Scenes/Main.unity` 点 Play。包含守店结账、6 类 × 3 档配件与库存容量、行情新闻、线下档口砍价、1788 网购、步行/打车/小电驴/三轮车/地铁、交警追逐、生活与晕倒住院、装机小游戏、雇人、房租破产、月目标、存读档。详见 [`docs/07-vs1-acceptance.md`](docs/07-vs1-acceptance.md)。

冒烟测试（需图形，不能加 `-nographics`）：

```
Unity.exe -batchmode -projectPath <项目路径> -executeMethod PCShopSell.EditorTools.GameSceneBuilder.SmokeFromBatch -logFile <日志>
```
