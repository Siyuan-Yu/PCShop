# 同机位材质复测

在仓库根目录运行以下 PowerShell 命令，可从定稿概念和 Unity 实机原帧重算六份取样表。使用 Python 3、NumPy 和 Pillow。定稿复测使用 NumPy 2.2.1、Pillow 13.0.0。

```powershell
$archive = "docs/art-direction/四区场景复核/三区实机验收/同机位复拍-2026-11-26"
python tools/district-art-capture/measure/measure.py --frames "$archive/实机原帧" --concepts docs/art-direction --output "$env:TEMP/pcshop-art-measure-review12" --label review12
```

`--frames` 指向一次完整拍摄的目录，其中含 `oldtown-home.png`、`oldtown-yard.png`、`oldtown-wide.png` 三张城郊图、`02A.png` 至 `04B.png` 六张分区街景、`fishing-a.png` 和 `fishing-b.png`，以及 `sites/02-life_community_park.png`。`--concepts` 根目录下应有 `old-quarter/交付图档`、`四区场景复核/分区概念` 和 `四区场景复核/城郊绿化与钓场定稿.png`。改用其他轮次的原图时，相应替换 `--frames` 和 `--label`。

输出目录含城郊草地树冠与铺装、住宅墙面、花树投影、三区草地、池岸、青湾钓场六份 TSV。每次计算定稿与指定轮次，使用独立输出目录即可保留交付表里的历史记录。02社区公园船池仅有这个机位的实拍记录；其余有对应定稿的样区都列出双方数值。

`regions.py` 保存与机位关联的地标、采样多边形、立面斜带和取样区间。亮度使用 sRGB 画面 Y′，范围为 0–255；明暗跨度取第90与第10百分位之差；笔触用同一分辨率下不同半径的高斯模糊差估计。色差使用 D65 下的 CIE76。重新取景或修改画幅后需先核对取样位置，再解释材质差异。
