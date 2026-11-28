# 多轮复测、游戏体验与 Windows 交接

2026-11-28，`art/experiment`，Unity 2022.3.6f1。本轮基于 `c04d280b`，包含本地路障常驻开销修复、相关回归用例更新和性能采样工具改进。当前完整玩法、资源清理、人物和四区美术索引见[前一轮交接](165-current-handoff-and-resource-cleanup-2026-11-28.md)。

本轮覆盖的流程未复现阻断游玩的缺陷。最终功能回归 **407/407**，恢复及深夜用餐补测 **10/10**。正常镜头下的性能达到本轮顺畅目标。感情线下一步优先补交往后的续段，接管流程优先补招聘核准后的去向。

## 源码变更

- 路障创建时缓存告示牌 Transform。只有路障可见时才朝向镜头，显隐变化时才切换物体状态。区域放行判定继续由会话的 `DistrictRoadOpen` 驱动。[DistrictGateView.cs](../Assets/Scripts/Game/Runtime/DistrictGateView.cs)
- 更新门店周租、柜台营业额、员工对话、商品定价、统一网购、地段客流和交警追逐的测试夹具。周租检查房租的门店归属，营业额检查控件复用，网购跨正式送达分钟，交警从追逐时限内能够追上的位置开始。
- 赛车陪诊用正式路线推进玩家和伤者，到达后完成结算。断言医院是有效地点，玩家在医院外未收费，全场只记一笔 ¥1,500 的活动支出。
- Game View 采样新增可选时长和超过 16.67、33.33 毫秒的帧比例。本轮普通样本暖机 4 秒后采样至少 24 秒，睡眠样本持续到恢复结束。[巡检入口](../tools/qa/run-visual.ps1)

## 功能、读档和长期模拟

| 轮次 | 完成情况 | 核对范围 |
| --- | --- | --- |
| [01 近期改动](validation/2026-11-28-followup-and-windows/round-01/summary.json) | 107/107 | 头像导入、感情线及场所、公司接管、员工和性能回归 |
| [02 常用系统](validation/2026-11-28-followup-and-windows/round-02/summary.json) | 303/313 | 门店、公司、赛车、存档及工作台。最初的 10 条未通过均在最终复测通过 |
| [03 长期模拟](validation/2026-11-28-followup-and-windows/round-03/cases.jsonl) | 输出 7 个结果，6 个通过 | 公司账务 30 和 90 天通过，经济机器人三个至少 7 天的样本及一个至少 30 天的样本通过。365 天单例触及六分钟上限，整批在 720 秒上限结束，计划中的其余样本未跑完 |
| [04 定向与会话复核](validation/2026-11-28-followup-and-windows/round-04/summary.json) | 120/125 | 员工对话文案、网购、客流、交警和旧版地图约束各有一条待复核，前四条随后通过 |
| [05 最终回归](validation/2026-11-28-followup-and-windows/round-05/summary.json) | **407/407** | 门店、定价、员工、公司、地区解锁、开店教学、赛车、保存恢复及 365 天续跑 |
| [06 恢复及深夜活动](validation/2026-11-28-followup-and-windows/round-06/summary.json) | **10/10** | 睡眠、晕倒和住院分帧结算，与同步结算的存档对照，及 21:42、23:42、01:00 三个深夜用餐时点 |

365 天用例从首轮保存的第 301 天继续，核对到第 365 天的薪资、持股、时间、余额与净资产，并完成存读档对照。[续跑记录](validation/2026-11-28-followup-and-windows/round-05/resume-914-365-progress.txt)

各轮用例存在重叠。第 04 轮的旧地图断言仍要求起始商业街 18 间铺位，当前布局读到 29 间。该断言的设计基准和历史全量待复核项仍待逐条确认。完整新档通关和大档长线经营尚待体验验收。[旧地图用例](validation/2026-11-28-followup-and-windows/round-04/failures.json) · [历史全量记录](159-performance-and-open-issues-2026-11-27.md)

