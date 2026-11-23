namespace Kable.Observability;

using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Wireshark에서 직접 열람할 수 있는 표준 PCAP (libpcap) 바이너리 포맷 라이터.
/// 통신 인터페이스 종류에 무관하게 LINKTYPE_RAW(101) 또는 LINKTYPE_USER0(147)로 덤프를 생성합니다.
/// </summary>
public sealed class PcapWriter : IAsyncDisposable
{
    private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Stream _stream;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly byte[] _globalHeader = new byte[24];
    private readonly byte[] _packetHeader = new byte[16];
    private bool _headerWritten;
    private int _disposed;

    // PCAP Magic Number: 0xA1B2C3D4 (Standard Microsecond resolution)
    private const uint PCAP_MAGIC = 0xA1B2C3D4;
    private const ushort VERSION_MAJOR = 2;
    private const ushort VERSION_MINOR = 4;
    private const uint SNAPLEN = 65535;
    private const uint LINKTYPE_RAW = 101; // Raw IP / Byte stream

    public PcapWriter(Stream outputStream)
    {
        _stream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
    }

    /// <summary>
    /// PCAP 파일 전역 헤더 (Global Header, 24 bytes)를 작성합니다.
    /// </summary>
    public async ValueTask EnsureHeaderWrittenAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (Volatile.Read(ref _headerWritten)) return;

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (_headerWritten) return;
            await WriteGlobalHeaderCoreAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 패킷 1건을 PCAP 패킷 레코드 헤더(16 bytes)와 함께 스트림에 씁니다.
    /// </summary>
    public ValueTask WritePacketAsync(ReadOnlyMemory<byte> payload, DateTime timestampUtc, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        int capLen = Math.Min(payload.Length, (int)SNAPLEN);
#if NET6_0_OR_GREATER
        return WriteCapturedMemoryAsync(payload.Slice(0, capLen), payload.Length, timestampUtc, ct);
#else
        var captured = payload.Slice(0, capLen).ToArray();
        return WriteCapturedPacketAsync(captured, payload.Length, timestampUtc, ct);
#endif
    }

    internal ValueTask WriteCapturedPacketAsync(byte[] payload, int originalLength,
        DateTime timestampUtc, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        int capLen = Math.Min(payload.Length, (int)SNAPLEN);
#if NET6_0_OR_GREATER
        return WriteCapturedMemoryAsync(payload.AsMemory(0, capLen), originalLength, timestampUtc, ct);
#else
        return WriteCapturedArrayAsync(payload, capLen, originalLength, timestampUtc, ct);
#endif
    }

#if NET6_0_OR_GREATER
    private async ValueTask WriteCapturedMemoryAsync(ReadOnlyMemory<byte> captured, int originalLength,
        DateTime timestampUtc, CancellationToken ct)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (!_headerWritten)
            {
                await WriteGlobalHeaderCoreAsync(ct).ConfigureAwait(false);
            }

            WritePacketHeader(_packetHeader, captured.Length, originalLength, timestampUtc);
            await _stream.WriteAsync(_packetHeader.AsMemory(), ct).ConfigureAwait(false);
            if (!captured.IsEmpty)
            {
                await _stream.WriteAsync(captured, ct).ConfigureAwait(false);
            }
            await _stream.FlushAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }
#else
    private async ValueTask WriteCapturedArrayAsync(byte[] payload, int capLen, int originalLength,
        DateTime timestampUtc, CancellationToken ct)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (!_headerWritten)
            {
                await WriteGlobalHeaderCoreAsync(ct).ConfigureAwait(false);
            }

            WritePacketHeader(_packetHeader, capLen, originalLength, timestampUtc);
            await _stream.WriteAsync(_packetHeader, 0, _packetHeader.Length, ct).ConfigureAwait(false);
            if (capLen > 0)
            {
                await _stream.WriteAsync(payload, 0, capLen, ct).ConfigureAwait(false);
            }
            await _stream.FlushAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }
#endif

    private async ValueTask WriteGlobalHeaderCoreAsync(CancellationToken ct)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(_globalHeader.AsSpan(0, 4), PCAP_MAGIC);
        BinaryPrimitives.WriteUInt16LittleEndian(_globalHeader.AsSpan(4, 2), VERSION_MAJOR);
        BinaryPrimitives.WriteUInt16LittleEndian(_globalHeader.AsSpan(6, 2), VERSION_MINOR);
        BinaryPrimitives.WriteInt32LittleEndian(_globalHeader.AsSpan(8, 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(_globalHeader.AsSpan(12, 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(_globalHeader.AsSpan(16, 4), SNAPLEN);
        BinaryPrimitives.WriteUInt32LittleEndian(_globalHeader.AsSpan(20, 4), LINKTYPE_RAW);

        await _stream.WriteAsync(_globalHeader, 0, _globalHeader.Length, ct).ConfigureAwait(false);
        await _stream.FlushAsync(ct).ConfigureAwait(false);
        Volatile.Write(ref _headerWritten, true);
    }

    private static void WritePacketHeader(byte[] buffer, int capLen, int originalLength, DateTime timestamp)
    {
        var utc = timestamp.Kind == DateTimeKind.Utc ? timestamp : timestamp.ToUniversalTime();
        long unixMicro = Math.Max(0, (utc.Ticks - UnixEpoch.Ticks) / 10);
        uint tsSec = (uint)(unixMicro / 1_000_000);
        uint tsUsec = (uint)(unixMicro % 1_000_000);

        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0, 4), tsSec);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4, 4), tsUsec);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(8, 4), (uint)capLen);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(12, 4), (uint)originalLength);
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(PcapWriter));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            try { await _stream.FlushAsync().ConfigureAwait(false); }
            finally { _stream.Dispose(); }
        }
        finally
        {
            _lock.Release();
            _lock.Dispose();
        }
    }
}
