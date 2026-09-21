namespace Kable.Observability;

using System;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;
using Kable.Core;

/// <summary>
/// 통신 세션에서 오가는 모든 패킷을 실시간 Wireshark .pcap 파일로 덤프하는 옵저버.
/// </summary>
public sealed class PcapStreamObserver : ICommObserver, IAsyncDisposable
{
    private readonly PcapWriter _writer;
    private readonly Channel<PacketTraceRecord> _commandChannel;
    private readonly Channel<PacketTraceRecord> _periodicChannel;
    private readonly Channel<PacketTraceRecord> _alarmChannel;

    public ChannelReader<PacketTraceRecord> CommandStream => _commandChannel.Reader;
    public ChannelReader<PacketTraceRecord> PeriodicStream => _periodicChannel.Reader;
    public ChannelReader<PacketTraceRecord> AlarmStream => _alarmChannel.Reader;

    public PcapStreamObserver(string filePath)
    {
        var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
        _writer = new PcapWriter(fileStream);

        _commandChannel = Channel.CreateUnbounded<PacketTraceRecord>(new UnboundedChannelOptions { SingleWriter = false });
        _periodicChannel = Channel.CreateUnbounded<PacketTraceRecord>(new UnboundedChannelOptions { SingleWriter = false });
        _alarmChannel = Channel.CreateUnbounded<PacketTraceRecord>(new UnboundedChannelOptions { SingleWriter = false });
    }

    public void OnPacketTrace(in PacketTraceRecord trace)
    {
        // PCAP 파일 비동기 스트리밍 기록
        _ = _writer.WritePacketAsync(trace.RawBytes, trace.TimestampUtc);

        // 스트림 분배
        switch (trace.Kind)
        {
            case TrafficKind.AperiodicCommand:
                _commandChannel.Writer.TryWrite(trace);
                break;
            case TrafficKind.PeriodicTelemetry:
                _periodicChannel.Writer.TryWrite(trace);
                break;
            case TrafficKind.SpontaneousAlarm:
                _alarmChannel.Writer.TryWrite(trace);
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _writer.DisposeAsync();
    }
}
