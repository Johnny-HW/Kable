namespace Kable.Modbus.Tests.Soak;

using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Kable.Engine;
using Kable.Modbus.Codecs;
using Kable.Modbus.Messages;
using Kable.Transports;

internal sealed class LoopbackModbusDevice : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _server;
    public ModbusTcpMaster Master { get; }
    internal LoopbackModbusDevice(byte value)
    {
        _listener.Start();
        int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Master = new ModbusTcpMaster(new KableSession<ModbusTcpMessage>(new TcpConnectionFactory("127.0.0.1", port),
            new ModbusTcpCodec()), new ModbusTcpOptions());
        _server = ServeAsync(value, _stop.Token);
    }
    private async Task ServeAsync(byte value, CancellationToken ct)
    {
        try
        {
            using var client = await _listener.AcceptTcpClientAsync(ct);
            await using var stream = client.GetStream();
            var reader = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));
            var codec = new ModbusTcpCodec();
            int count = 0;
            try
            {
                while (true)
                {
                    var read = await reader.ReadAsync(ct);
                    var buffer = read.Buffer;
                    while (codec.TryDecode(ref buffer, out var request))
                    {
                        if (++count % 31 == 0) await Task.Delay(2, ct);
                        var output = new ArrayBufferWriter<byte>();
                        codec.Encode(new(request.TransactionId, request.UnitId, request.FunctionCode, new byte[] { 2, 0, value }), output);
                        await stream.WriteAsync(output.WrittenMemory, ct);
                    }
                    reader.AdvanceTo(buffer.Start, buffer.End);
                    if (read.IsCompleted) break;
                }
            }
            finally { await reader.CompleteAsync(); }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { /* Normal test shutdown. */ }
    }
    public async ValueTask DisposeAsync()
    {
        await Master.DisposeAsync();
        _stop.Cancel(); _listener.Stop();
        await _server.WaitAsync(TimeSpan.FromSeconds(5));
        _stop.Dispose();
    }
}
