param([string]$Mirror='D:/tmp/pcshop-suburb-day-mam5h57m/preview',[string]$Output='D:/tmp/pcshop-qa-visual-20261127',[int]$TimeoutSeconds=900,
      [ValidateSet('QaRuntimeCapture','QaPerformanceCapture','RelationshipExperienceCapture')][string]$Capture='QaRuntimeCapture',
      [switch]$GameView,[switch]$Hotspots,[string]$Scenario="",[switch]$PlayerLoop,[ValidateRange(0,300)][int]$SampleSeconds=0)
$ErrorActionPreference='Stop'
$src=(Resolve-Path "$PSScriptRoot/../..").Path.TrimEnd('\')
$dst=[IO.Path]::GetFullPath($Mirror).TrimEnd('\')
if($dst -eq $src -or $src.StartsWith($dst+'\',[StringComparison]::OrdinalIgnoreCase) -or $dst.StartsWith($src+'\',[StringComparison]::OrdinalIgnoreCase)){throw '必须使用主项目之外的隔离镜像。'}
foreach($process in (Get-CimInstance Win32_Process -Filter "name='Unity.exe'")) {
    if($process.CommandLine -and $process.CommandLine.Replace('/','\').IndexOf($dst,[StringComparison]::OrdinalIgnoreCase)-ge 0){throw '镜像正在使用。'}
}
foreach($dir in 'Assets','Packages','ProjectSettings') {
    robocopy "$src/$dir" "$dst/$dir" /MIR /NFL /NDL /NJH /NJS /NP /R:0 /W:0 | Out-Null
    if($LASTEXITCODE -ge 8){throw "同步失败 $dir"}
}
$settings=Join-Path $dst 'ProjectSettings/ProjectSettings.asset'
$config=Get-Content -LiteralPath $settings -Raw
$config=[regex]::Replace($config,'(?m)^  companyName:.*$','  companyName: PCShopQA')
$config=[regex]::Replace($config,'(?m)^  productName:.*$','  productName: PCShopSellQA')
Set-Content -LiteralPath $settings -Value $config -Encoding utf8 -NoNewline
Copy-Item -LiteralPath "$PSScriptRoot/$Capture.cs" -Destination "$dst/Assets/Editor/$Capture.cs"
$outputPath=Join-Path ([IO.Path]::GetFullPath($Output)) (Get-Date -Format 'yyyyMMdd-HHmmss')
New-Item -ItemType Directory -Force $outputPath | Out-Null
$old=$env:PCSHOP_QA_RESULTS;$env:PCSHOP_QA_RESULTS=$outputPath
$arguments=@('-projectPath',('"'+$dst+'"'),'-executeMethod',($Capture+'.Run'),'-logFile',('"'+$outputPath+'/unity.log"'))
if(!$GameView){$arguments=@('-batchmode')+$arguments}
if($Hotspots){$arguments+='-pcshop-perf-hotspots'}
if($PlayerLoop){$arguments+='-pcshop-perf-playerloop'}
if($SampleSeconds -gt 0){$arguments+=@('-pcshop-perf-seconds',"$SampleSeconds")}
if($Scenario){
    if($Scenario -notin @('suburb-1x','community-1x','park-1x','garden-1x','race-1x','shop-1x','home-1x','suburb-reload','garden-4x','home-sleep')){throw '未知巡检场景'}
    $arguments+=@('-pcshop-perf-case',$Scenario)
}
try{$process=Start-Process -WindowStyle Hidden -FilePath 'D:/UnityEditor/2022.3.6f1/Editor/Unity.exe' -ArgumentList $arguments -PassThru}
finally{$env:PCSHOP_QA_RESULTS=$old}
Write-Output "实机巡检 pid=$($process.Id) output=$outputPath"
$finished=$process.WaitForExit($TimeoutSeconds*1000)
if(!$finished){Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue}
$result=[ordered]@{exitCode=$process.ExitCode;timeout=!$finished;output=$outputPath;screenshots=@(Get-ChildItem $outputPath -Filter '*.png').Count}
$result|ConvertTo-Json|Set-Content "$outputPath/summary.json" -Encoding utf8
$result|ConvertTo-Json -Compress
if(!$finished -or $process.ExitCode -ne 0 -or !(Test-Path "$outputPath/result.txt")){exit 1}
