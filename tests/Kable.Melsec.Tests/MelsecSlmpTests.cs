namespace Kable.Melsec.Tests;

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;
using Kable.Engine;
using Kable.Melsec;
using Kable.Melsec.Codecs;
using Kable.Melsec.Protocol;
using Xunit;

internal sealed class MockPlcDuplexPipeConnection : IConnectionContext
{
    public string ConnectionId { get; } = Guid.NewGuid().ToString("N");
    public string EndpointDescription { get; } = "MockPlcPipe";

    private readonly Pipe _inboundPipe = new();
    private readonly Pipe _outboundPipe = new();
    private readonly CancellationTokenSource _cts = new();

    public PipeReader Input => _inboundPipe.Reader;
    public PipeWriter Output => _outboundPipe.Writer;

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

internal sealed class MockPlcConnectionFactory : IConnectionFactory
{
    public MockPlcDuplexPipeConnection Connection { get; } = new();

    public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default)
    {
        return ValueTask.FromResult<IConnectionContext>(Connection);
    }
}

public class MelsecSlmpTests
{
    [Fact]
    public void MelsecSlmpCodec_EncodeAndDecodeRequest_Preserves3EStructure()
    {
        var codec = new MelsecSlmpCodec();
        var bufferWriter = new ArrayBufferWriter<byte>();

        var request = Slmp3EFrame.CreateReadDeviceRequest(
            device: MelsecDeviceCode.D,
            headDeviceNo: 1000,
            devicePoints: 10);

        codec.Encode(request, bufferWriter);

        var seq = new ReadOnlySequence<byte>(bufferWriter.WrittenMemory);
        Assert.True(seq.Length >= 21); // 15 header bytes + 6 request data bytes

        // Check Subheader (0x50, 0x00)
        Span<byte> span = stackalloc byte[(int)seq.Length];
        seq.CopyTo(span);
        Assert.Equal(0x50, span[0]);
        Assert.Equal(0x00, span[1]);

        // Check Command 0x0401 (Read)
        ushort command = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(11, 2));
        Assert.Equal(0x0401, command);

        // Check HeadDeviceNo 1000 and DeviceCode 0xA8 (D)
        int headNo = span[15] | (span[16] << 8) | (span[17] << 16);
        Assert.Equal(1000, headNo);
        Assert.Equal((byte)MelsecDeviceCode.D, span[18]);
    }

    [Fact]
    public async Task MelsecPlcClient_ReadWords_ReturnsValidRegisterValues()
    {
        var factory = new MockPlcConnectionFactory();
        var codec = new MelsecSlmpCodec();
        var session = new KableSession<Slmp3EFrame>(factory, codec);
        await session.StartAsync();

        var client = new MelsecPlcClient(session);
        var mockConn = factory.Connection;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // 가상 미쓰비시 PLC 시뮬레이터
        var plcServerTask = Task.Run(async () =>
        {
            var serverCodec = new MelsecSlmpCodec();
            while (!cts.IsCancellationRequested)
            {
                var readResult = await mockConn.ServerReader.ReadAsync(cts.Token);
                var buffer = readResult.Buffer;
                if (buffer.IsEmpty && readResult.IsCompleted) break;

                // 3E Request Frame 구조 파싱
                if (buffer.Length >= 21)
                {
                    // 응답 프레임 생성: D1000=1234, D1001=5678 (2워드 = 4바이트)
                    // 3E Response Frame: Subheader(0xD0, 0x00) + Net(0) + PC(0xFF) + DestIo(0x03FF) + Station(0) + Len(6: EndCode[2] + Data[4]) + EndCode(0x0000) + Data[4]
                    byte[] responseBytes = new byte[15];
                    responseBytes[0] = 0xD0;
                    responseBytes[1] = 0x00;
                    responseBytes[2] = 0x00;
                    responseBytes[3] = 0xFF;
                    BinaryPrimitives.WriteUInt16LittleEndian(responseBytes.AsSpan(4, 2), 0x03FF);
                    responseBytes[6] = 0x00;
                    BinaryPrimitives.WriteUInt16LittleEndian(responseBytes.AsSpan(7, 2), 6); // EndCode(2) + Data(4)
                    BinaryPrimitives.WriteUInt16LittleEndian(responseBytes.AsSpan(9, 2), 0); // EndCode 0 = Success
                    BinaryPrimitives.WriteUInt16LittleEndian(responseBytes.AsSpan(11, 2), 1234); // D1000
                    BinaryPrimitives.WriteUInt16LittleEndian(responseBytes.AsSpan(13, 2), 5678); // D1001

                    await mockConn.ServerWriter.WriteAsync(responseBytes, cts.Token);
                    mockConn.ServerReader.AdvanceTo(buffer.End);
                    break;
                }

                mockConn.ServerReader.AdvanceTo(buffer.Start, buffer.End);
            }
        }, cts.Token);

        // D1000부터 2개 워드 읽기 질의
        var words = await client.ReadWordsAsync(MelsecDeviceCode.D, headDeviceNo: 1000, count: 2, ct: cts.Token);

        Assert.Equal(2, words.Length);
        Assert.Equal(1234, words[0]);
        Assert.Equal(5678, words[1]);

        cts.Cancel();
        await client.DisposeAsync();
    }
}
