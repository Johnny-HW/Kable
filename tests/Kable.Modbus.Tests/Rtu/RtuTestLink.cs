namespace Kable.Modbus.Tests.Rtu;

using System.Buffers;
using Kable.Core;
using Kable.Core.Checksums;
using Kable.Modbus.Rtu;
using Kable.Transports;

internal sealed class RtuTestLink : IAsyncDisposable
{
    private readonly InMemoryConnectionContext _server;
    internal ModbusRtuMaster Master { get; }
    private RtuTestLink(ModbusRtuOptions options)
    {
        var (client, server) = InMemoryConnectionContext.CreatePair();
        _server = server;
        Master = new ModbusRtuMaster(new Factory(client), options);
    }
    internal static async Task<RtuTestLink> OpenAsync(ModbusRtuOptions? options = null)
    {
        var link = new RtuTestLink(options ?? new ModbusRtuOptions());
        await link.Master.StartAsync();
        return link;
    }
    internal async Task<byte[]> ReadRequestAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (true)
        {
            var read = await _server.Input.ReadAsync(deadline.Token);
            var buffer = read.Buffer;
            if (buffer.Length >= 8)
            {
                byte[] header = buffer.Slice(0, 7).ToArray();
                int length = header[1] is 15 or 16 ? 9 + header[6] : 8;
                if (buffer.Length >= length)
                {
                    byte[] result = buffer.Slice(0, length).ToArray();
                    _server.Input.AdvanceTo(buffer.GetPosition(length));
                    AssertCrc(result);
                    return result;
                }
            }
            _server.Input.AdvanceTo(buffer.Start, buffer.End);
        }
    }
    internal async Task RespondAsync(byte[] body, bool corrupt = false, bool fragmented = false)
    {
        byte[] frame = new byte[body.Length + 2];
        Crc16Modbus.Append(body, frame);
        if (corrupt) frame[^1] ^= 0xFF;
        if (fragmented)
        {
            await _server.Output.WriteAsync(frame.AsMemory(0, 2));
            await _server.Output.WriteAsync(frame.AsMemory(2));
        }
        else await _server.Output.WriteAsync(frame);
    }
    internal async Task DisconnectAsync() => await _server.Output.CompleteAsync();
    private static void AssertCrc(byte[] frame) => Xunit.Assert.True(Crc16Modbus.Validate(frame));
    public async ValueTask DisposeAsync()
    {
        await Master.DisposeAsync();
        await _server.DisposeAsync();
    }
    private sealed class Factory(IConnectionContext context) : IConnectionFactory
    {
        public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default) => new(context);
    }
}
