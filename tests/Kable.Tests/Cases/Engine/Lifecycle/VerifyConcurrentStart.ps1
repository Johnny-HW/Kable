param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../..'))
$probeRoot = Join-Path ([IO.Path]::GetTempPath()) ('kable-start-race-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $probeRoot | Out-Null
$runtimeProject = [Security.SecurityElement]::Escape((Join-Path $repoRoot 'src/Kable/Kable.csproj'))
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup><ProjectReference Include="$runtimeProject" /></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $probeRoot 'Probe.csproj') -Encoding utf8

# Run thread-pool scheduling controls in a child process, never in the test runner.
@'
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;

ThreadPool.GetMinThreads(out int minWorkers, out int minIo);
ThreadPool.GetMaxThreads(out int maxWorkers, out int maxIo);
if (!ThreadPool.SetMinThreads(1, 1) || !ThreadPool.SetMaxThreads(1, 1))
    throw new Exception("Unable to control diagnostic thread pool.");
using var workerEntered = new ManualResetEventSlim();
using var workerRelease = new ManualResetEventSlim();
ThreadPool.QueueUserWorkItem(_ => { workerEntered.Set(); workerRelease.Wait(); });
if (!workerEntered.Wait(TimeSpan.FromSeconds(10))) throw new Exception("Worker did not start.");
var factory = new FailureFactory();
var session = new KableSession<string>(factory, new AsciiLineCodec());
using var caller = new CancellationTokenSource();
using var starterDone = new ManualResetEventSlim();
Exception? starterError = null;
var starterThread = new Thread(() =>
{
    try { session.StartAsync().AsTask().GetAwaiter().GetResult(); }
    catch (Exception error) { starterError = error; }
    finally { starterDone.Set(); }
}) { IsBackground = true };
starterThread.Start();
Task? firstWaiter = null;
try
{
    if (!factory.Entered.Wait(TimeSpan.FromSeconds(10))) throw new Exception("Factory did not start.");
    firstWaiter = session.StartAsync(caller.Token).AsTask();
    factory.Release.Set();
    if (!starterDone.Wait(TimeSpan.FromSeconds(10))) throw new Exception("Starter did not finish.");
}
finally
{
    workerRelease.Set();
    factory.Release.Set();
    ThreadPool.SetMaxThreads(maxWorkers, maxIo);
    ThreadPool.SetMinThreads(minWorkers, minIo);
}
if (!ReferenceEquals(starterError, factory.Failure)) throw new Exception("Starter lost original failure.");
if (caller.IsCancellationRequested) throw new Exception("Uncanceled caller was unexpectedly canceled.");
var waiterError = FailureOf(firstWaiter!);
if (!ReferenceEquals(waiterError, factory.Failure))
    throw new Exception($"Uncanceled waiter received {waiterError.GetType().Name} instead of original connection failure.");
if (factory.Calls != 1 || session.IsConnected || session.State != SessionLifecycleState.Stopped)
    throw new Exception("Connection count or terminal state was incorrect.");
if (!ReferenceEquals(FailureOf(session.DisposeAsync().AsTask()), factory.Failure))
    throw new Exception("Cleanup lost original connection failure.");
if (!starterThread.Join(TimeSpan.FromSeconds(10))) throw new Exception("Starter thread did not exit.");
Console.WriteLine("PASS: starter, uncanceled waiter and cleanup preserve the same connection failure.");

static Exception FailureOf(Task task)
{
    try { task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult(); }
    catch (Exception error) { return error; }
    throw new Exception("Expected operation to fail.");
}

sealed class FailureFactory : IConnectionFactory
{
    public ManualResetEventSlim Entered { get; } = new();
    public ManualResetEventSlim Release { get; } = new();
    public Exception Failure { get; } = new InvalidOperationException("Original connection failure");
    public int Calls { get; private set; }
    public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
    {
        Calls++;
        Entered.Set();
        if (!Release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Factory gate timed out.");
        throw Failure;
    }
}
'@ | Set-Content -LiteralPath (Join-Path $probeRoot 'Program.cs') -Encoding utf8

& dotnet build (Join-Path $probeRoot 'Probe.csproj') -c $Configuration --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Failed to build startup regression probe.' }
$probeDll = Join-Path $probeRoot "bin/$Configuration/net10.0/Probe.dll"
1..3 | ForEach-Object {
    & dotnet $probeDll
    if ($LASTEXITCODE -ne 0) { throw "Startup regression probe failed on execution $_." }
}
Write-Host "Concurrent-start regression passed three controlled executions. Artifacts: $probeRoot"
