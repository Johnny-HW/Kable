namespace Kable.Modbus.Tests;

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;
using Kable.Engine;
using Kable.Modbus;
using Kable.Modbus.Codecs;
using Kable.Modbus.Messages;
using Xunit;

internal sealed class MockDuplexPipeConnection : IConnectionContext
{
    public string ConnectionId { get; } = Guid.NewGuid().ToString("N");
    public string EndpointDescription { get; } = "MockDuplexPipe";

    private readonly Pipe _inboundPipe = new();
    private readonly Pipe _outboundPipe = new();
    private readonly CancellationTokenSource _cts = new();

    public PipeReader Input => _inboundPipe.Reader;
    public PipeWriter Output => _outboundPipe.Writer;

    // Server-side endpoints
    public PipeWriter ServerWriter => _inboundPipe.Writer;
    public PipeReader ServerReader => _outboundPipe.Reader;

    public CancellationToken ConnectionClosed => _cts.Token;

    public void Abort(string reason) => _cts.Cancel();

    public ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _inboundPipe.Writer.Complete();
        _inboundPipe.Reader.Complete();
        _outboundPipe.Writer.Complete();
        _outboundPipe.Reader.Complete();
        _cts.Dispose();
        return ValueTask.CompletedTask;
    }
}

internal sealed class MockConnectionFactory : IConnectionFactory
{
    public MockDuplexPipeConnection Connection { get; } = new();

    public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
    {
        return ValueTask.FromResult<IConnectionContext>(Connection);
    }
}

public class ModbusTcpTests
{
    [Fact]
    public void ModbusTcpCodec_EncodeAndDecode_PreservesMBAPAndPDU()
    {
        var codec = new ModbusTcpCodec();
        var bufferWriter = new ArrayBufferWriter<byte>();

        ushort transId = 0x1234;
        byte unitId = 0x01;
        var request = ModbusTcpMessage.CreateReadRegisters(transId, unitId, 0x03, startAddress: 100, quantity: 10);

        codec.Encode(request, bufferWriter);

        var seq = new ReadOnlySequence<byte>(bufferWriter.WrittenMemory);
        bool decoded = codec.TryDecode(ref seq, out var parsed);

        Assert.True(decoded);
        Assert.Equal(transId, parsed.TransactionId);
        Assert.Equal(unitId, parsed.UnitId);
        Assert.Equal(0x03, parsed.FunctionCode);
        Assert.Equal(4, parsed.Payload.Length);

        ushort parsedAddr = BinaryPrimitives.ReadUInt16BigEndian(parsed.Payload.Span.Slice(0, 2));
        ushort parsedQty = BinaryPrimitives.ReadUInt16BigEndian(parsed.Payload.Span.Slice(2, 2));
        Assert.Equal(100, parsedAddr);
        Assert.Equal(10, parsedQty);
    }

    [Fact]
    public async Task ModbusTcpMaster_ConcurrentRequests_CorrelatesResponsesByTransactionId()
    {
        var factory = new MockConnectionFactory();
        var codec = new ModbusTcpCodec();
        var session = new KableSession<ModbusTcpMessage>(factory, codec);
        await session.StartAsync();

        var master = new ModbusTcpMaster(session);
        var mockConn = factory.Connection;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // 가상 Modbus-TCP 슬레이브 장비 시뮬레이션 태스크
        // 요청 순서와 상관없이 수신된 요청의 Transaction ID를 그대로 달아서 응답
        var serverTask = Task.Run(async () =>
        {
            var serverCodec = new ModbusTcpCodec();
            while (!cts.IsCancellationRequested)
            {
                var readResult = await mockConn.ServerReader.ReadAsync(cts.Token);
                var buffer = readResult.Buffer;

                if (buffer.IsEmpty && readResult.IsCompleted) break;

                while (serverCodec.TryDecode(ref buffer, out var request))
                {
                    // FC03 요청에 대한 가상 레지스터 응답 (2개 레지스터: Val1 = TransId, Val2 = 42)
                    byte[] respPayload = new byte[5];
                    respPayload[0] = 4; // byte count
                    BinaryPrimitives.WriteUInt16BigEndian(respPayload.AsSpan(1, 2), request.TransactionId);
                    BinaryPrimitives.WriteUInt16BigEndian(respPayload.AsSpan(3, 2), 42);

                    var response = new ModbusTcpMessage(request.TransactionId, request.UnitId, request.FunctionCode, respPayload);
                    serverCodec.Encode(response, mockConn.ServerWriter);
                    await mockConn.ServerWriter.FlushAsync(cts.Token);
                }

                mockConn.ServerReader.AdvanceTo(buffer.Start, buffer.End);
            }
        }, cts.Token);

        // 5개의 동시 비동기 요청 발행 (Pipelining 동시성 검증)
        var tasks = new List<Task<ushort[]>>();
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(master.ReadHoldingRegistersAsync(startAddress: (ushort)(i * 10), count: 2, ct: cts.Token));
        }

        var results = await Task.WhenAll(tasks);

        // 각 호출자가 자신의 Transaction ID에 일치하는 응답 데이터를 정확히 수신했는지 확인
        Assert.Equal(5, results.Length);
        foreach (var regArray in results)
        {
            Assert.Equal(2, regArray.Length);
            Assert.Equal(42, regArray[1]); // Val2
        }

        cts.Cancel();
        await master.DisposeAsync();
    }
}
