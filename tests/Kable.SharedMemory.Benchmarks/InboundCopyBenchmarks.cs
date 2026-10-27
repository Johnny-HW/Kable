namespace Kable.SharedMemory.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;
using Kable.SharedMemory.Memory;
using Perfolizer.Horology;

// Isolates the extra copy. Both paths use the same current ring lifetime protection and Set calls.
[MemoryDiagnoser]
[Config(typeof(WindowsConfig))]
public class InboundCopyBenchmarks
{
    public sealed class WindowsConfig : ManualConfig
    {
        public WindowsConfig()
            => AddJob(Job.Default.WithToolchain(CsProjCoreToolchain.From(
                    new NetCoreAppSettings("net10.0-windows", null, ".NET 10 Windows")))
                .WithWarmupCount(3).WithIterationCount(5).WithLaunchCount(2)
                .WithIterationTime(TimeInterval.FromMilliseconds(250)));
    }

    [Params(64, 4096)] public int Bytes { get; set; }
    private SharedMemoryRingBuffer _writer = null!;
    private SharedMemoryRingBuffer _reader = null!;
    private byte[] _payload = null!;
    private byte[] _temporary = null!;
    private byte[] _output = null!;

    [GlobalSetup]
    public void Setup()
    {
        string name = "copy_bench_" + Guid.NewGuid().ToString("N");
        _writer = SharedMemoryRingBuffer.Create(name, 8192);
        _reader = SharedMemoryRingBuffer.Open(name);
        _payload = Enumerable.Range(0, Bytes).Select(i => (byte)(i * 31)).ToArray();
        _temporary = new byte[Bytes];
        _output = new byte[Bytes];
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = 256)]
    public int TemporaryThenOutput()
    {
        int count = 0;
        for (int i = 0; i < 256; i++)
        {
            _writer.Write(_payload);
            int read = _reader.Read(_temporary);
            _temporary.AsSpan(0, read).CopyTo(_output);
            count += read;
        }
        return count;
    }

    [Benchmark(OperationsPerInvoke = 256)]
    public int DirectToOutput()
    {
        int count = 0;
        for (int i = 0; i < 256; i++)
        {
            _writer.Write(_payload);
            count += _reader.Read(_output);
        }
        return count;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _reader.Dispose();
        _writer.Dispose();
    }
}
