namespace Kable.SharedMemory;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

internal static class SharedMemoryCompat
{
    internal static void ThrowIfNullOrWhiteSpace(string? argument, string paramName)
    {
#if NET8_0_OR_GREATER
        ArgumentException.ThrowIfNullOrWhiteSpace(argument, paramName);
#else
        if (argument is null) throw new ArgumentNullException(paramName);
        if (string.IsNullOrWhiteSpace(argument))
            throw new ArgumentException("The value cannot be an empty string or composed entirely of whitespace.", paramName);
#endif
    }

    internal static void ThrowIfNegativeOrZero(int value, string paramName)
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, paramName);
#else
        if (value <= 0)
            throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} ('{value}') must be a non-negative and non-zero value.");
#endif
    }

    internal static void ThrowIfLessThan(int value, int other, string paramName)
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfLessThan(value, other, paramName);
#else
        if (value < other)
            throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} ('{value}') must be greater than or equal to '{other}'.");
#endif
    }

    internal static long GetTickCount64()
    {
#if NETCOREAPP3_0_OR_GREATER || NET5_0_OR_GREATER
        return Environment.TickCount64;
#else
        return (long)(Stopwatch.GetTimestamp() * (1000.0 / Stopwatch.Frequency));
#endif
    }

    internal static int InterlockedOr(ref int location, int value)
    {
#if NET5_0_OR_GREATER
        return Interlocked.Or(ref location, value);
#else
        int current = Volatile.Read(ref location);
        while (true)
        {
            int next = current | value;
            int previous = Interlocked.CompareExchange(ref location, next, current);
            if (previous == current) return previous;
            current = previous;
        }
#endif
    }

    internal static ValueTask<int> ReadAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        return stream.ReadAsync(buffer, ct);
#else
        return new ValueTask<int>(stream.ReadAsync(buffer, 0, buffer.Length, ct));
#endif
    }

    internal static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
#if NET7_0_OR_GREATER
        await stream.ReadExactlyAsync(buffer, ct).ConfigureAwait(false);
#else
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer, offset, buffer.Length - offset, ct).ConfigureAwait(false);
            if (read == 0) throw new EndOfStreamException("Unable to read beyond the end of the stream.");
            offset += read;
        }
#endif
    }

    internal static ValueTask WriteAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        return stream.WriteAsync(buffer, ct);
#else
        return new ValueTask(stream.WriteAsync(buffer, 0, buffer.Length, ct));
#endif
    }
}
