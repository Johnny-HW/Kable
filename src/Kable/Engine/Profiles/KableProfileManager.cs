namespace Kable.Engine.Profiles;

using System;
using System.Threading;
using System.Threading.Tasks;
using Kable.Observability;
using Kable.Simple;

/// <summary>
/// 상시/수시 명령 목록을 등록받아 완전 자동화된 하드웨어 통신을 제공하는 엔진 매니저
/// </summary>
public static class KableProfileManager
{
    /// <summary>
    /// 장비별 Enum 타입 기반 클라이언트 연결 및 자동 상시 폴링 시작
    /// </summary>
    public static async ValueTask<IKableProfileClient<TPeriodic, TAperiodic>> ConnectAsync<TPeriodic, TAperiodic>(
        KableProfileConfig<TPeriodic, TAperiodic> config,
        ICommObserver? observer = null,
        CancellationToken ct = default)
        where TPeriodic : struct, Enum
        where TAperiodic : struct, Enum
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        var simpleClient = await CreateSimpleClientAsync(config, observer, ct).ConfigureAwait(false);
        var profileClient = new GenericKableProfileClientImpl<TPeriodic, TAperiodic>(simpleClient, config, observer);
        profileClient.Start();
        return profileClient;
    }

    /// <summary>
    /// 문자열 기반 클라이언트 연결 및 자동 상시 폴링 시작
    /// </summary>
    public static async ValueTask<IKableProfileClient> ConnectAsync(
        KableProfileConfig config,
        ICommObserver? observer = null,
        CancellationToken ct = default)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        var simpleClient = await CreateSimpleClientAsync(config, observer, ct).ConfigureAwait(false);
        var profileClient = new StringKableProfileClientImpl(simpleClient, config, observer);
        profileClient.Start();
        return profileClient;
    }

    private static async ValueTask<IKableSimpleClient> CreateSimpleClientAsync(
        KableProfileConfigBase config,
        ICommObserver? observer,
        CancellationToken ct)
    {
        var endpoint = config.Endpoint;
        if (!string.IsNullOrEmpty(endpoint) && endpoint != null)
        {
            if (endpoint.StartsWith("pipe://", StringComparison.OrdinalIgnoreCase))
            {
                var pipeName = endpoint.Substring("pipe://".Length);
                return await KableSimple.OpenNamedPipeAsync(pipeName, delimiter: config.Delimiter, encoding: config.Encoding, observer: observer, ct: ct).ConfigureAwait(false);
            }
            if (endpoint.StartsWith(@"\\.\pipe\", StringComparison.OrdinalIgnoreCase))
            {
                var pipeName = endpoint.Substring(@"\\.\pipe\".Length);
                return await KableSimple.OpenNamedPipeAsync(pipeName, delimiter: config.Delimiter, encoding: config.Encoding, observer: observer, ct: ct).ConfigureAwait(false);
            }
            if (endpoint.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
            {
                var parts = endpoint.Split(':');
                var portName = parts[0];
                var baud = parts.Length > 1 && int.TryParse(parts[1], out var b) ? b : 9600;
                return await KableSimple.OpenSerialAsync(portName, baud, delimiter: config.Delimiter, encoding: config.Encoding, observer: observer, ct: ct).ConfigureAwait(false);
            }

            var tcpParts = endpoint.Split(':');
            var host = tcpParts[0];
            var port = tcpParts.Length > 1 && int.TryParse(tcpParts[1], out var p) ? p : 9000;
            return await KableSimple.OpenTcpAsync(host, port, delimiter: config.Delimiter, encoding: config.Encoding, observer: observer, ct: ct).ConfigureAwait(false);
        }

        if (config.IsNamedPipe)
        {
            return await KableSimple.OpenNamedPipeAsync(config.PipeName, delimiter: config.Delimiter, encoding: config.Encoding, observer: observer, ct: ct).ConfigureAwait(false);
        }

        if (config.IsSerial)
        {
            return await KableSimple.OpenSerialAsync(config.SerialPort, config.BaudRate, delimiter: config.Delimiter, encoding: config.Encoding, observer: observer, ct: ct).ConfigureAwait(false);
        }

        return await KableSimple.OpenTcpAsync(config.Host, config.Port, delimiter: config.Delimiter, encoding: config.Encoding, observer: observer, ct: ct).ConfigureAwait(false);
    }
}
