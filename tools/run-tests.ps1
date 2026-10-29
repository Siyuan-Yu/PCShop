# 在工程镜像里跑 EditMode 测试（编辑器开着本工程时也能跑）。
# 用法：powershell -File tools\run-tests.ps1 [-Unity <Unity.exe>] [-Mirror <镜像目录>]
param(
    [string]$Platform = "EditMode",
    [string]$Unity = "D:\UnityEditor\2022.3.6f1\Editor\Unity.exe",
    [string]$Mirror = "$PSScriptRoot\..\..\_ci"
)
$src = (Resolve-Path "$PSScriptRoot\..").Path
New-Item -ItemType Directory -Force $Mirror | Out-Null
$dst = (Resolve-Path $Mirror).Path
foreach ($d in "Assets", "Packages", "ProjectSettings") {
    robocopy "$src\$d" "$dst\$d" /MIR /NFL /NDL /NJH /NJS /NP /R:0 /W:0 | Out-Null
}
$xml = "$env:TEMP\pcshop-tests.xml"
$log = "$env:TEMP\pcshop-tests.log"
Remove-Item $xml -ErrorAction SilentlyContinue
# 不用 Start-Process -Wait：Unity 的子进程会让它一直挂着。
$p = Start-Process -FilePath $Unity -ArgumentList '-batchmode', '-nographics', '-projectPath', "`"$dst`"", '-runTests', '-testPlatform', $Platform, '-testResults', "`"$xml`"", '-logFile', "`"$log`"" -PassThru
$p.WaitForExit()
"exit $($p.ExitCode)"
if (Test-Path $xml) {
    [xml]$r = Get-Content $xml
    $run = $r.'test-run'
    "total=$($run.total) passed=$($run.passed) failed=$($run.failed)"
    $r.SelectNodes("//test-case[@result='Failed']") | ForEach-Object {
        "FAIL $($_.fullname)"
        "  $($_.failure.message.'#cdata-section')"
    }
} else {
    Select-String -Path $log -Pattern "error CS|Exception" | Select-Object -First 40 | ForEach-Object { $_.Line }
}
