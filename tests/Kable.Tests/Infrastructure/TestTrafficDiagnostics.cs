namespace Kable.Tests.Infrastructure;

using System.Text;
using System.Threading.Channels;
using Kable.Observability;

internal sealed class TestTrafficDiagnostics : ICommObserver
{
    private readonly object _gate = new();
    private readonly PacketTraceRecord[] _recent = new PacketTraceRecord[24];
    private readonly CommObserver _streams = new(bufferCapacity: 1);
    private long _count;

    public ChannelReader<PacketTraceRecord> PeriodicStream => _streams.PeriodicStream;
    public ChannelReader<PacketTraceRecord> CommandStream => _streams.CommandStream;
    public ChannelReader<PacketTraceRecord> AlarmStream => _streams.AlarmStream;

    public void OnPacketTrace(in PacketTraceRecord trace)
    {
        lock (_gate)
        {
            _recent[_count % _recent.Length] = trace;
            _count++;
        }
    }

    public string Snapshot()
    {
        lock (_gate)
        {
            var text = new StringBuilder($"Trace records: {_count}\n");
            for (long i = Math.Max(0, _count - _recent.Length); i < _count; i++)
            {
                var record = _recent[i % _recent.Length];
                text.AppendLine($"{record.TimestampUtc:O} {record.Direction} {record.Level} {record.Tag}: {record.ParsedText}");
            }
            return text.ToString();
        }
    }

    public static async Task WaitForCompletionAsync(Task operation, Task server, CancellationToken ct, Task? disconnected = null)
    {
        var completed = await (disconnected == null
            ? Task.WhenAny(operation, server)
            : Task.WhenAny(operation, server, disconnected)).WaitAsync(ct);
        await completed;
        if (completed != operation)
            throw new InvalidOperationException("Server or connection ended before the expected client completion signal.");
    }
}
