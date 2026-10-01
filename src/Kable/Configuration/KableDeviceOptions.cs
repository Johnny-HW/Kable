namespace Kable.Configuration;

using System;
using System.IO.Ports;

/// <summary>
/// Kable 통신 장비 연결 및 프로토콜 기본 설정을 담는 불변 POCO 설정 모델입니다.
/// 상위 애플리케이션(JSON, INI, TOML, YAML, DB 등)의 설정 소스로부터 자유롭게 바인딩하여 사용할 수 있습니다.
/// </summary>
public record KableDeviceOptions
{
    private int? _connectTimeoutMs;
    /// <summary>
    /// 장비 고유 식별자 (로깅 및 옵저버 추적용)
    /// </summary>
    public string DeviceId { get; init; } = "DEFAULT";

    /// <summary>
    /// 전송 계층 종류 ("Tcp", "Serial", "NamedPipe", "Simulator")
    /// </summary>
    public string Transport { get; init; } = "Tcp";

    #region TCP Settings
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 9000;
    #endregion

    #region Serial Settings
    public string PortName { get; init; } = "COM1";
    public int BaudRate { get; init; } = 9600;
    public string Parity { get; init; } = "None";
    public int DataBits { get; init; } = 8;
    public string StopBits { get; init; } = "One";
    #endregion

    #region NamedPipe Settings
    public string PipeName { get; init; } = "kable_pipe";
    public string ServerName { get; init; } = ".";
    #endregion

    /// <summary>
    /// [하위 호환성 유지] ConnectTimeoutMs가 명시되지 않았을 때의 연결 제한시간 (기본 3000ms).
    /// 요청 제한시간에는 적용되지 않습니다.
    /// 보다 명확한 구성을 위해 <see cref="ConnectTimeoutMs"/> 및 <see cref="DefaultRequestTimeoutMs"/> 사용을 권장합니다.
    /// </summary>
    public int TimeoutMs { get; init; } = 3000;

    /// <summary>
    /// TCP/Named Pipe 연결 수립 제한시간. 생략 시 TimeoutMs를 사용합니다. 시리얼에는 적용되지 않습니다.
    /// </summary>
    public int ConnectTimeoutMs { get => _connectTimeoutMs ?? TimeoutMs; init => _connectTimeoutMs = value; }

    /// <summary>
    /// 개별 명령에 타임아웃이 명시되지 않았을 때 적용되는 기본 요청-응답 제한시간 (밀리초, 기본 3000ms).
    /// </summary>
    public int DefaultRequestTimeoutMs { get; init; } = 3000;

    /// <summary>
    /// 문자열 파리티(Parity)를 열거형으로 변환합니다.
    /// </summary>
    public Parity GetParity()
    {
        if (Enum.TryParse<Parity>(Parity, true, out var result))
            return result;
        return System.IO.Ports.Parity.None;
    }

    /// <summary>
    /// 문자열 정지 비트(StopBits)를 열거형으로 변환합니다.
    /// </summary>
    public StopBits GetStopBits()
    {
        if (Enum.TryParse<StopBits>(StopBits, true, out var result))
            return result;
        return System.IO.Ports.StopBits.One;
    }
}
