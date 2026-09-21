namespace Kable.Engine.Profiles;

using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 장비 통신 물리 옵션 베이스
/// </summary>
public abstract class KableProfileConfigBase
{
    public string? Endpoint { get; set; } // "192.168.1.100:9000", "COM3:9600", or "pipe://local_pipe"
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 9000;
    public string SerialPort { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public string PipeName { get; set; } = "hardware_pipe";
    public bool IsSerial { get; set; }
    public bool IsNamedPipe { get; set; }
    public byte Delimiter { get; set; } = 0x0A; // '\n'
    public Encoding? Encoding { get; set; }
    public TimeSpan DefaultCommandTimeout { get; set; } = TimeSpan.FromSeconds(3);

    public TConfig UseTcp<TConfig>(string host, int port) where TConfig : KableProfileConfigBase
    {
        Host = host;
        Port = port;
        IsSerial = false;
        IsNamedPipe = false;
        return (TConfig)this;
    }

    public TConfig UseSerial<TConfig>(string serialPort, int baudRate = 9600) where TConfig : KableProfileConfigBase
    {
        SerialPort = serialPort;
        BaudRate = baudRate;
        IsSerial = true;
        IsNamedPipe = false;
        return (TConfig)this;
    }

    public TConfig UseNamedPipe<TConfig>(string pipeName) where TConfig : KableProfileConfigBase
    {
        PipeName = pipeName;
        IsNamedPipe = true;
        IsSerial = false;
        return (TConfig)this;
    }

    public TConfig WithDelimiter<TConfig>(byte delimiter) where TConfig : KableProfileConfigBase
    {
        Delimiter = delimiter;
        return (TConfig)this;
    }

    public TConfig WithTimeout<TConfig>(TimeSpan timeout) where TConfig : KableProfileConfigBase
    {
        DefaultCommandTimeout = timeout;
        return (TConfig)this;
    }

    public TConfig WithEncoding<TConfig>(Encoding encoding) where TConfig : KableProfileConfigBase
    {
        Encoding = encoding;
        return (TConfig)this;
    }
}

/// <summary>
/// 사용자가 정의한 장비별 Enum(상시/수시)을 키로 사용하는 제네릭 통신 프로파일 설정
/// </summary>
public sealed class KableProfileConfig<TPeriodic, TAperiodic> : KableProfileConfigBase
    where TPeriodic : struct, Enum
    where TAperiodic : struct, Enum
{
    public Dictionary<TPeriodic, (string RawCommand, TimeSpan Interval)> PeriodicCommands { get; } = new();
    public Dictionary<TAperiodic, string> AperiodicCommands { get; } = new();

    public KableProfileConfig<TPeriodic, TAperiodic> UseTcp(string host, int port)
        => UseTcp<KableProfileConfig<TPeriodic, TAperiodic>>(host, port);

    public KableProfileConfig<TPeriodic, TAperiodic> UseSerial(string serialPort, int baudRate = 9600)
        => UseSerial<KableProfileConfig<TPeriodic, TAperiodic>>(serialPort, baudRate);

    public KableProfileConfig<TPeriodic, TAperiodic> UseNamedPipe(string pipeName)
        => UseNamedPipe<KableProfileConfig<TPeriodic, TAperiodic>>(pipeName);

    public KableProfileConfig<TPeriodic, TAperiodic> WithDelimiter(byte delimiter)
        => WithDelimiter<KableProfileConfig<TPeriodic, TAperiodic>>(delimiter);

    public KableProfileConfig<TPeriodic, TAperiodic> WithTimeout(TimeSpan timeout)
        => WithTimeout<KableProfileConfig<TPeriodic, TAperiodic>>(timeout);

    public KableProfileConfig<TPeriodic, TAperiodic> AddPeriodic(TPeriodic command, string rawProtocolCommand, TimeSpan interval)
    {
        PeriodicCommands[command] = (rawProtocolCommand, interval);
        return this;
    }

    public KableProfileConfig<TPeriodic, TAperiodic> AddAperiodic(TAperiodic command, string rawProtocolCommand)
    {
        AperiodicCommands[command] = rawProtocolCommand;
        return this;
    }
}

/// <summary>
/// 문자열 기반 상시/수시 통신 프로파일 설정 (기본 문자열 타입)
/// </summary>
public sealed class KableProfileConfig : KableProfileConfigBase
{
    public Dictionary<string, TimeSpan> PeriodicCommands { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> CommandAliases { get; } = new(StringComparer.OrdinalIgnoreCase);

    public KableProfileConfig UseTcp(string host, int port)
        => UseTcp<KableProfileConfig>(host, port);

    public KableProfileConfig UseSerial(string serialPort, int baudRate = 9600)
        => UseSerial<KableProfileConfig>(serialPort, baudRate);

    public KableProfileConfig UseNamedPipe(string pipeName)
        => UseNamedPipe<KableProfileConfig>(pipeName);

    public KableProfileConfig WithDelimiter(byte delimiter)
        => WithDelimiter<KableProfileConfig>(delimiter);

    public KableProfileConfig WithTimeout(TimeSpan timeout)
        => WithTimeout<KableProfileConfig>(timeout);

    public KableProfileConfig AddPeriodic(string command, TimeSpan interval)
    {
        PeriodicCommands[command] = interval;
        return this;
    }

    public KableProfileConfig AddCommand(string alias, string rawCommand)
    {
        CommandAliases[alias] = rawCommand;
        return this;
    }
}
