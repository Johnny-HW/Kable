namespace Kable.Configuration;

using System;
using System.IO.Ports;

/// <summary>
/// Kable 통신 장비 연결 및 프로토콜 기본 설정을 담는 불변 POCO 설정 모델입니다.
/// 상위 애플리케이션(JSON, INI, TOML, YAML, DB 등)의 설정 소스로부터 자유롭게 바인딩하여 사용할 수 있습니다.
/// </summary>
public record KableDeviceOptions
{
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
    /// 기본 요청-응답 타임아웃 (밀리초, 기본 3000ms)
    /// </summary>
    public int TimeoutMs { get; init; } = 3000;

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
