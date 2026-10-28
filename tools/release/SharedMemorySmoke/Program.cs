using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Samples.SharedMemoryReconnect;
using Kable.Transports;
using Kable.Transports.Simulators;

var (client, server) = InMemoryConnectionContext.CreatePair();
await using var simulator = new MockHardwareSimulator(server).OnCommand("STATUS", "READY").OnCommand("PING", "PONG");
simulator.Start();
await using var device = new KableSession<string>(new Factory(client), new AsciiLineCodec());
using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
await device.StartAsync(stop.Token);
string channel = "KableSampleSmoke_" + Guid.NewGuid().ToString("N");
var errors = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
var host = UiReconnectExample.RunHostAsync(device, channel, errors.Enqueue, stop.Token);
try
{
    for (int i = 0; i < 2; i++)
    {
        var (ui, state) = await UiReconnectExample.OpenUiAsync(channel, stop.Token);
        await using (ui)
        {
            if (state.Trim() != "READY") throw new Exception("UI did not obtain initial state.");
            if ((await ui.RequestAsync<string>("PING", TimeSpan.FromSeconds(3), stop.Token)).Trim() != "PONG")
                throw new Exception("Shared-memory UI command failed.");
        }
        if (!device.IsConnected) throw new Exception("UI disconnect terminated the device session.");
    }
    if ((await device.RequestAsync<string>("PING", TimeSpan.FromSeconds(3), stop.Token)).Trim() != "PONG")
        throw new Exception("Device session did not survive UI reconnect.");
}
finally { stop.Cancel(); await host; }
Console.WriteLine($"Shared-memory sample smoke OK: two UI sessions, device retained; IPC diagnostics={errors.Count}.");

internal sealed class Factory(IConnectionContext context) : IConnectionFactory
{
    public ValueTask<IConnectionContext> ConnectAsync(CancellationToken ct = default) => new(context);
}
