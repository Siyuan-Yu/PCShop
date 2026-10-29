# 在工程镜像里跑冒烟测试并截图（需要图形，不能加 -nographics）。
# 截图输出到 <镜像>\docs\previews\vs1_*.png，加 -CopyShots 会拷回本工程。
# 也可以用 -Method PCShopSell.EditorTools.GameSceneBuilder.CreateSceneFromBatch 重新生成 Main 场景。
param(
    [string]$Method = "PCShopSell.EditorTools.GameSceneBuilder.SmokeFromBatch",
    [string]$Unity = "D:\UnityEditor\2022.3.6f1\Editor\Unity.exe",
    [string]$Mirror = "$PSScriptRoot\..\..\_ci",
    [switch]$CopyShots
)
$src = (Resolve-Path "$PSScriptRoot\..").Path
New-Item -ItemType Directory -Force $Mirror | Out-Null
$dst = (Resolve-Path $Mirror).Path
foreach ($d in "Assets", "Packages", "ProjectSettings") {
    robocopy "$src\$d" "$dst\$d" /MIR /NFL /NDL /NJH /NJS /NP /R:0 /W:0 | Out-Null
}
$log = "$env:TEMP\pcshop-smoke.log"
$p = Start-Process -FilePath $Unity -ArgumentList '-batchmode', '-projectPath', "`"$dst`"", '-executeMethod', $Method, '-logFile', "`"$log`"" -PassThru
$p.WaitForExit()
"exit $($p.ExitCode)"
Get-Content $log -Encoding UTF8 | Select-String "SMOKE|error CS|Exception" | Where-Object { $_ -notmatch "GameSceneBuilder:" } | ForEach-Object { $_.Line }
if ($CopyShots) {
    New-Item -ItemType Directory -Force "$src\docs\previews" | Out-Null
    Copy-Item "$dst\docs\previews\vs1_*.png" "$src\docs\previews\" -Force
}
