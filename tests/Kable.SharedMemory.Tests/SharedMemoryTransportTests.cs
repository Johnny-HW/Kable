namespace Kable.SharedMemory.Tests;

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kable;
using Kable.Core;
using Kable.SharedMemory.Memory;
using Kable.SharedMemory.Waveform;
using Xunit;

public class SharedMemoryTransportTests
{
    [Fact]
    public void SharedMemoryRingBuffer_RoundTrip_PreservesDataIntegrity()
    {
        string channel = "test_ring_" + Guid.NewGuid().ToString("N")[..8];

        using var serverBuffer = SharedMemoryRingBuffer.Create(channel, 4096);
        using var clientBuffer = SharedMemoryRingBuffer.Open(channel);

        byte[] sendData = Encoding.UTF8.GetBytes("Hello Ultra-Low Latency MMF IPC!");
        int written = serverBuffer.Write(sendData);
        Assert.Equal(sendData.Length, written);

        byte[] receiveData = new byte[128];
        int read = clientBuffer.Read(receiveData);

        Assert.Equal(sendData.Length, read);
        string receivedText = Encoding.UTF8.GetString(receiveData, 0, read);
        Assert.Equal("Hello Ultra-Low Latency MMF IPC!", receivedText);
    }

    [Fact]
    public void SharedMemoryWaveformBuffer_BatchWriteAndRead_ZeroLoss()
    {
        string channel = "test_wave_" + Guid.NewGuid().ToString("N")[..8];

        using var serverWave = SharedMemoryWaveformBuffer.Create(channel, 2048);
        using var clientWave = SharedMemoryWaveformBuffer.Open(channel);

        var batch = new WaveformSample[100];
        for (int i = 0; i < 100; i++)
        {
            batch[i] = new WaveformSample(channelId: 1, value: i * 0.5, timestampTicks: 1000 + i);
        }

        int written = serverWave.WriteBatch(batch);
        Assert.Equal(100, written);

        var readBuffer = new WaveformSample[100];
        int read = clientWave.ReadBatch(readBuffer);

        Assert.Equal(100, read);
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(batch[i].ChannelId, readBuffer[i].ChannelId);
            Assert.Equal(batch[i].Value, readBuffer[i].Value);
            Assert.Equal(batch[i].TimestampTicks, readBuffer[i].TimestampTicks);
        }
    }

    [Fact]
    public async Task SharedMemoryConnectionContext_Over_KableSession_EchoTest()
    {
        string channel = "test_kable_ipc_" + Guid.NewGuid().ToString("N")[..8];

        await using var serverContext = SharedMemoryTransport.CreateServerEndpoint(channel, 16384);
        await using var clientContext = SharedMemoryTransport.ConnectClientEndpoint(channel);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // 서버 루프: 클라이언트가 보낸 메시지를 그대로 에코(Echo)
        var serverTask = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                var result = await serverContext.Input.ReadAsync(cts.Token);
                var buffer = result.Buffer;

                if (buffer.IsEmpty && result.IsCompleted) break;

                // CRLF 라인 파싱
                var seqReader = new SequenceReader<byte>(buffer);
                if (seqReader.TryReadTo(out ReadOnlySequence<byte> line, (byte)'\n'))
                {
                    byte[] lineBytes = line.ToArray();
                    serverContext.Input.AdvanceTo(seqReader.Position);

                    // Echo back with \n
                    var mem = serverContext.Output.GetMemory(lineBytes.Length + 1);
                    lineBytes.CopyTo(mem.Span);
                    mem.Span[lineBytes.Length] = (byte)'\n';
                    serverContext.Output.Advance(lineBytes.Length + 1);
                    await serverContext.Output.FlushAsync(cts.Token);
                    break;
                }
                else
                {
                    serverContext.Input.AdvanceTo(buffer.Start, buffer.End);
                }
            }
        }, cts.Token);

        // 클라이언트 전송: "PING_IPC\n"
        byte[] payload = Encoding.UTF8.GetBytes("PING_IPC\n");
        await clientContext.Output.WriteAsync(payload, cts.Token);

        // 클라이언트 수신 검증
        var clientResult = await clientContext.Input.ReadAsync(cts.Token);
        var clientBuffer = clientResult.Buffer;
        string received = Encoding.UTF8.GetString(clientBuffer.ToArray());
        Assert.Equal("PING_IPC\n", received);

        clientContext.Input.AdvanceTo(clientBuffer.End);
        await serverTask;
    }
}
