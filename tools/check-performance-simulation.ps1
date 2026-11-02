param([Parameter(Mandatory=$true)][string]$AssemblyPath,
      [Parameter(Mandatory=$true)][string]$LayoutPath,
      [Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
$null=[Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $AssemblyPath))
$options=[System.Text.Json.JsonSerializerOptions]::new()
$options.IncludeFields=$true
$data=[System.Text.Json.JsonSerializer]::Deserialize([IO.File]::ReadAllText((Resolve-Path -LiteralPath $LayoutPath)),[PCShopSell.Core.World.CityLayoutData],$options)
$city=[PCShopSell.Core.World.CityLayout]::FromData($data)
$results=@()
foreach($preset in @('solo','staff')) {
    $session=[PCShopSell.Core.Session.GameSession]::CreateCityLifePreset($city,$preset)
    try {
        $session.EventsEnabled=$false
        $watch=[Diagnostics.Stopwatch]::StartNew()
        for($minute=0;$minute -lt 720;$minute++) {
            $session.Player.Hunger=100; $session.Player.Energy=100; $session.Player.Health=100
            $session.AdvanceClock(1)
        }
        $elapsed=$watch.Elapsed.TotalMilliseconds
        $state=[ordered]@{
            minute=$session.Clock.Now.TotalMinutes; cashCents=$session.Ledger.Balance.Cents
            shops=@($session.MyShops | ForEach-Object {
                [ordered]@{id=$_.Place.Id;customers=$_.Sim.TodayCustomers;lost=$_.Sim.TodayLost;salesCents=$_.Sim.TodaySales.Cents
                    parts=@($_.Sim.Stock.Lines() | ForEach-Object {"$($_.Part.Id):$($_.Boxed):$($_.Count)"} | Sort-Object)
                    queue=@($_.Sim.Queue | ForEach-Object {$_.Id});orders=@($_.Sim.Orders | ForEach-Object {"$($_.Id):$($_.EmployeeMinutesLeft)"})}
            })
            people=@($session.CityPeople | Where-Object {[PCShopSell.Core.Session.CityLifeCatalog]::Person($_.PersonId)} | Sort-Object PersonId | ForEach-Object {
                [ordered]@{id=$_.PersonId;place=$_.LocationPlaceId;task=[string]$_.Task;x=$_.X;y=$_.Y;target=$_.Route.TargetPlaceId;arrival=$_.Route.ArriveMinute}
            })
        }
        $results+=@{preset=$preset;simulationMinutes=720;elapsedMs=$elapsed;state=$state}
    } finally {$session.Dispose()}
}
$results | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $OutputPath -Encoding utf8
