# 性能续修证据索引

结论与边界见 [162 续修报告](../../162-performance-followup-2026-11-27.md)。本目录只做本地归档，没有上传。

- `runs`：五次 NUnit 批次的 XML、逐例结果、失败历史、源码 SHA-256 和模拟耗时。
- `合成存档夹具`：本次性能对照专门生成的初始／预期 JSON，不是玩家存档。对照脚本为 `完整存档对照脚本.cs.txt`；复现时将夹具放到结果根目录的 `state-fixtures` 子目录。
- `profile-before`、`profile-after`、`profile-indexed`：逐步诊断，不用最优的某次结果替代完整巡检。
- `graphics`：主动中止的部分采样，目录内保留中止说明，不能算完整验收。
- `graphics-final`：人物资源预备之前的完整十场景巡检，使用旧编辑器回调驱动。
- `playerloop-diagnostic`：正常 Update 循环中的单场花园诊断，包括引擎线程计时。
- `playerloop-final`：人物预备后的完整十场景巡检，原始 JSON、截图和实际脚本已归档；526 个运行时代码文件的指纹与当前工作区一致。

每次图形巡检的 `巡检脚本.cs.txt` 是该次实际脚本版本，不能拿最新工具覆盖旧证据。截图是额外离屏渲染的场景画面，不含 Overlay HUD；帧率采样时仍更新、渲染实际 Game View 的 HUD，截图耗时不纳入采样。

未复制含启动凭证的完整 Unity 日志、临时工程或玩家存档。加载日志只保留以 `[城市加载]` 开头的阶段耗时。

## 复现入口

在项目根目录运行。镜像必须在主项目外，并且不能被另一 Unity 进程占用；图形巡检与 CPU 回归串行运行。

```powershell
pwsh -NoProfile -File tools/run-tests.ps1 -Mirror D:/tmp/PCShop-main-build-5f4a412c -ResultsRoot D:/tmp/pcshop-perf-recheck -Filter 'SleepPerformanceTests;PerformanceRegressionTests;CompanyWellbeingRegressionTests;SaveGhostStateTests;CityActivityTests;CityStoryTests;OnsitePlayTests;DistrictDetailTests;DistrictProgressionTests;PathfinderTests;WorldMapTests;CityTravelTimingTests;ShopFootfallTests' -Tag regression -TimeoutSeconds 600

pwsh -NoProfile -File tools/qa/run-visual.ps1 -Mirror D:/tmp/PCShop-main-build-5f4a412c -Output D:/tmp/pcshop-perf-recheck/graphics -Capture QaPerformanceCapture -GameView -PlayerLoop -TimeoutSeconds 900
```

追加 `-Hotspots -Scenario garden-1x` 运行单场细分诊断。不要把细分计时开销混入正式帧率比较。巡检退出码为 0 仅表示正常运行完成，是否达到 120 FPS 要单独查看 `meets120P99`、最慢 1% 帧、最长帧和休息结果。
