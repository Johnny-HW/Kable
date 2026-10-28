using System.Net;
using System.Net.Sockets;
using System.Text;
using Kable.Generators;
using Kable.Modbus.Data;
using Kable.Simple;

if (new AttributePing().FormatWireMessage() != "PING") throw new Exception("Attribute generator failed.");
if (new ReleaseProbe.PingCommand().FormatWireMessage() != "PING") throw new Exception("YAML generator failed.");
ushort[] registers = new ushort[2];
PlcRegisterConverter.WriteSingle(12.5f, registers, PlcDataOrder.WordSwap);
if (PlcRegisterConverter.ReadSingle(registers, PlcDataOrder.WordSwap) != 12.5f) throw new Exception("PLC converter failed.");
foreach (string name in new[] { "Kable.Generators.dll", "Microsoft.CodeAnalysis.dll" })
    if (File.Exists(Path.Combine(AppContext.BaseDirectory, name))) throw new Exception("Compiler dependency leaked into runtime: " + name);

using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
using var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start();
int port = ((IPEndPoint)listener.LocalEndpoint).Port;
var server = Task.Run(async () =>
{
    using var tcp = await listener.AcceptTcpClientAsync(timeout.Token);
    using var stream = tcp.GetStream();
    using var reader = new StreamReader(stream, Encoding.ASCII);
    using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };
    string? request = await reader.ReadLineAsync(timeout.Token);
    await writer.WriteLineAsync(request == "STATUS?" ? "READY" : "ERROR");
});
await using (var device = await KableSimple.OpenTcpAsync("127.0.0.1", port, ct: timeout.Token))
    if (await device.QueryAsync("STATUS?", ct: timeout.Token) != "READY") throw new Exception("Packaged TCP query failed.");
await server;
await MqttProbe.RunAsync(timeout.Token);
await OpcUaProbe.RunAsync(timeout.Token);
Console.WriteLine($"Package consumer OK: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");

[DeviceCommand("PING")]
public readonly partial record struct AttributePing;
