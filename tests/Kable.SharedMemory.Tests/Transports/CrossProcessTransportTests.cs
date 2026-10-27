namespace Kable.SharedMemory.Tests.Transports;

using System.Diagnostics;
using Xunit;

public class CrossProcessTransportTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(1048576)]
    public async Task IndependentProcess_EchoesAllBytesThroughNamedMappingAndEvents(int length)
    {
        string name = "process_echo_" + Guid.NewGuid().ToString("N");
        await using var server = SharedMemoryTransport.CreateServerEndpoint(name, 1024);
        using var peer = StartPeer("echo", name, length);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            Assert.Equal("READY", await peer.StandardOutput.ReadLineAsync(timeout.Token));
            byte[] payload = TransportTestSupport.Pattern(length);
            Task<byte[]> receive = TransportTestSupport.ReceiveAsync(server.Input, length, timeout.Token);
            await TransportTestSupport.SendAsync(server.Output, payload, 4097, timeout.Token);
            Assert.Equal(payload, await receive);
            await peer.StandardInput.WriteLineAsync("STOP");
            await peer.WaitForExitAsync(timeout.Token);
            Assert.Equal(0, peer.ExitCode);
            Assert.Contains("DONE", await peer.StandardOutput.ReadToEndAsync(timeout.Token));
            Assert.Equal(string.Empty, await peer.StandardError.ReadToEndAsync(timeout.Token));
        }
        finally { if (!peer.HasExited) peer.Kill(entireProcessTree: true); }
    }

    [Fact]
    public async Task IndependentProcess_ObservesCooperativePeerClosureWithoutPolling()
    {
        string name = "process_close_" + Guid.NewGuid().ToString("N");
        await using var server = SharedMemoryTransport.CreateServerEndpoint(name, 256);
        using var peer = StartPeer("close", name, 0);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            Assert.Equal("READY", await peer.StandardOutput.ReadLineAsync(timeout.Token));
            await server.DisposeAsync();
            await peer.WaitForExitAsync(timeout.Token);
            Assert.Equal(0, peer.ExitCode);
            Assert.Contains("CLOSED", await peer.StandardOutput.ReadToEndAsync(timeout.Token));
            Assert.Equal(string.Empty, await peer.StandardError.ReadToEndAsync(timeout.Token));
        }
        finally { if (!peer.HasExited) peer.Kill(entireProcessTree: true); }
    }

    private static Process StartPeer(string mode, string name, int bytes)
    {
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        string dll = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../Kable.SharedMemory.TestPeer/bin", configuration,
            "net10.0-windows/Kable.SharedMemory.TestPeer.dll"));
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string argument in new[] { dll, mode, name, bytes.ToString() }) start.ArgumentList.Add(argument);
        return Process.Start(start) ?? throw new InvalidOperationException("Cannot start the IPC test peer.");
    }
}
