namespace Kable.Channels;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Kable 파이프라인 기반의 Modbus RTU 바이트 스트림 통신 채널 인터페이스
/// </summary>
public interface IKableModbusChannel : IAsyncDisposable
{
    /// <summary>
    /// 통신 채널 연결 여부
    /// </summary>
    bool IsOpen { get; }

    /// <summary>
    /// 채널 연결 개시
    /// </summary>
    void Open();

    /// <summary>
    /// 채널 연결 해제
    /// </summary>
    void Close();

    /// <summary>
    /// Modbus PDU 프레임 송신 및 완성된 응답 프레임 수신 (CRC 검증 자동 수행)
    /// </summary>
    ValueTask<byte[]> SendAndReceiveFrameAsync(byte[] requestPdu, TimeSpan timeout, CancellationToken ct = default);
}
