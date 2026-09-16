namespace Kable.Melsec;

using System;
using System.Threading;
using System.Threading.Tasks;
using Kable.Melsec.Protocol;

public interface IMelsecPlcClient : IAsyncDisposable
{
    byte NetworkNo { get; set; }
    byte PcNo { get; set; }
    TimeSpan DefaultTimeout { get; set; }

    Task StartAsync(CancellationToken ct = default);
    Task<ushort[]> ReadWordsAsync(MelsecDeviceCode device, int headDeviceNo, ushort count, CancellationToken ct = default);
    Task WriteWordsAsync(MelsecDeviceCode device, int headDeviceNo, ushort[] values, CancellationToken ct = default);
}