## 实机流程与体验

两次 Unity Play Mode 巡检均使用准备好的阶段状态，操作正式界面按钮并推进活动和路线，记录均无游戏运行错误。

| 流程 | 实测 | 下一步 |
| --- | --- | --- |
| 公司接管、前台和招聘 | 65 个检查点、21 张原帧。买股过半并确认次日交接，查看门店权限及个人门店，按道路到写字楼左侧前台。由「经营 → 公司」可找到写字楼、自营及参控股公司。前台查看门店、任命人事负责人、到任后核准招聘，次日从指定面试店聘用并安排岗位。同行公司在前台展示整体经营规模。 | 核准后保留招聘状态、到店时间和面试门店，在「经营 → 公司」增加「前往面试门店」，让玩家隔天回来能直接接上。 |
| 感情线 | 82 个检查点、11 张原帧。唐晓芸在同一市场的好友版和交往版使用不同对话，交往版延续此前的选择并展示专属 CG。陈志衡在同一市场围绕旧掌机展开自己的内容。温音在练歌房有专属曲谱 CG、接唱表情和次日回应，玩家可以继续做朋友。 | 先为唐晓芸和温音做第二、第三次约会。沿用上次选择，安排一次实际可做的后续，例如继续用选过的蓝布做挂件。现有专属片段完成后，再约主要会用到固定伴侣台词和牵手。 |
| 开店初期 | 正式新档参数的三个种子均以 ¥15,000 开始，城郊铺位签约 ¥5,000、周租 ¥2,000。付完签约和必需家具后，餐馆剩 ¥6,211，电脑店剩 ¥9,100，均尚需支付运输、进货和生活费。自动经营脚本有旧报表和采购策略，适合做长期状态压力采样。 | 从自然新档比较餐馆与电脑店的首单时间、首周最低现金和周租前的流动资金，再决定是否调整起步成本。 |

[接管和招聘检查表](validation/2026-11-28-followup-and-windows/gameplay/checks.txt) · [交接通知](validation/2026-11-28-followup-and-windows/gameplay/15-接管首日交接通知.png) · [写字楼前台](validation/2026-11-28-followup-and-windows/gameplay/18-前台查看门店与人事.png)

[感情线检查表](validation/2026-11-28-followup-and-windows/relationship/checks.txt) · [好友市场](validation/2026-11-28-followup-and-windows/relationship/02-唐晓芸好友市场.png) · [交往市场 CG](validation/2026-11-28-followup-and-windows/relationship/03-唐晓芸交往市场CG.png) · [温音接唱表情](validation/2026-11-28-followup-and-windows/relationship/10-温音接唱表情.png) · [开局预算样本](validation/2026-11-28-followup-and-windows/round-05/开局预算-7.tsv)

## 性能、材质与树冠

本轮使用 RTX 3070、1920×1080、Unity Game View 真实 PlayerLoop，关闭垂直同步和帧率上限。十段共采样 **37,263 帧**，都有实际绘制，未检测到后台固定节流。各 1× 样本推进 144 游戏分钟，花园 4× 推进 576 分钟，睡眠完成 1,296 分钟的跨日恢复。

| 镜头 | 平均 FPS | P99 帧时间 | 最慢帧 |
| --- | ---: | ---: | ---: |
| 城郊 1× | 166.49 | 8.37 毫秒 | 14.22 毫秒 |
| 社区 1× | 144.98 | 9.51 毫秒 | 13.97 毫秒 |
| 公园 1× | 144.25 | 9.33 毫秒 | 14.31 毫秒 |
| 花园 1× | 141.45 | 9.41 毫秒 | 19.66 毫秒 |
| 赛场 1× | 172.74 | 8.38 毫秒 | 14.83 毫秒 |
| 电脑店 1× | 147.63 | 10.60 毫秒 | 23.17 毫秒 |
| 住宅 1× | 299.50 | 5.22 毫秒 | 13.76 毫秒 |
| 城郊读档后 | 163.86 | 8.34 毫秒 | 14.23 毫秒 |
| 花园 4× | 140.58 | 10.74 毫秒 | 18.51 毫秒 |
| 睡眠跨日 | 153.84 | 10.92 毫秒 | 29.02 毫秒 |

