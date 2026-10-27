using System.Buffers;
using Kable.SharedMemory;

if (args.Length == 3 && args[0].StartsWith("managed-", StringComparison.Ordinal))
{
    await ManagedPeer.RunAsync(args);
    return;
}

if (args.Length != 3) throw new ArgumentException("Expected echo|close channel byte-count.");
await using var context = SharedMemoryTransport.ConnectClientEndpoint(args[1]);
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
Console.WriteLine("READY");
if (args[0] == "echo")
{
    var payload = new byte[int.Parse(args[2])];
    int offset = 0;
    while (offset < payload.Length)
    {
        var result = await context.Input.ReadAsync(timeout.Token);
        int count = (int)Math.Min(result.Buffer.Length, payload.Length - offset);
        result.Buffer.Slice(0, count).CopyTo(payload.AsSpan(offset));
        offset += count;
        context.Input.AdvanceTo(result.Buffer.GetPosition(count));
        if (result.IsCompleted && offset < payload.Length) throw new EndOfStreamException();
    }
    await context.Output.WriteAsync(payload, timeout.Token);
    Console.WriteLine("SENT");
    // Parent acknowledges consumption before this process closes the shared mapping.
    if (await Console.In.ReadLineAsync(timeout.Token) != "STOP") throw new IOException("Missing stop acknowledgement.");
    Console.WriteLine("DONE");
}
else if (args[0] == "close")
{
    var result = await context.Input.ReadAsync(timeout.Token);
    context.Input.AdvanceTo(result.Buffer.End);
    if (!result.IsCompleted || !context.ConnectionClosed.IsCancellationRequested)
        throw new IOException("Peer close was not propagated.");
    Console.WriteLine("CLOSED");
}
else throw new ArgumentException("Unknown peer mode.");
