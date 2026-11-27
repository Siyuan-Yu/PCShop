# 按实际测试类分组；长年模拟单独执行。
param([string]$Mirror='D:/tmp/pcshop-suburb-day-mam5h57m/preview', [string]$ResultsRoot='D:/tmp/pcshop-qa-20261127', [int]$BatchSize=10, [string]$Round='baseline', [int]$BatchTimeoutSeconds=600)
$ErrorActionPreference='Stop'
$classes = @(Get-ChildItem "$PSScriptRoot/../Assets/Tests/EditMode" -Filter '*.cs' | ForEach-Object {
    $text=Get-Content -LiteralPath $_.FullName -Raw
    [regex]::Matches($text,'public\s+(?:sealed\s+)?class\s+(\w+Tests?)(?:\s|:)') | ForEach-Object { $_.Groups[1].Value }
} | Sort-Object -Unique | Where-Object { $_ -notin @('EconomyCalibrationTests','LongSessionRegressionTests') })
$classes | ConvertTo-Json | Set-Content "$ResultsRoot-classes.json" -Encoding utf8
if($classes.Count -eq 0){throw '没有发现测试类，不能把空运行算成通过。'}
$failedBatches=0
for($i=0;$i -lt $classes.Count;$i+=$BatchSize) {
    $batch=@($classes[$i..([math]::Min($i+$BatchSize-1,$classes.Count-1))])
    $filter=($batch | ForEach-Object { if($_ -eq 'CampaignTests') { '^PCShopSell.Tests.CampaignTests\.(?!ThreeYears_)' } elseif($_ -eq 'EventScriptTests') { '^PCShopSell.Tests.EventScriptTests\.(?!RandomEvents_)' } elseif($_ -eq 'EventSystemTests') { '^PCShopSell.Tests.EventSystemTests\.(?!Events_FireOverTime_AndEveryChoiceResolves)' } else { '^PCShopSell.Tests.'+$_+'\.' } }) -join ';'
    $args=@('-NoProfile','-File',"$PSScriptRoot/run-tests.ps1",'-Mirror',$Mirror,'-ResultsRoot',$ResultsRoot,'-Filter',$filter,'-Tag',('{0}-{1:d2}' -f $Round,($i/$BatchSize+1)),'-TimeoutSeconds',$BatchTimeoutSeconds)
    if($i -gt 0) { $args+='-SkipSync' }
    & pwsh @args
    if($LASTEXITCODE -ne 0){$failedBatches++}
}
if($failedBatches -gt 0){Write-Output "分批运行结束：$failedBatches 批未通过，请检查各批 XML、失败记录和超时进度。";exit 1}
exit 0
