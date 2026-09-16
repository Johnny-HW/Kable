namespace Kable.Channels;

using System;
using System.Buffers;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using Kable.Codecs;
using Kable.Core;
using Kable.Transports;

/// <summary>
/// Kable System.IO.Pipelines 및 ModbusRtuCodec 기반 초고속 0-GC 하드웨어 통신 채널
/// DIO, FFU 등 Modbus-RTU 하드웨어 장비의 원시 통신 파이프라인 단일화 구현체
/// </summary>
public sealed class KableModbusDeviceChannel : IKableModbusChannel
{
    private readonly IConnectionFactory _factory;
    private readonly ModbusRtuCodec _codec = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _unitId;

    private IConnectionContext? _context;

    public bool IsOpen => _context != null;
    public string UnitId => _unitId;

    public KableModbusDeviceChannel(
        string portName,
        int baudRate = 19200,
        Parity parity = Parity.None,
        int dataBits = 8,
        StopBits stopBits = StopBits.One,
        string unitId = "ModbusDevice")
        : this(new SerialPortConnectionFactory(portName, baudRate, parity, dataBits, stopBits), unitId)
    {
    }

    public KableModbusDeviceChannel(IConnectionFactory factory, string unitId = "ModbusDevice")
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _unitId = unitId;
    }

    public async Task OpenAsync(CancellationToken ct = default)
    {
        if (_context == null)
        {
            _context = await _factory.ConnectAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task CloseAsync(CancellationToken ct = default)
    {
        if (_context != null)
        {
            await _context.DisposeAsync().ConfigureAwait(false);
            _context = null;
        }
    }

    [Obsolete("Use OpenAsync instead to avoid UI deadlocks.")]
    public void Open()
    {
        _context ??= _factory.ConnectAsync().GetAwaiter().GetResult();
    }

    [Obsolete("Use CloseAsync instead to avoid UI deadlocks.")]
    public void Close()
    {
        if (_context != null)
        {
            _context.DisposeAsync().GetAwaiter().GetResult();
            _context = null;
        }
    }

    public async ValueTask<byte[]> SendAndReceiveFrameAsync(byte[] requestPdu, TimeSpan timeout, CancellationToken ct = default)
    {
        if (requestPdu == null)
        {
            throw new ArgumentNullException(nameof(requestPdu));
        }

        if (_context == null)
        {
            throw new InvalidOperationException($"Channel is not open for unit '{_unitId}'. Call Open() first.");
        }

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            // 1. Kable 파이프라인으로 Modbus PDU 인코딩 및 전송 (CRC 자동 부착)
            _codec.Encode(requestPdu, _context.Output);
            var flushResult = await _context.Output.FlushAsync(cts.Token).ConfigureAwait(false);
            if (flushResult.IsCanceled || flushResult.IsCompleted)
            {
                throw new TimeoutException($"Flush failed or was canceled for unit '{_unitId}'.");
            }

            // 2. Kable PipeReader에서 완성된 Modbus 프레임 수신 및 CRC 자동 검증
            while (!cts.IsCancellationRequested)
            {
                var readResult = await _context.Input.ReadAsync(cts.Token).ConfigureAwait(false);
                var buffer = readResult.Buffer;

                if (_codec.TryDecode(ref buffer, out var responseMessage))
                {
                    _context.Input.AdvanceTo(buffer.Start, buffer.End);
                    return responseMessage.ToArray();
                }

                _context.Input.AdvanceTo(buffer.Start, buffer.End);
                if (readResult.IsCompleted || readResult.IsCanceled)
                {
                    break;
                }
            }

            throw new TimeoutException($"Communication timeout waiting for Modbus response from unit '{_unitId}'.");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_context != null)
        {
            await _context.DisposeAsync().ConfigureAwait(false);
            _context = null;
        }
        _lock.Dispose();
    }
}
