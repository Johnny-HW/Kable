namespace Kable.Localization;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Kable 기본 다국어 로컬라이저 구현체 (영어, 한국어, 중국어 지원)
/// </summary>
public sealed class KableLocalizer : IKableLocalizer
{
    public static KableLocalizer Instance { get; } = new();

    private CultureInfo _currentCulture = CultureInfo.GetCultureInfo("en-US");

    public CultureInfo CurrentCulture => _currentCulture;

    public event EventHandler<CultureInfo>? CultureChanged;

    private readonly Dictionary<string, Dictionary<KableErrorCode, string>> _errorDictionary = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> _stringDictionary = new(StringComparer.OrdinalIgnoreCase);

    public KableLocalizer()
    {
        InitializeDefaultTranslations();
    }

    public void SetCulture(CultureInfo culture)
    {
        if (culture == null) throw new ArgumentNullException(nameof(culture));
        if (!Equals(_currentCulture, culture))
        {
            _currentCulture = culture;
            CultureChanged?.Invoke(this, culture);
        }
    }

    public string GetErrorMessage(KableErrorCode code, params object[] args)
    {
        string cultureName = GetMatchingCultureName(_currentCulture);
        if (_errorDictionary.TryGetValue(cultureName, out var langDict) && langDict.TryGetValue(code, out var template))
        {
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        // Fallback to English
        if (_errorDictionary.TryGetValue("en", out var enDict) && enDict.TryGetValue(code, out var enTemplate))
        {
            return args.Length > 0 ? string.Format(enTemplate, args) : enTemplate;
        }

        return $"[Error {code}]";
    }

    public string GetString(string key, params object[] args)
    {
        string cultureName = GetMatchingCultureName(_currentCulture);
        if (_stringDictionary.TryGetValue(cultureName, out var langDict) && langDict.TryGetValue(key, out var template))
        {
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        if (_stringDictionary.TryGetValue("en", out var enDict) && enDict.TryGetValue(key, out var enTemplate))
        {
            return args.Length > 0 ? string.Format(enTemplate, args) : enTemplate;
        }

        return key;
    }

    private string GetMatchingCultureName(CultureInfo culture)
    {
        if (culture.Name.StartsWith("ko", StringComparison.OrdinalIgnoreCase)) return "ko";
        if (culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "zh";
        return "en";
    }

    private void InitializeDefaultTranslations()
    {
        // 1. English (en)
        var enErrors = new Dictionary<KableErrorCode, string>
        {
            [KableErrorCode.None] = "Success.",
            [KableErrorCode.DeviceDisconnected] = "Hardware connection has been disconnected. (Fail-fast aborting all pending requests)",
            [KableErrorCode.DeviceTimeout] = "Command '{0}' timed out after {1}ms.",
            [KableErrorCode.ProtocolViolation] = "Protocol violation occurred: {0}",
            [KableErrorCode.InvalidCast] = "Expected response type '{0}', but received '{1}'.",
            [KableErrorCode.ConnectionFailed] = "Failed to establish connection to '{0}'.",
            [KableErrorCode.OperationCanceled] = "Operation was canceled.",
            [KableErrorCode.BufferOverflow] = "Buffer capacity exceeded limit.",
            [KableErrorCode.HeartbeatLost] = "Heartbeat ping lost. Connection deemed dead.",
            [KableErrorCode.HardwareFault] = "Hardware fault detected: {0}"
        };

        // 2. Korean (ko)
        var koErrors = new Dictionary<KableErrorCode, string>
        {
            [KableErrorCode.None] = "성공.",
            [KableErrorCode.DeviceDisconnected] = "하드웨어 연결이 끊어졌습니다. (모든 대기 요청 즉시 취소)",
            [KableErrorCode.DeviceTimeout] = "명령어 '{0}'이(가) {1}ms 동안 응답이 없어 시간 초과되었습니다.",
            [KableErrorCode.ProtocolViolation] = "프로토콜 규약 위반이 발생했습니다: {0}",
            [KableErrorCode.InvalidCast] = "예상된 응답 타입은 '{0}'이지만, 실제 수신된 타입은 '{1}'입니다.",
            [KableErrorCode.ConnectionFailed] = "'{0}'(으)로의 연결 수립에 실패했습니다.",
            [KableErrorCode.OperationCanceled] = "작업이 취소되었습니다.",
            [KableErrorCode.BufferOverflow] = "버퍼 용량을 초과했습니다.",
            [KableErrorCode.HeartbeatLost] = "하트비트 신호가 끊겼습니다. 연결이 유실된 것으로 간주합니다.",
            [KableErrorCode.HardwareFault] = "하드웨어 결함이 감지되었습니다: {0}"
        };

        // 3. Chinese Simplified (zh)
        var zhErrors = new Dictionary<KableErrorCode, string>
        {
            [KableErrorCode.None] = "成功。",
            [KableErrorCode.DeviceDisconnected] = "硬件连接已断开。（快速中止所有挂起的请求）",
            [KableErrorCode.DeviceTimeout] = "命令 '{0}' 在 {1}ms 后响应超时。",
            [KableErrorCode.ProtocolViolation] = "发生协议违规: {0}",
            [KableErrorCode.InvalidCast] = "预期响应类型为 '{0}'，但实际接收到 '{1}'。",
            [KableErrorCode.ConnectionFailed] = "无法与 '{0}' 建立连接。",
            [KableErrorCode.OperationCanceled] = "操作已取消。",
            [KableErrorCode.BufferOverflow] = "超出缓冲区容量。",
            [KableErrorCode.HeartbeatLost] = "心跳信号丢失，连接判定中断。",
            [KableErrorCode.HardwareFault] = "检测到硬件故障: {0}"
        };

        _errorDictionary["en"] = enErrors;
        _errorDictionary["ko"] = koErrors;
        _errorDictionary["zh"] = zhErrors;
    }
}
