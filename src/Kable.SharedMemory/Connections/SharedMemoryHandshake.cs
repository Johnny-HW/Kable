namespace Kable.SharedMemory.Connections;

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

internal static class SharedMemoryHandshake
{
    private const uint Magic = 0x31434D53;
    private const int Version = 1;

    internal static string ControlName(string channelName)
    {
        SharedMemoryCompat.ThrowIfNullOrWhiteSpace(channelName, nameof(channelName));
#if NET5_0_OR_GREATER
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(channelName)));
#else
        using var sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(channelName));
        string hash = BitConverter.ToString(hashBytes).Replace("-", "");
#endif
        return "Kable.SharedMemory." + hash.Substring(0, 32);
    }

    internal static string SessionName(string channelName, Guid generation)
        => ControlName(channelName) + "." + generation.ToString("N");

    private static byte[] Header(int size)
    {
        var bytes = new byte[size];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, Magic);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), Version);
        return bytes;
    }

    private static void Validate(byte[] bytes)
    {
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes) != Magic ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)) != Version)
            throw new InvalidDataException("Unsupported shared memory control protocol.");
    }

    internal static async Task ReadRequestAsync(Stream pipe, CancellationToken ct)
    {
        var request = new byte[8];
        await SharedMemoryCompat.ReadExactlyAsync(pipe, request, ct).ConfigureAwait(false);
        Validate(request);
    }

    internal static ValueTask WriteRequestAsync(Stream pipe, CancellationToken ct)
        => SharedMemoryCompat.WriteAsync(pipe, Header(8), ct);

    internal static async Task WriteReplyAsync(Stream pipe, Guid generation, CancellationToken ct)
    {
        var reply = Header(24);
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        generation.TryWriteBytes(reply.AsSpan(8));
#else
        Buffer.BlockCopy(generation.ToByteArray(), 0, reply, 8, 16);
#endif
        await SharedMemoryCompat.WriteAsync(pipe, reply, ct).ConfigureAwait(false);
    }

    internal static async Task<Guid> ReadReplyAsync(Stream pipe, CancellationToken ct)
    {
        var reply = new byte[24];
        await SharedMemoryCompat.ReadExactlyAsync(pipe, reply, ct).ConfigureAwait(false);
        Validate(reply);
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        var generation = new Guid(reply.AsSpan(8));
#else
        var guidBytes = new byte[16];
        Buffer.BlockCopy(reply, 8, guidBytes, 0, 16);
        var generation = new Guid(guidBytes);
#endif
        if (generation == Guid.Empty) throw new InvalidDataException("Missing shared memory generation.");
        return generation;
    }

    internal static async Task ReadAckAsync(Stream pipe, CancellationToken ct)
    {
        var ack = new byte[1];
        await SharedMemoryCompat.ReadExactlyAsync(pipe, ack, ct).ConfigureAwait(false);
        if (ack[0] != 1) throw new InvalidDataException("Invalid shared memory acknowledgement.");
    }
}
