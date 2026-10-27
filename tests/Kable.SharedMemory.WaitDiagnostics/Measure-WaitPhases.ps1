param(
    [ValidateRange(1, 100000)][int]$Iterations = 30000,
    [ValidateRange(1, 20)][int]$Rounds = 5,
    [switch]$BuildOnly
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runRoot = Join-Path $repoRoot ('artifacts/shared-memory-wait-cost/phases-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$instrumentedRoot = Join-Path $runRoot 'instrumented'
New-Item -ItemType Directory -Force -Path $instrumentedRoot | Out-Null
$utf8 = [Text.UTF8Encoding]::new($false)
$originalPath = Join-Path $repoRoot 'src/Kable.SharedMemory/Memory/RegisteredEventWait.cs'
$source = (Get-Content -LiteralPath $originalPath -Raw).Replace("`r`n", "`n")

function Replace-Required([string]$old, [string]$new) {
    if (-not $script:source.Contains($old)) { throw ('The diagnostic patch no longer matches: ' + $old) }
    $script:source = $script:source.Replace($old, $new)
}

Replace-Required 'using System.Threading;' "using Kable.SharedMemory.WaitDiagnostics;`nusing System.Threading;"
$allocation = @'
var trace = new WaitPhaseProbe { Entered = WaitPhaseProbe.Now() };
        WaitPhaseProbe.Current = trace;
        var completion = new TaskCompletionSource<bool>(trace, TaskCreationOptions.RunContinuationsAsynchronously);
        trace.RegisterStart = WaitPhaseProbe.Now();
'@
Replace-Required 'var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);' $allocation
$callback = @'
static (state, timedOut) =>
            {
                var completion = (TaskCompletionSource<bool>)state!;
                var trace = (WaitPhaseProbe)completion.Task.AsyncState!;
                trace.Callback = WaitPhaseProbe.Now();
                trace.Completing = WaitPhaseProbe.Now();
                completion.TrySetResult(!timedOut);
            },
'@
Replace-Required 'static (state, timedOut) => ((TaskCompletionSource<bool>)state!).TrySetResult(!timedOut),' $callback
Replace-Required 'completion, timeoutMs, executeOnlyOnce: true);' "completion, timeoutMs, executeOnlyOnce: true);`n        trace.RegisterEnd = WaitPhaseProbe.Now();"
$cancelEnd = @'
static (state, token) => ((TaskCompletionSource<bool>)state!).TrySetCanceled(token), completion);
'@
Replace-Required $cancelEnd ($cancelEnd + "`n            trace.CancellationRegistered = WaitPhaseProbe.Now();")
Replace-Required 'return await completion.Task.ConfigureAwait(false);' "bool result = await completion.Task.ConfigureAwait(false);`n            trace.Resumed = WaitPhaseProbe.Now();`n            return result;"
Replace-Required 'finally { registration.Unregister(null); }' 'finally { trace.UnregisterStart = WaitPhaseProbe.Now(); registration.Unregister(null); trace.UnregisterEnd = WaitPhaseProbe.Now(); }'
$sourcePath = Join-Path $runRoot 'RegisteredEventWait.cs'
[IO.File]::WriteAllText($sourcePath, $source, $utf8)
foreach ($file in @('Program.cs','WaitPhaseProbe.cs','Distribution.cs','Kable.SharedMemory.WaitDiagnostics.csproj')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination (Join-Path $instrumentedRoot $file)
}
& dotnet build (Join-Path $PSScriptRoot 'Kable.SharedMemory.WaitDiagnostics.csproj') -c Release --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'Uninstrumented helper build failed.' }
& dotnet build (Join-Path $instrumentedRoot 'Kable.SharedMemory.WaitDiagnostics.csproj') -c Release "-p:RegisteredWaitSource=$sourcePath" --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'Instrumented helper build failed.' }
$controlDll = Join-Path $PSScriptRoot 'bin/Release/net10.0-windows/Kable.SharedMemory.WaitDiagnostics.dll'
$phasesDll = Join-Path $instrumentedRoot 'bin/Release/net10.0-windows/Kable.SharedMemory.WaitDiagnostics.dll'
if ($BuildOnly) { Write-Output "Control: $controlDll"; Write-Output "Phases: $phasesDll"; return }
$output = Join-Path $runRoot 'wait-phases.jsonl'
for ($round = 1; $round -le $Rounds; $round++) {
    $modes = if ($round % 2 -eq 1) { @('control','phases') } else { @('phases','control') }
    foreach ($mode in $modes) {
        $dll = if ($mode -eq 'control') { $controlDll } else { $phasesDll }
        $json = & dotnet $dll $mode $Iterations
        if ($LASTEXITCODE -ne 0) { throw ('Wait diagnostic failed: ' + $mode) }
        $row = $json | ConvertFrom-Json
        $row | Add-Member -NotePropertyName Round -NotePropertyValue $round
        $row | Add-Member -NotePropertyName SourceSha256 -NotePropertyValue (Get-FileHash -LiteralPath $originalPath -Algorithm SHA256).Hash
        $row | ConvertTo-Json -Depth 6 -Compress | Add-Content -LiteralPath $output -Encoding utf8
        Write-Output "$mode round=$round mean=$($row.Total.MeanUs)us p99=$($row.Total.P99Us)us"
    }
}
Write-Output "Results: $output"