没有超过 33.33 毫秒的采样帧。十段样本的可见材质缺失、着色器异常、结束时待加载纹理及未达到目标 mip 的数量均为 0。花园中介前的树冠实机图中，先前标出的右侧实心绿块未出现。首次进档约 8 秒，其中人物加载约 4 秒，可作为下一次启动耗时排查的切入点。

这些数值对应编辑器画面，赛场使用固定镜头。连续驾驶、后期多店满员、快速拉镜头和 Windows 客户端帧率仍需独立测量。花园、电脑店等镜头的 P99 高于 120 FPS 对应的 8.33 毫秒。

[性能原始数据](validation/2026-11-28-followup-and-windows/performance/performance.json) · [花园正常视距实机截图](validation/2026-11-28-followup-and-windows/performance/garden-1x.png)

## Windows x64 完整版

| 项目 | 记录 |
| --- | --- |
| 本地 ZIP | `F:/PCShopSell_builds/PCShopSell-Windows-x64-20261128-131356-c04d280b/PCShopSell-Windows-x64-20261128-131356-c04d280b.zip` |
| 来源 | `art/experiment` 基线 `c04d280b905c10b343f9ab1df242e5df8e7b5a79` 加本轮源码修改。本次 Windows 运行时改动为 `DistrictGateView.cs`，其哈希见构建信息 |
| 构建 | Unity 2022.3.6f1、Windows x64、Mono 正式版，默认 1920×1080 窗口，完整版地图重新烘焙 |
| 容量 | **324,661,041 字节**，约 **309.6 MiB**，含 133 个文件 |
| ZIP SHA-256 | `0e27fbde797d6b0131007e03f3a725f47269bcb06a7ef34a5a9c3f63b9db0722` |
| 校验 | Unity 构建成功，核对程序的 Windows x64 PE 头和主要运行文件，逐项读取 ZIP 并与源文件比较 SHA-256。Windows 客户端启动和性能为后续验证项 |

上一版 `7c51368a` 的 ZIP 为 289,988,682 字节。本版增加 34,672,359 字节（约 33.1 MiB），主要增长落在 Unity 的 `data.unity3d`。这是两次完整构建的差额，包含游戏内容及生成资源的变化。过程图清理量见[资源清理记录](165-current-handoff-and-resource-cleanup-2026-11-28.md#过程图清理)。

完整解压后运行 `PCShopSell/PCShopSell.exe`。换设备重建时，拉取本分支，用 Unity 2022.3.6f1 同步独立构建目录，以 `WindowsPlayerBuild.Run` 生成 Windows x64 完整版。构建入口会按源资源指纹检查或重烘地图。请替换下例中的项目、输出和日志路径。

```powershell
& 'D:/UnityEditor/2022.3.6f1/Editor/Unity.exe' -batchmode -projectPath '<构建目录>' -buildTarget StandaloneWindows64 -executeMethod WindowsPlayerBuild.Run -pcshop-build-output '<新目录>/PCShopSell.exe' -logFile '<日志目录>/unity-build.log'
```

游戏 ZIP 存于上表的本机目录。仓库提供[构建来源](validation/2026-11-28-followup-and-windows/windows/构建信息.json)、[构建摘要](validation/2026-11-28-followup-and-windows/windows/构建摘要.txt)、[ZIP 校验结果](validation/2026-11-28-followup-and-windows/windows/打包校验结果.json)及[逐文件哈希](validation/2026-11-28-followup-and-windows/windows/文件校验.json)。
