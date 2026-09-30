namespace Kable.Tests.Cases.Engine;

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Tests.Fixtures;
using Xunit;
using Xunit.Abstractions;

public sealed class LongRunningSoakStressTests
{
    private readonly ITestOutputHelper _output;

    public LongRunningSoakStressTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(Timeout = 60000)]
    public async Task TC_SOAK_01_FIFO_SequentialContinuousLoad()
    {
        // 1. Arrange: FIFO Half-Duplex Model with Background Telemetry
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec(delimiter: 0x0A);
        var options = new KableSessionOptions<string>
        {
            InboundQueueCapacity = 2000,
            IsAlarmMessage = msg => msg.StartsWith("ALARM:"),
            TelemetryOverflowMode = System.Threading.Channels.BoundedChannelFullMode.DropOldest
        };

        await using var session = new KableSession<string>(factory, codec, sessionOptions: options);
        await session.StartAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = cts.Token;

        const int targetRequests = 2000;
        const int targetTelemetry = 600;
        const int targetAlarms = 20;

        var latenciesUs = new List<double>(targetRequests);
        int successfulRequests = 0;
        int failedRequests = 0;
        int receivedAlarms = 0;

        var writeLock = new SemaphoreSlim(1, 1);
        Task? pumpTask = null;

        // 2. Hardware Mock Background Task
        var hardwareTask = Task.Run(async () =>
        {
            var reader = factory.Context.RemoteRead;
            var writer = factory.Context.RemoteWrite;

            // Background Telemetry & Alarm Pumper
            pumpTask = Task.Run(async () =>
            {
                int telemIdx = 0;
                int alarmIdx = 0;
                while (!token.IsCancellationRequested && (telemIdx < targetTelemetry || alarmIdx < targetAlarms))
                {
                    await writeLock.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        if (telemIdx < targetTelemetry)
                        {
                            var tBytes = Encoding.ASCII.GetBytes($"$DATA,CHAMBER_1,T={telemIdx},P=101.3\n");
                            await writer.WriteAsync(tBytes, token).ConfigureAwait(false);
                            telemIdx++;
                        }

                        if (telemIdx % 30 == 0 && alarmIdx < targetAlarms)
                        {
                            var aBytes = Encoding.ASCII.GetBytes($"ALARM:HIGH_TEMP_{alarmIdx}\n");
                            await writer.WriteAsync(aBytes, token).ConfigureAwait(false);
                            alarmIdx++;
                        }

                        await writer.FlushAsync(token).ConfigureAwait(false);
                    }
                    finally
                    {
                        writeLock.Release();
                    }

                    if (telemIdx % 30 == 0)
                    {
                        await Task.Yield();
                    }
                }
            }, token);

            // Echo responder for commands
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var readResult = await reader.ReadAsync(token).ConfigureAwait(false);
                    var buffer = readResult.Buffer;

                    while (TryReadLine(ref buffer, out var line))
                    {
                        if (line.StartsWith("REQ_"))
                        {
                            var id = line.Substring(4);
                            var reply = Encoding.ASCII.GetBytes($"RSP_{id}\n");
                            await writeLock.WaitAsync(token).ConfigureAwait(false);
                            try
                            {
                                await writer.WriteAsync(reply, token).ConfigureAwait(false);
                                await writer.FlushAsync(token).ConfigureAwait(false);
                            }
                            finally
                            {
                                writeLock.Release();
                            }
                        }
                    }

                    reader.AdvanceTo(buffer.Start, buffer.End);
                    if (readResult.IsCompleted || readResult.IsCanceled) break;
                }
            }
            catch (OperationCanceledException) { }
        }, token);

        // 3. Telemetry & Alarm Consumer Task
        var consumerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in session.GetStreamAsync(token))
                {
                    if (item.StartsWith("ALARM:"))
                    {
                        Interlocked.Increment(ref receivedAlarms);
                    }
                }
            }
            catch (OperationCanceledException) { }
        }, token);

        // 4. Memory baseline before load
        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        long memBefore = GC.GetTotalMemory(true);
        var swTotal = Stopwatch.StartNew();

        // 5. Sequential Client Request Pumping (Strict FIFO half-duplex)
        for (int idx = 0; idx < targetRequests; idx++)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var resp = await session.RequestAsync<string>($"REQ_{idx}", TimeSpan.FromSeconds(5), token);
                sw.Stop();
                if (resp == $"RSP_{idx}")
                {
                    successfulRequests++;
                    latenciesUs.Add(sw.Elapsed.TotalMicroseconds);
                }
                else
                {
                    failedRequests++;
                }
            }
            catch
            {
                failedRequests++;
            }
        }

        swTotal.Stop();

        // Ensure background stream completes and all alarms are drained
        if (pumpTask != null)
        {
            try { await pumpTask.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None); } catch { }
        }

        var drainSw = Stopwatch.StartNew();
        while (receivedAlarms < targetAlarms && drainSw.Elapsed < TimeSpan.FromSeconds(3))
        {
            await Task.Delay(20);
        }

        cts.Cancel();
        try { await hardwareTask; } catch { }
        try { await consumerTask; } catch { }

        // 6. Memory after load and cleanup
        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        long memAfter = GC.GetTotalMemory(true);
        long memDeltaBytes = memAfter - memBefore;

        // 7. Calculate Latency Statistics
        var latencyList = latenciesUs.OrderBy(x => x).ToList();
        double minUs = latencyList.Count > 0 ? latencyList.First() : 0;
        double maxUs = latencyList.Count > 0 ? latencyList.Last() : 0;
        double avgUs = latencyList.Count > 0 ? latencyList.Average() : 0;
        double p50Us = latencyList.Count > 0 ? latencyList[(int)(latencyList.Count * 0.50)] : 0;
        double p95Us = latencyList.Count > 0 ? latencyList[(int)(latencyList.Count * 0.95)] : 0;
        double p99Us = latencyList.Count > 0 ? latencyList[(int)(latencyList.Count * 0.99)] : 0;
        double errorRate = (double)failedRequests / targetRequests * 100.0;
        double rps = successfulRequests / swTotal.Elapsed.TotalSeconds;

        _output.WriteLine("=================================================================");
        _output.WriteLine(" Kable: In-Memory Mock Loopback Stress Test (Sequential FIFO, 2k)");
        _output.WriteLine("=================================================================");
        _output.WriteLine($"Execution Duration    : {swTotal.Elapsed.TotalSeconds:F2} seconds");
        _output.WriteLine($"Throughput            : {rps:F1} requests/sec");
        _output.WriteLine($"Total Requests        : {targetRequests:N0}");
        _output.WriteLine($"Successful Requests   : {successfulRequests:N0}");
        _output.WriteLine($"Failed Requests       : {failedRequests:N0}");
        _output.WriteLine($"Error Rate            : {errorRate:F3}%");
        _output.WriteLine($"-----------------------------------------------------------------");
        _output.WriteLine($"RoundTrip Latency Min : {minUs:F1} μs");
        _output.WriteLine($"RoundTrip Latency Avg : {avgUs:F1} μs");
        _output.WriteLine($"RoundTrip Latency P50 : {p50Us:F1} μs");
        _output.WriteLine($"RoundTrip Latency P95 : {p95Us:F1} μs");
        _output.WriteLine($"RoundTrip Latency P99 : {p99Us:F1} μs");
        _output.WriteLine($"RoundTrip Latency Max : {maxUs:F1} μs");
        _output.WriteLine($"-----------------------------------------------------------------");
        _output.WriteLine($"Memory Before         : {memBefore / 1024.0 / 1024.0:F2} MB");
        _output.WriteLine($"Memory After          : {memAfter / 1024.0 / 1024.0:F2} MB");
        _output.WriteLine($"Memory Delta          : {memDeltaBytes / 1024.0:F1} KB (Threshold: < 5,120 KB)");
        _output.WriteLine($"-----------------------------------------------------------------");
        _output.WriteLine($"Injected Alarms       : {targetAlarms}");
        _output.WriteLine($"Received Alarms       : {receivedAlarms}");
        _output.WriteLine($"Dropped Telemetry     : {session.DroppedTelemetryCount} messages");
        _output.WriteLine("=================================================================");

        // 8. Verdict Criteria
        failedRequests.Should().Be(0, "FIFO sequential load must achieve 0 error rate.");
        successfulRequests.Should().Be(targetRequests);
        errorRate.Should().Be(0.0);
        avgUs.Should().BeLessThan(2000.0, "Average latency must be sub-millisecond range.");
        memDeltaBytes.Should().BeLessThan(5 * 1024 * 1024, "No memory leak over continuous load.");
        receivedAlarms.Should().Be(targetAlarms, "All alarms must be safely received even during heavy FIFO traffic.");
    }

    [Fact(Timeout = 60000)]
    public async Task TC_SOAK_02_ConcurrentMultiplexedLoad()
    {
        // 1. Arrange: Full-Duplex Multiplexed Model with Correlation ID
        var factory = new TestMemoryConnectionFactory();
        var codec = new TestCorrelationCodec();
        var options = new KableSessionOptions<string>
        {
            InboundQueueCapacity = 2000,
            IsAlarmMessage = msg => msg.StartsWith("ALARM:"),
            TelemetryOverflowMode = System.Threading.Channels.BoundedChannelFullMode.DropOldest
        };

        await using var session = new KableSession<string>(factory, codec, sessionOptions: options);
        await session.StartAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = cts.Token;

        const int targetRequests = 2000;
        const int targetTelemetry = 600;
        const int targetAlarms = 20;

        var latenciesUs = new ConcurrentBag<double>();
        int successfulRequests = 0;
        int failedRequests = 0;
        int receivedAlarms = 0;

        var writeLock = new SemaphoreSlim(1, 1);
        Task? pumpTask = null;

        // 2. Hardware Mock Background Task (Full-duplex CID echo)
        var hardwareTask = Task.Run(async () =>
        {
            var reader = factory.Context.RemoteRead;
            var writer = factory.Context.RemoteWrite;

            // Background Telemetry & Alarm Pumper
            pumpTask = Task.Run(async () =>
            {
                int telemIdx = 0;
                int alarmIdx = 0;
                while (!token.IsCancellationRequested && (telemIdx < targetTelemetry || alarmIdx < targetAlarms))
                {
                    await writeLock.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        if (telemIdx < targetTelemetry)
                        {
                            var tBytes = Encoding.ASCII.GetBytes($"$DATA,SENSOR={telemIdx}\n");
                            await writer.WriteAsync(tBytes, token).ConfigureAwait(false);
                            telemIdx++;
                        }

                        if (telemIdx % 30 == 0 && alarmIdx < targetAlarms)
                        {
                            var aBytes = Encoding.ASCII.GetBytes($"ALARM:PRESSURE_WARN_{alarmIdx}\n");
                            await writer.WriteAsync(aBytes, token).ConfigureAwait(false);
                            alarmIdx++;
                        }

                        await writer.FlushAsync(token).ConfigureAwait(false);
                    }
                    finally
                    {
                        writeLock.Release();
                    }

                    if (telemIdx % 50 == 0)
                    {
                        await Task.Yield();
                    }
                }
            }, token);

            // CID Echo responder
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var readResult = await reader.ReadAsync(token).ConfigureAwait(false);
                    var buffer = readResult.Buffer;

                    while (TryReadLine(ref buffer, out var line))
                    {
                        // line: REQ:cid:payload
                        if (line.StartsWith("REQ:"))
                        {
                            var parts = line.Split(':');
                            if (parts.Length >= 2)
                            {
                                var cid = parts[1];
                                var reply = Encoding.ASCII.GetBytes($"RSP:{cid}:OK\n");
                                await writeLock.WaitAsync(token).ConfigureAwait(false);
                                try
                                {
                                    await writer.WriteAsync(reply, token).ConfigureAwait(false);
                                    await writer.FlushAsync(token).ConfigureAwait(false);
                                }
                                finally
                                {
                                    writeLock.Release();
                                }
                            }
                        }
                    }

                    reader.AdvanceTo(buffer.Start, buffer.End);
                    if (readResult.IsCompleted || readResult.IsCanceled) break;
                }
            }
            catch (OperationCanceledException) { }
        }, token);

        // 3. Telemetry & Alarm Consumer Task
        var consumerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in session.GetStreamAsync(token))
                {
                    if (item.StartsWith("ALARM:"))
                    {
                        Interlocked.Increment(ref receivedAlarms);
                    }
                }
            }
            catch (OperationCanceledException) { }
        }, token);

        // 4. Memory baseline
        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        long memBefore = GC.GetTotalMemory(true);
        var swTotal = Stopwatch.StartNew();

        // 5. 8 Concurrent Workers Pumping CID Requests (Parallel multiplexing)
        await Parallel.ForEachAsync(
            Enumerable.Range(0, targetRequests),
            new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = token },
            async (idx, ct) =>
            {
                var cid = $"CID{idx:D6}";
                var reqMsg = $"REQ:{cid}:DATA";
                var sw = Stopwatch.StartNew();
                try
                {
                    var resp = await session.RequestAsync<string>(reqMsg, TimeSpan.FromSeconds(5), ct);
                    sw.Stop();
                    if (resp == $"RSP:{cid}:OK")
                    {
                        Interlocked.Increment(ref successfulRequests);
                        latenciesUs.Add(sw.Elapsed.TotalMicroseconds);
                    }
                    else
                    {
                        Interlocked.Increment(ref failedRequests);
                    }
                }
                catch
                {
                    Interlocked.Increment(ref failedRequests);
                }
            });

        swTotal.Stop();

        if (pumpTask != null)
        {
            try { await pumpTask.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None); } catch { }
        }

        var drainSw = Stopwatch.StartNew();
        while (receivedAlarms < targetAlarms && drainSw.Elapsed < TimeSpan.FromSeconds(3))
        {
            await Task.Delay(20);
        }

        cts.Cancel();
        try { await hardwareTask; } catch { }
        try { await consumerTask; } catch { }

        // 6. Memory after load
        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        long memAfter = GC.GetTotalMemory(true);
        long memDeltaBytes = memAfter - memBefore;

        // 7. Statistics
        var latencyList = latenciesUs.OrderBy(x => x).ToList();
        double minUs = latencyList.Count > 0 ? latencyList.First() : 0;
        double maxUs = latencyList.Count > 0 ? latencyList.Last() : 0;
        double avgUs = latencyList.Count > 0 ? latencyList.Average() : 0;
        double p50Us = latencyList.Count > 0 ? latencyList[(int)(latencyList.Count * 0.50)] : 0;
        double p95Us = latencyList.Count > 0 ? latencyList[(int)(latencyList.Count * 0.95)] : 0;
        double p99Us = latencyList.Count > 0 ? latencyList[(int)(latencyList.Count * 0.99)] : 0;
        double errorRate = (double)failedRequests / targetRequests * 100.0;
        double rps = successfulRequests / swTotal.Elapsed.TotalSeconds;

        _output.WriteLine("=================================================================");
        _output.WriteLine(" Kable: In-Memory Mock Loopback Stress Test (Multiplexed, 2k, 8Th)");
        _output.WriteLine("=================================================================");
        _output.WriteLine($"Execution Duration    : {swTotal.Elapsed.TotalSeconds:F2} seconds");
        _output.WriteLine($"Throughput            : {rps:F1} requests/sec");
        _output.WriteLine($"Total Requests        : {targetRequests:N0}");
        _output.WriteLine($"Successful Requests   : {successfulRequests:N0}");
        _output.WriteLine($"Failed Requests       : {failedRequests:N0}");
        _output.WriteLine($"Error Rate            : {errorRate:F3}%");
        _output.WriteLine($"-----------------------------------------------------------------");
        _output.WriteLine($"RoundTrip Latency Min : {minUs:F1} μs");
        _output.WriteLine($"RoundTrip Latency Avg : {avgUs:F1} μs");
        _output.WriteLine($"RoundTrip Latency P50 : {p50Us:F1} μs");
        _output.WriteLine($"RoundTrip Latency P95 : {p95Us:F1} μs");
        _output.WriteLine($"RoundTrip Latency P99 : {p99Us:F1} μs");
        _output.WriteLine($"RoundTrip Latency Max : {maxUs:F1} μs");
        _output.WriteLine($"-----------------------------------------------------------------");
        _output.WriteLine($"Memory Before         : {memBefore / 1024.0 / 1024.0:F2} MB");
        _output.WriteLine($"Memory After          : {memAfter / 1024.0 / 1024.0:F2} MB");
        _output.WriteLine($"Memory Delta          : {memDeltaBytes / 1024.0:F1} KB (Threshold: < 5,120 KB)");
        _output.WriteLine($"-----------------------------------------------------------------");
        _output.WriteLine($"Injected Alarms       : {targetAlarms}");
        _output.WriteLine($"Received Alarms       : {receivedAlarms}");
        _output.WriteLine($"Dropped Telemetry     : {session.DroppedTelemetryCount} messages");
        _output.WriteLine("=================================================================");

        // 8. Verdict Criteria
        failedRequests.Should().Be(0, "Multiplexed CID load must achieve 0 error rate.");
        successfulRequests.Should().Be(targetRequests);
        errorRate.Should().Be(0.0);
        rps.Should().BeGreaterThan(500.0, "Throughput must maintain responsive progress (> 500 rps) under 8 concurrent workers.");
        avgUs.Should().BeLessThan(10000.0, "Average latency must remain responsive under load.");
        memDeltaBytes.Should().BeLessThan(5 * 1024 * 1024, "No memory leak over continuous load.");
        receivedAlarms.Should().Be(targetAlarms, "All alarms must be safely received during multiplexed traffic.");
    }

    private static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out string line)
    {
        var pos = buffer.PositionOf((byte)0x0A);
        if (pos == null)
        {
            line = string.Empty;
            return false;
        }

        var slice = buffer.Slice(0, pos.Value);
        line = Encoding.ASCII.GetString(BuffersExtensions.ToArray(slice));
        buffer = buffer.Slice(buffer.GetPosition(1, pos.Value));
        return true;
    }

    private sealed class TestCorrelationCodec : DelimitedFrameCodec<string>
    {
        public override bool SupportsCorrelationId => true;

        public TestCorrelationCodec() : base(new DelimitedFrameOptions
        {
            EndDelimiter = new byte[] { 0x0A },
            StripDelimiters = true
        }) { }

        public override string? ExtractCorrelationId(string message)
        {
            if (string.IsNullOrEmpty(message)) return null;
            // Format: REQ:cid:... or RSP:cid:...
            if (message.StartsWith("REQ:") || message.StartsWith("RSP:"))
            {
                var parts = message.Split(':');
                if (parts.Length >= 2) return parts[1];
            }
            return null;
        }

        public override bool IsAutonomousMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            return message[0] == '$' || message.StartsWith("ALARM:");
        }

        protected override bool TryDecodePayload(in ReadOnlySequence<byte> payloadSequence, out string message)
        {
            message = Encoding.ASCII.GetString(BuffersExtensions.ToArray(payloadSequence));
            return true;
        }

        public override void Encode(string message, IBufferWriter<byte> output)
        {
            var bytes = Encoding.ASCII.GetBytes(message);
            var span = output.GetSpan(bytes.Length + 1);
            bytes.CopyTo(span);
            span[bytes.Length] = 0x0A;
            output.Advance(bytes.Length + 1);
        }
    }
}
