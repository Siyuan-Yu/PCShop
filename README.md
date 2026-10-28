# PCShopSell（工作名）

电脑城创业故事：从电脑城的小摊位起家，手装机赚第一桶金，靠行情、人脉和签约主播做大，最后建成自己的电脑乐园。2D 模拟经营游戏（世界层 + 面板层 + 手操层），目标平台 Steam。

## 文档
- [`docs/00-overview.md`](docs/00-overview.md)：概述与目录
- [`docs/02-design.md`](docs/02-design.md)：设计文档（定位、竞品与差异化、成长曲线、系统、垂直切片）
- [`docs/03-decisions.md`](docs/03-decisions.md)：决策记录（已定、已否决、默认假设、待办）
- [`docs/04-3c-and-world.md`](docs/04-3c-and-world.md)：3C、世界层与地产设计
- [`docs/05-player-life.md`](docs/05-player-life.md)：玩家角色、生活模拟、目标与失败
- [`docs/06-art-and-rendering.md`](docs/06-art-and-rendering.md)：美术路线、光影方案、可替换性约定
- [`docs/01-roadmap.md`](docs/01-roadmap.md)：开发路线
- [`docs/asset-log.md`](docs/asset-log.md)：AI 美术资源登记

## 技术
- Unity 2022.3.6f1，uGUI，2D
- `Assets/Scripts/Core`：纯 C# 逻辑（无 UnityEngine 依赖）
- `Assets/Scripts/Game`：Unity 层
- `Assets/Tests/EditMode`：单元测试

运行测试（命令行）：

```
Unity.exe -batchmode -nographics -projectPath <项目路径> -runTests -testPlatform EditMode -testResults <结果.xml>
```

## 状态
M1 底座进行中：已完成 Money、日期/时刻/连续世界时钟、账本；世界层基础（路径图、A* 寻路、路径插值、移动耗时）已完成。
