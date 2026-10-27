using System.Buffers;
using System.Text;
using Kable.SharedMemory.Connections;

internal static class ManagedPeer
{
    internal static async Task RunAsync(string[] args)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var context = await new SharedMemoryClientFactory(args[1]).ConnectAsync(timeout.Token);
        Console.WriteLine("READY");
        if (args[0] == "managed-hold")
            await context.Output.WriteAsync(Encoding.ASCII.GetBytes("EXEC\n"), timeout.Token);
        else if (args[0] == "managed-echo")
        {
            var payload = new byte[int.Parse(args[2])];
            int offset = 0;
            while (offset < payload.Length)
            {
                var read = await context.Input.ReadAsync(timeout.Token);
                int count = (int)Math.Min(read.Buffer.Length, payload.Length - offset);
                read.Buffer.Slice(0, count).CopyTo(payload.AsSpan(offset));
                offset += count;
                context.Input.AdvanceTo(read.Buffer.GetPosition(count));
                if (read.IsCompleted && offset < payload.Length) throw new EndOfStreamException();
            }
            await context.Output.WriteAsync(payload, timeout.Token);
            Console.WriteLine("SENT");
        }
        else throw new ArgumentException("Unknown managed peer mode.");
        if (await Console.In.ReadLineAsync(timeout.Token) != "STOP") throw new IOException("Missing stop acknowledgement.");
        Console.WriteLine("DONE");
    }
}
