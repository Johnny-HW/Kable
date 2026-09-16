namespace Kable.Melsec;

using System;
using System.Buffers.Binary;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Kable.Engine;
using Kable.Melsec.Protocol;

/// <summary>
/// KableSession 기반의 고수준 미쓰비시 PLC 클라이언트.
/// D, W, R, M, X, Y 디바이스의 비동기 일괄 읽기/쓰기 및 에러 코드 처리를 제공합니다.
/// </summary>
public sealed class MelsecPlcClient : IMelsecPlcClient
{
    private readonly IDeviceSession<Slmp3EFrame> _session;
    private readonly ILogger<MelsecPlcClient>? _logger;

    public byte NetworkNo { get; set; }
    public byte PcNo { get; set; }
    public TimeSpan DefaultTimeout { get; set; }

    public MelsecPlcClient(
        IDeviceSession<Slmp3EFrame> session,
        IOptions<MelsecOptions> options,
        ILogger<MelsecPlcClient>? logger = null)
        : this(session, (options ?? throw new ArgumentNullException(nameof(options))).Value, logger)
    {
    }

    public MelsecPlcClient(
        IDeviceSession<Slmp3EFrame> session,
        MelsecOptions options,
        ILogger<MelsecPlcClient>? logger = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger;

        NetworkNo = options.NetworkNo;
        PcNo = options.PcNo;
        DefaultTimeout = options.Timeout;
    }

    /// <summary>
    /// 지정된 워드 디바이스(D, W, R 등)에서 count 개수만큼의 ushort 값을 일괄 읽어옵니다.
    /// </summary>
    public async Task<ushort[]> ReadWordsAsync(MelsecDeviceCode device, int headDeviceNo, ushort count, CancellationToken ct = default)
    {
        var request = Slmp3EFrame.CreateReadDeviceRequest(
            device: device,
            headDeviceNo: headDeviceNo,
            devicePoints: count,
            isBitAccess: false,
            networkNo: NetworkNo,
            pcNo: PcNo);

        var response = await _session.RequestAsync<Slmp3EFrame>(request, DefaultTimeout, ct).ConfigureAwait(false);

        if (!response.IsSuccess)
        {
            throw new InvalidOperationException($"MELSEC PLC Error EndCode: 0x{response.EndCode:X4}");
        }

        var span = response.Data.Span;
        int wordCount = span.Length / 2;
        var words = new ushort[wordCount];

        for (int i = 0; i < wordCount; i++)
        {
            words[i] = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(i * 2, 2));
        }

        return words;
    }

    /// <summary>
    /// 지정된 워드 디바이스(D, W, R 등)에 values 배열을 일괄 기록합니다.
    /// </summary>
    public async Task WriteWordsAsync(MelsecDeviceCode device, int headDeviceNo, ushort[] values, CancellationToken ct = default)
    {
        var request = Slmp3EFrame.CreateWriteWordsRequest(
            device: device,
            headDeviceNo: headDeviceNo,
            values: values,
            networkNo: NetworkNo,
            pcNo: PcNo);

        var response = await _session.RequestAsync<Slmp3EFrame>(request, DefaultTimeout, ct).ConfigureAwait(false);

        if (!response.IsSuccess)
        {
            throw new InvalidOperationException($"MELSEC PLC Error EndCode: 0x{response.EndCode:X4}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _session.DisposeAsync().ConfigureAwait(false);
    }
}
