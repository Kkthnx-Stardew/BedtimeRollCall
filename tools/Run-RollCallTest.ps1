<#
.SYNOPSIS
Runs the two player night scenario in the real game. One process hosts a throwaway farm, a second one joins it, and Bedtime Roll Call runs in both.

.DESCRIPTION
The live Mods folder is never read or written. Each player gets its own small Mods folder under testprofile. The host's config sets a one minute
idle rule, and the joining player turns on auto sleep, so the scenario covers the panel, the idle rule, the go to bed code and the auto sleep.
Results and panel pictures land in testprofile\results. The throwaway BRCTest farm is removed afterwards.

.PARAMETER GamePath
The Stardew Valley folder. Defaults to the Steam library on D:.

.PARAMETER TimeoutSeconds
Stops both games after this long.
#>
param(
    [string] $GamePath = 'D:\SteamLibrary\steamapps\common\Stardew Valley',
    [int] $TimeoutSeconds = 600,
    [int] $StallSeconds = 180,
    [switch] $ClientWithoutMod,
    [switch] $WithConfigMenu
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$work = Join-Path $repo 'testprofile'
$results = Join-Path $work 'results'
$sync = Join-Path $results 'sync'
$logDir = Join-Path $env:APPDATA 'StardewValley\ErrorLogs'

dotnet build (Join-Path $repo 'BedtimeRollCall\BedtimeRollCall.csproj') -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Mod build failed.' }
dotnet build (Join-Path $repo 'BedtimeRollCall.DevHarness\BedtimeRollCall.DevHarness.csproj') -c Debug --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Harness build failed.' }

New-Item -ItemType Directory -Force $results | Out-Null
if (Test-Path $sync) { Remove-Item $sync -Recurse -Force }
New-Item -ItemType Directory $sync | Out-Null
Remove-Item (Join-Path $results 'night-*') -ErrorAction SilentlyContinue
Remove-Item (Join-Path $results 'rc-*.png') -ErrorAction SilentlyContinue

function New-ModsFolder([string] $name, [hashtable] $config) {
    $mods = Join-Path $work $name
    if (Test-Path $mods) { Remove-Item $mods -Recurse -Force }
    New-Item -ItemType Directory $mods | Out-Null
    Copy-Item (Join-Path $GamePath 'Mods\ConsoleCommands') (Join-Path $mods 'ConsoleCommands') -Recurse
    $own = Join-Path $mods 'BedtimeRollCall'
    New-Item -ItemType Directory $own | Out-Null
    Copy-Item (Join-Path $repo 'BedtimeRollCall\bin\Release\net6.0\BedtimeRollCall.dll') $own
    Copy-Item (Join-Path $repo 'BedtimeRollCall\manifest.json') $own
    Copy-Item (Join-Path $repo 'BedtimeRollCall\i18n') (Join-Path $own 'i18n') -Recurse
    $config | ConvertTo-Json | Set-Content (Join-Path $own 'config.json')
    if ($WithConfigMenu) { Copy-Item (Join-Path $GamePath 'Mods\GenericModConfigMenu') (Join-Path $mods 'GenericModConfigMenu') -Recurse }
    $harness = Join-Path $mods 'BedtimeRollCall.DevHarness'
    New-Item -ItemType Directory $harness | Out-Null
    Copy-Item (Join-Path $repo 'BedtimeRollCall.DevHarness\bin\Debug\net6.0\BedtimeRollCall.DevHarness.dll') $harness
    Copy-Item (Join-Path $repo 'BedtimeRollCall.DevHarness\manifest.json') $harness
    return $mods
}

$modsHost = New-ModsFolder 'ModsHost' @{ AfkMinutes = 1; WarnBeforeAfkSeconds = 10; AutoSleepWhenIdle = $false }
# the joining player keeps the default five minutes on purpose, the host's one minute rule must win
$modsClient = New-ModsFolder 'ModsClient' @{ AutoSleepWhenIdle = $true; PanelPosition = 'UnderClock' }
# a joining player without the mod, to check what the host sees and that the night still ends
if ($ClientWithoutMod) {
    Remove-Item (Join-Path $modsClient 'BedtimeRollCall') -Recurse -Force
    $env:BRC_NOMOD = '1'
}

$prefs = Join-Path $env:APPDATA 'StardewValley\startup_preferences'
$prefsBackup = $null
if (Test-Path $prefs) { $prefsBackup = Get-Content $prefs -Raw }

$env:BRC_SYNC = $sync
$env:BRC_ROLE = 'host'
$env:BRC_OUT = Join-Path $results 'night-host.json'
$hostProc = Start-Process -FilePath (Join-Path $GamePath 'StardewModdingAPI.exe') -ArgumentList '--no-terminal', '--mods-path', "`"$modsHost`"" -WorkingDirectory $GamePath -PassThru

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while (-not $hostProc.HasExited -and -not (Test-Path (Join-Path $sync 'hostready.host')) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 2 }
$clientProc = $null
if (Test-Path (Join-Path $sync 'hostready.host')) {
    Start-Sleep -Seconds 3
    $env:BRC_ROLE = 'client'
    $env:BRC_OUT = Join-Path $results 'night-client.json'
    $clientProc = Start-Process -FilePath (Join-Path $GamePath 'StardewModdingAPI.exe') -ArgumentList '--no-terminal', '--mods-path', "`"$modsClient`"" -WorkingDirectory $GamePath -PassThru
}
Remove-Item Env:BRC_ROLE, Env:BRC_OUT, Env:BRC_SYNC, Env:BRC_NOMOD -ErrorAction SilentlyContinue

# a run that stops making progress is stopped early. Progress means a new sync file or growing logs
$lastProgress = Get-Date
$lastStamp = ''
while ((Get-Date) -lt $deadline) {
    $stamp = (Get-ChildItem $sync -File -ErrorAction SilentlyContinue | Measure-Object).Count.ToString() + '/' + ((Get-ChildItem $logDir -File -Filter 'SMAPI-latest*.txt' -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum)
    if ($stamp -ne $lastStamp) { $lastStamp = $stamp; $lastProgress = Get-Date }
    elseif (((Get-Date) - $lastProgress).TotalSeconds -gt $StallSeconds) { Write-Warning "STALLED for $StallSeconds seconds, stopping both games."; break }
    if ($hostProc.HasExited -and (($null -eq $clientProc) -or $clientProc.HasExited)) { break }
    Start-Sleep -Seconds 2
}
foreach ($proc in @($hostProc, $clientProc)) {
    if ($proc -and -not $proc.HasExited) { Write-Warning "Stopping process $($proc.Id)."; Stop-Process -Id $proc.Id -Force }
}

$saves = Join-Path $env:APPDATA 'StardewValley\Saves'
Get-ChildItem $saves -Directory -Filter 'BRCTest_*' -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
if ($null -ne $prefsBackup -and (Get-Content $prefs -Raw) -ne $prefsBackup) { Set-Content $prefs $prefsBackup -NoNewline }

foreach ($f in Get-ChildItem $logDir -File -Filter 'SMAPI-latest*.txt') { Copy-Item $f.FullName (Join-Path $results "night-$($f.Name)") -Force }
foreach ($role in 'host', 'client') {
    $f = Join-Path $results "night-$role.json"
    if (Test-Path $f) { Write-Host "Results: $f" } else { Write-Warning "No $role results file." }
}
