namespace Kable.SharedMemory.Tests.Connections;

using System.Diagnostics;
using System.Text;
using Kable.SharedMemory.Connections;
using Kable.SharedMemory.Memory;
using Kable.SharedMemory.Tests.Transports;
using Xunit;

public class UiRestartProcessTests
{
    [Fact]
    public async Task UiKilledAndRestarted_DeviceSessionAndInFlightCommandSurvive()
    {
        await using var device = await DeviceSessionFixture.CreateAsync();
        string channel = "ui_restart_" + Guid.NewGuid().ToString("N");
        await using var listener = new SharedMemoryServerListener(channel, 256);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var firstAccept = listener.AcceptAsync(timeout.Token).AsTask();
        using var firstUi = StartPeer("managed-hold", channel, 0);
        try
        {
            Assert.Equal("READY", await firstUi.StandardOutput.ReadLineAsync(timeout.Token));
            var firstServer = (SharedMemoryConnectionContext)await firstAccept;
            using var retainedOldMapping = SharedMemoryRingBuffer.Open(
                SharedMemoryHandshake.SessionName(channel, firstServer.SessionGeneration!.Value) + "_s2c");
            Assert.Equal(Encoding.ASCII.GetBytes("EXEC\n"),
                await TransportTestSupport.ReceiveAsync(firstServer.Input, 5, timeout.Token));
            Task<string> deviceCommand = device.Session.RequestAsync<string>("EXEC", TimeSpan.FromSeconds(30)).AsTask();
            await device.CommandStarted.Task.WaitAsync(timeout.Token);

            firstUi.Kill(entireProcessTree: true);
            await firstUi.WaitForExitAsync(timeout.Token);
            await ManagedConnectionTests.WaitClosedAsync(firstServer.ConnectionClosed);
            Assert.True(device.Session.IsConnected);
            Assert.False(deviceCommand.IsCompleted);

            var secondAccept = listener.AcceptAsync(timeout.Token).AsTask();
            using var secondUi = StartPeer("managed-echo", channel, 32);
            try
            {
                Assert.Equal("READY", await secondUi.StandardOutput.ReadLineAsync(timeout.Token));
                var secondServer = (SharedMemoryConnectionContext)await secondAccept;
                Assert.NotEqual(firstServer.SessionGeneration, secondServer.SessionGeneration);
                Assert.True(retainedOldMapping.IsClosed);
                device.CompleteCommand.SetResult();
                Assert.Equal("DONE", await deviceCommand.WaitAsync(timeout.Token));
                Assert.Equal(1, device.Executions);
                Assert.Equal(1, device.ConnectCount);

                // A late result stays bound to the old IPC; it must never be sent through a global current-UI slot.
                try { await firstServer.Output.WriteAsync(Encoding.ASCII.GetBytes("OLD_RESULT"), timeout.Token); }
                catch (InvalidOperationException) { }
                catch (IOException) { }
                byte[] currentSnapshot = TransportTestSupport.Pattern(32, 91);
                await secondServer.Output.WriteAsync(currentSnapshot, timeout.Token);
                Assert.Equal(currentSnapshot,
                    await TransportTestSupport.ReceiveAsync(secondServer.Input, 32, timeout.Token));
                Assert.Equal("READY", await device.Session.RequestAsync<string>("STATUS", TimeSpan.FromSeconds(5)));
                Assert.Equal(1, device.ConnectCount);
                await secondUi.StandardInput.WriteLineAsync("STOP");
                await secondUi.WaitForExitAsync(timeout.Token);
                Assert.Equal(0, secondUi.ExitCode);
                Assert.Equal(string.Empty, await secondUi.StandardError.ReadToEndAsync(timeout.Token));
            }
            finally { if (!secondUi.HasExited) secondUi.Kill(entireProcessTree: true); }
        }
        finally { if (!firstUi.HasExited) firstUi.Kill(entireProcessTree: true); }
    }

    private static Process StartPeer(string mode, string channel, int bytes)
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
        foreach (string argument in new[] { dll, mode, channel, bytes.ToString() }) start.ArgumentList.Add(argument);
        return Process.Start(start) ?? throw new InvalidOperationException("Cannot start the UI test peer.");
    }
}
