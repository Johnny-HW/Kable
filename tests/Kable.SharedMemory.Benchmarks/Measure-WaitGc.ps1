param([ValidateRange(1000, 20000)][int]$Iterations = 20000, [ValidateRange(1, 20)][int]$Rounds = 5)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$buildOutput = & (Join-Path $PSScriptRoot 'Compare-Transport.ps1') -BuildOnly
$buildOutput | Write-Output
$baselineDll = ($buildOutput | Where-Object { $_ -like 'Baseline: *' } | Select-Object -Last 1).Substring(10)
$currentDll = ($buildOutput | Where-Object { $_ -like 'Current: *' } | Select-Object -Last 1).Substring(9)
$baselineRoot = [IO.Path]::GetFullPath((Join-Path (Split-Path $baselineDll) '../../..'))
$runRoot = Join-Path $repoRoot ('artifacts/shared-memory-wait-cost/gc-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$counterRoot = Join-Path $runRoot 'counter/Kable.SharedMemory'
$counterProfile = Join-Path $runRoot 'counter/Profile'
New-Item -ItemType Directory -Force -Path $counterRoot, $counterProfile | Out-Null
$utf8 = [Text.UTF8Encoding]::new($false)
$entry = 'await Kable.SharedMemory.Benchmarks.TransportProfile.RunAsync(args[1], args[2], int.Parse(args[3]), int.Parse(args[4]), int.Parse(args[5]), int.Parse(args[6]), noGc: args[0] == "--profile-no-gc", prepareGc: true, warmupRequests: 5000);'
[IO.File]::WriteAllText((Join-Path $baselineRoot 'Program.cs'), $entry, $utf8)
& dotnet build (Join-Path $baselineRoot 'Profile.csproj') -c Release --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'GC baseline build failed.' }
foreach ($file in (& git -C $repoRoot ls-files -- src/Kable.SharedMemory)) {
    $destination = Join-Path $counterRoot $file.Substring('src/Kable.SharedMemory/'.Length)
    New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot $file) -Destination $destination
}
$counterProject = Join-Path $counterRoot 'Kable.SharedMemory.csproj'
[xml]$projectXml = Get-Content -LiteralPath $counterProject -Raw
foreach ($reference in $projectXml.SelectNodes('//ProjectReference')) {
    $reference.Include = [IO.Path]::GetFullPath((Join-Path (Join-Path $repoRoot 'src/Kable.SharedMemory') $reference.Include))
}
$projectXml.Save($counterProject)
$helperPath = Join-Path $counterRoot 'Memory/RegisteredEventWait.cs'
$helper = Get-Content -LiteralPath $helperPath -Raw
$oldCallback = 'static (state, timedOut) => ((TaskCompletionSource<bool>)state!).TrySetResult(!timedOut),'
if (-not $helper.Contains($oldCallback)) { throw 'The counter patch does not match the current helper.' }
$helper = $helper.Replace('ct.ThrowIfCancellationRequested();', "ct.ThrowIfCancellationRequested();`n        Interlocked.Increment(ref WaitCostCounters.Registrations);")
$helper = $helper.Replace($oldCallback, 'static (state, timedOut) => { Interlocked.Increment(ref WaitCostCounters.Callbacks); ((TaskCompletionSource<bool>)state!).TrySetResult(!timedOut); },')
[IO.File]::WriteAllText($helperPath, $helper, $utf8)
$counterSource = @'
namespace Kable.SharedMemory.Memory;
public static class WaitCostCounters
{
    public static long Registrations = 0;
    public static long Callbacks = 0;
}
'@
[IO.File]::WriteAllText((Join-Path $counterRoot 'Memory/WaitCostCounters.cs'), $counterSource, $utf8)
foreach ($file in @('TransportProfile.cs', 'TransportProfile.Workload.cs', 'ProfileLink.cs')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination (Join-Path $counterProfile $file)
}
Copy-Item -LiteralPath (Join-Path $baselineRoot 'Profile.csproj') -Destination (Join-Path $counterProfile 'Profile.csproj')
[IO.File]::WriteAllText((Join-Path $counterProfile 'Program.cs'), $entry, $utf8)
& dotnet build (Join-Path $counterProfile 'Profile.csproj') -c Release --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'Wait-counter profiler build failed.' }
$counterDll = Join-Path $counterProfile 'bin/Release/net10.0-windows/Profile.dll'
$outputPath = Join-Path $runRoot 'gc-profiles.jsonl'
for ($round = 1; $round -le $Rounds; $round++) {
    $variants = if ($round % 2 -eq 1) { @('baseline','managed','counter') } else { @('counter','managed','baseline') }
    foreach ($variant in $variants) {
        $dll = switch ($variant) { 'baseline' { $baselineDll } 'managed' { $currentDll } 'counter' { $counterDll } }
        $mode = if ($variant -eq 'baseline') { 'raw' } else { 'managed' }
        $gcModes = if ($round % 2 -eq 1) { @('--profile-gc','--profile-no-gc') } else { @('--profile-no-gc','--profile-gc') }
        foreach ($gcMode in $gcModes) {
            $json = & dotnet $dll $gcMode $mode request 1 1024 $Iterations 0
            if ($LASTEXITCODE -ne 0) { throw "GC comparison failed: $variant $gcMode" }
            $row = $json | ConvertFrom-Json
            if ($gcMode -eq '--profile-no-gc' -and (-not $row.NoGcRegionActive -or ($row.GcCollections | Measure-Object -Sum).Sum -ne 0)) {
                throw 'GC occurred in a requested no-GC measurement.'
            }
            $row | Add-Member -NotePropertyName Build -NotePropertyValue $variant
            $row | Add-Member -NotePropertyName Round -NotePropertyValue $round
            $row | ConvertTo-Json -Compress | Add-Content -LiteralPath $outputPath -Encoding utf8
            Write-Output "$variant $gcMode round=$round mean=$($row.MeanMicroseconds)us p99=$($row.P99Microseconds)us waits=$($row.WaitRegistrationsPerOperation)"
        }
    }
}
$rows = @(Get-Content -LiteralPath $outputPath | ConvertFrom-Json)
if (@($rows.EngineSha256 | Select-Object -Unique).Count -ne 1 -or @($rows.CoreSha256 | Select-Object -Unique).Count -ne 1) {
    throw 'Engine/Core binaries differ across GC measurements.'
}
Write-Output "Results: $outputPath"
