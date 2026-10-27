param(
    [string]$BaselineRef = '75cb0f8',
    [ValidateRange(1, 20)][int]$Rounds = 3,
    [ValidateRange(0, 86400)][int]$SteadySeconds = 60,
    [switch]$BuildOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runRoot = Join-Path $repoRoot ('artifacts/shared-memory-reconnect/compare-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$transportRoot = Join-Path $runRoot 'baseline/Kable.SharedMemory'
$profileRoot = Join-Path $runRoot 'baseline/Profile'
$utf8 = [Text.UTF8Encoding]::new($false)
New-Item -ItemType Directory -Force -Path $transportRoot, $profileRoot | Out-Null
$files = & git -C $repoRoot ls-tree -r --name-only $BaselineRef -- src/Kable.SharedMemory
if ($LASTEXITCODE -ne 0 -or -not $files) { throw 'Cannot resolve the baseline shared-memory source.' }
foreach ($file in $files) {
    $destination = Join-Path $transportRoot $file.Substring('src/Kable.SharedMemory/'.Length)
    New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
    $contents = (& git -C $repoRoot show ($BaselineRef + ':' + $file)) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw ('Cannot export ' + $file) }
    [IO.File]::WriteAllText($destination, $contents + "`n", $utf8)
}
$transportProject = Join-Path $transportRoot 'Kable.SharedMemory.csproj'
[xml]$projectXml = Get-Content -LiteralPath $transportProject -Raw
foreach ($reference in $projectXml.SelectNodes('//ProjectReference')) {
    $reference.Include = [IO.Path]::GetFullPath((Join-Path (Join-Path $repoRoot 'src/Kable.SharedMemory') $reference.Include))
}
$projectXml.Save($transportProject)
foreach ($file in @('TransportProfile.cs', 'TransportProfile.Workload.cs', 'ProfileLink.cs')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination (Join-Path $profileRoot $file)
}
$profileProject = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0-windows</TargetFramework></PropertyGroup>
  <ItemGroup><ProjectReference Include="../Kable.SharedMemory/Kable.SharedMemory.csproj" /></ItemGroup>
</Project>
'@
[IO.File]::WriteAllText((Join-Path $profileRoot 'Profile.csproj'), $profileProject, $utf8)
$entry = 'await Kable.SharedMemory.Benchmarks.TransportProfile.RunAsync(args[1], args[2], int.Parse(args[3]), int.Parse(args[4]), int.Parse(args[5]), int.Parse(args[6]));'
[IO.File]::WriteAllText((Join-Path $profileRoot 'Program.cs'), $entry, $utf8)
& dotnet build (Join-Path $PSScriptRoot 'Kable.SharedMemory.Benchmarks.csproj') -c Release --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'Current profiler build failed.' }
& dotnet build (Join-Path $profileRoot 'Profile.csproj') -c Release --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'Baseline profiler build failed.' }
$baselineDll = Join-Path $profileRoot 'bin/Release/net10.0-windows/Profile.dll'
$currentDll = Join-Path $PSScriptRoot 'bin/Release/net10.0-windows/Kable.SharedMemory.Benchmarks.dll'
if ($BuildOnly) { Write-Output "Baseline: $baselineDll"; Write-Output "Current: $currentDll"; return }
$outputPath = Join-Path $runRoot 'profiles.jsonl'

function Measure-Profile([string]$variant, [string]$scenario, [int]$pairs, [int]$characters, [int]$operations, [int]$seconds, [int]$round) {
    $dll = if ($variant -eq 'baseline') { $baselineDll } else { $currentDll }
    $mode = if ($variant -eq 'baseline') { 'raw' } else { $variant }
    $json = & dotnet $dll --profile $mode $scenario $pairs $characters $operations $seconds
    if ($LASTEXITCODE -ne 0) { throw ('Profile failed: ' + $variant) }
    $row = $json | ConvertFrom-Json
    $row | Add-Member -NotePropertyName Build -NotePropertyValue $variant
    $row | Add-Member -NotePropertyName Round -NotePropertyValue $round
    $row | ConvertTo-Json -Compress | Add-Content -LiteralPath $outputPath -Encoding utf8
    Write-Output "$variant $scenario pairs=$pairs chars=$characters round=$round p99=$($row.P99Microseconds)us"
}

for ($round = 1; $round -le $Rounds; $round++) {
    foreach ($pairs in @(1, 32)) {
        foreach ($characters in @(32, 1024)) {
            $operations = if ($pairs -eq 1) { 20000 } else { 2000 }
            $variants = if ($round % 2 -eq 1) { @('baseline', 'raw', 'managed') } else { @('managed', 'raw', 'baseline') }
            foreach ($variant in $variants) { Measure-Profile $variant request $pairs $characters $operations 0 $round }
        }
    }
}
foreach ($scenario in @('burst', 'stream')) {
    $operations = if ($scenario -eq 'stream') { 100000 } else { 10000 }
    foreach ($variant in @('baseline', 'raw', 'managed')) { Measure-Profile $variant $scenario 1 32 $operations 0 1 }
}
if ($SteadySeconds -gt 0) {
    foreach ($variant in @('baseline', 'managed')) { Measure-Profile $variant request 1 1024 0 $SteadySeconds 0 }
}
$rows = @(Get-Content -LiteralPath $outputPath | ConvertFrom-Json)
if (@($rows.EngineSha256 | Select-Object -Unique).Count -ne 1 -or @($rows.CoreSha256 | Select-Object -Unique).Count -ne 1) {
    throw 'Engine/Core binaries differ across profiles; this comparison is invalid.'
}
Write-Output "Results: $outputPath"
