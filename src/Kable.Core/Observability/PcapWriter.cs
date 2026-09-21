namespace Kable.Observability;

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Wireshark에서 직접 열람할 수 있는 표준 PCAP (libpcap) 바이너리 포맷 라이터.
/// 통신 인터페이스 종류에 무관하게 LINKTYPE_RAW(101) 또는 LINKTYPE_USER0(147)로 덤프를 생성합니다.
/// </summary>
public sealed class PcapWriter : IAsyncDisposable
{
    private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Stream _stream;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _headerWritten;

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
        if (_headerWritten) return;

        await _lock.WaitAsync(ct);
        try
        {
            if (_headerWritten) return;

            byte[] header = new byte[24];
            BitConverter.GetBytes(PCAP_MAGIC).CopyTo(header, 0);
            BitConverter.GetBytes(VERSION_MAJOR).CopyTo(header, 4);
            BitConverter.GetBytes(VERSION_MINOR).CopyTo(header, 6);
            BitConverter.GetBytes((int)0).CopyTo(header, 8);  // thiszone
            BitConverter.GetBytes((uint)0).CopyTo(header, 12); // sigfigs
            BitConverter.GetBytes(SNAPLEN).CopyTo(header, 16); // snaplen
            BitConverter.GetBytes(LINKTYPE_RAW).CopyTo(header, 20); // network

            await _stream.WriteAsync(header, 0, header.Length, ct);
            await _stream.FlushAsync(ct);
            _headerWritten = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 패킷 1건을 PCAP 패킷 레코드 헤더(16 bytes)와 함께 스트림에 씁니다.
    /// </summary>
    public async ValueTask WritePacketAsync(ReadOnlyMemory<byte> payload, DateTime timestampUtc, CancellationToken ct = default)
    {
        if (!_headerWritten)
        {
            await EnsureHeaderWrittenAsync(ct);
        }

        long unixMicro = (timestampUtc.Ticks - UnixEpoch.Ticks) / 10;
        uint tsSec = (uint)(unixMicro / 1_000_000);
        uint tsUsec = (uint)(unixMicro % 1_000_000);
        uint capLen = (uint)Math.Min(payload.Length, (int)SNAPLEN);
        uint origLen = (uint)payload.Length;

        byte[] pktHeader = new byte[16];
        BitConverter.GetBytes(tsSec).CopyTo(pktHeader, 0);
        BitConverter.GetBytes(tsUsec).CopyTo(pktHeader, 4);
        BitConverter.GetBytes(capLen).CopyTo(pktHeader, 8);
        BitConverter.GetBytes(origLen).CopyTo(pktHeader, 12);

        await _lock.WaitAsync(ct);
        try
        {
            await _stream.WriteAsync(pktHeader, 0, pktHeader.Length, ct);
            if (capLen > 0)
            {
                byte[] buffer = payload.Slice(0, (int)capLen).ToArray();
                await _stream.WriteAsync(buffer, 0, buffer.Length, ct);
            }
            await _stream.FlushAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stream.FlushAsync();
        _stream.Dispose();
        _lock.Dispose();
    }
}
