# 隔离镜像、分批过滤、超时保护和独立结果目录。不会打包或上传。
param(
    [string]$Platform = "EditMode",
    [string]$Unity = "D:\UnityEditor\2022.3.6f1\Editor\Unity.exe",
    [string]$Mirror = "$PSScriptRoot\..\..\_ci",
    [string]$Filter = "",
    [string]$Tag = "manual",
    [int]$TimeoutSeconds = 300,
    [string]$ResultsRoot = "$PSScriptRoot\..\..\_qa-results",
    [switch]$SkipSync,
    [switch]$Graphics,
    [switch]$SimulationCapture
)
$ErrorActionPreference = 'Stop'
$src = (Resolve-Path "$PSScriptRoot\..").Path.TrimEnd('\')
$dst = [IO.Path]::GetFullPath($Mirror).TrimEnd('\')
if ($dst -eq $src -or $src.StartsWith($dst + '\', [StringComparison]::OrdinalIgnoreCase) -or $dst.StartsWith($src + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw '测试镜像必须在主项目之外，且不能是主项目的父目录。'
}
if ($Tag -notmatch '^[a-zA-Z0-9_.-]+$') { throw 'Tag 只能包含字母、数字、下划线、点和连字符。' }
if ($TimeoutSeconds -lt 10) { throw '超时至少为 10 秒。' }
$running = Get-CimInstance Win32_Process -Filter "name='Unity.exe'"
foreach ($process in $running) {
    if ($process.CommandLine -and $process.CommandLine.Replace('/','\').IndexOf($dst, [StringComparison]::OrdinalIgnoreCase) -ge 0) { throw "镜像已有 Unity 进程：$($process.ProcessId)" }
}
New-Item -ItemType Directory -Force $dst | Out-Null
if (!$SkipSync) {
    foreach ($d in 'Assets','Packages','ProjectSettings') {
        # 两端绝对路径已检查；仅同步指定镜像里的项目目录。
        robocopy "$src\$d" "$dst\$d" /MIR /NFL /NDL /NJH /NJS /NP /R:0 /W:0 | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "同步 $d 失败：$LASTEXITCODE" }
    }
}
# 镜像使用独立应用标识，避免 PlayerPrefs 与正在运行的主项目共享。
if ($SimulationCapture) {
    Copy-Item -LiteralPath "$PSScriptRoot/qa/SimulationPerformanceCapture.cs" -Destination "$dst/Assets/Tests/EditMode/SimulationPerformanceCapture.cs"
}
$settings=Join-Path $dst 'ProjectSettings/ProjectSettings.asset'
$settingsText=Get-Content -LiteralPath $settings -Raw
$settingsText=[regex]::Replace($settingsText,'(?m)^  companyName:.*$','  companyName: PCShopQA')
$settingsText=[regex]::Replace($settingsText,'(?m)^  productName:.*$','  productName: PCShopSellQA')
Set-Content -LiteralPath $settings -Value $settingsText -Encoding utf8 -NoNewline
$runDir = Join-Path ([IO.Path]::GetFullPath($ResultsRoot)) ("{0}-{1}-{2}" -f (Get-Date -Format 'yyyyMMdd-HHmmss'), $Tag, ([guid]::NewGuid().ToString('N').Substring(0,6)))
New-Item -ItemType Directory -Force $runDir | Out-Null
$manifestFiles=@(Get-ChildItem "$dst/Assets/Scripts","$dst/Assets/Tests","$dst/Packages","$dst/ProjectSettings" -File -Recurse | Where-Object { $_.Extension -in '.cs','.json','.asset','.txt','.asmdef' })
$manifest=@($manifestFiles | Sort-Object FullName | ForEach-Object { [ordered]@{path=$_.FullName.Substring($dst.Length+1).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} })
ConvertTo-Json -InputObject $manifest -Depth 3 | Set-Content -LiteralPath (Join-Path $runDir 'source-manifest.json') -Encoding utf8
$xml = Join-Path $runDir 'results.xml'
$log = Join-Path $runDir 'unity.log'
$arguments = @('-batchmode','-projectPath',('"' + $dst + '"'),'-runTests','-testPlatform',$Platform,'-testResults',('"' + $xml + '"'),'-logFile',('"' + $log + '"'))
if (!$Graphics) { $arguments += '-nographics' }
if ($Filter) { $arguments += @('-testFilter', ('"' + $Filter + '"')) }
$started = Get-Date
$previousResults=$env:PCSHOP_QA_RESULTS
$env:PCSHOP_QA_RESULTS=$runDir
try { $p = Start-Process -WindowStyle Hidden -FilePath $Unity -ArgumentList $arguments -PassThru }
finally { $env:PCSHOP_QA_RESULTS=$previousResults }
Write-Output "开始 $Tag pid=$($p.Id) results=$runDir"
$timedOut = !$p.WaitForExit($TimeoutSeconds * 1000)
if ($timedOut) {
    # 仅终止本轮启动的进程；不按进程名终止用户编辑器。
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    $p.WaitForExit(10000) | Out-Null
}
$summary = [ordered]@{ tag=$Tag; filter=$Filter; started=$started.ToString('o'); seconds=[math]::Round(((Get-Date)-$started).TotalSeconds,2); timeout=$timedOut; exitCode=$p.ExitCode; result='NoResult'; total=0; passed=0; failed=0; skipped=0; directory=$runDir }
if (Test-Path -LiteralPath $xml) {
    [xml]$r = Get-Content -LiteralPath $xml -Raw
    $run = $r.'test-run'
    $summary.result=$run.result; $summary.total=[int]$run.total; $summary.passed=[int]$run.passed; $summary.failed=[int]$run.failed; $summary.skipped=[int]$run.skipped
    $failures = @($r.SelectNodes("//test-case[@result='Failed']") | ForEach-Object {
        [ordered]@{ name=$_.fullname; message=$_.failure.message.InnerText; stack=$_.failure.'stack-trace'.InnerText }
    })
    ConvertTo-Json -InputObject $failures -Depth 5 | Set-Content -LiteralPath (Join-Path $runDir 'failures.json') -Encoding utf8
}
if ($timedOut) { $summary.result='Timeout' }
$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runDir 'summary.json') -Encoding utf8
$summary | ConvertTo-Json -Compress
if ($timedOut -or $summary.result -ne 'Passed' -or $summary.total -eq 0 -or $summary.failed -gt 0 -or $p.ExitCode -ne 0) { exit 1 }
exit 0
