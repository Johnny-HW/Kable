namespace Kable.Localization;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Kable 기본 다국어 로컬라이저 구현체 (영어, 한국어, 중국어 간체/번체, 일본어, 독일어, 프랑스어 지원)
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
        string cultureKey = GetMatchingCultureKey(_currentCulture);
        if (_errorDictionary.TryGetValue(cultureKey, out var langDict) && langDict.TryGetValue(code, out var template))
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
        string cultureKey = GetMatchingCultureKey(_currentCulture);
        if (_stringDictionary.TryGetValue(cultureKey, out var langDict) && langDict.TryGetValue(key, out var template))
        {
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        if (_stringDictionary.TryGetValue("en", out var enDict) && enDict.TryGetValue(key, out var enTemplate))
        {
            return args.Length > 0 ? string.Format(enTemplate, args) : enTemplate;
        }

        return key;
    }

    public void RegisterCustomError(string cultureCode, KableErrorCode code, string messageTemplate)
    {
        if (string.IsNullOrWhiteSpace(cultureCode)) throw new ArgumentNullException(nameof(cultureCode));
        if (string.IsNullOrWhiteSpace(messageTemplate)) throw new ArgumentNullException(nameof(messageTemplate));

        if (!_errorDictionary.TryGetValue(cultureCode, out var dict))
        {
            dict = new Dictionary<KableErrorCode, string>();
            _errorDictionary[cultureCode] = dict;
        }
        dict[code] = messageTemplate;
    }

    public void RegisterCustomString(string cultureCode, string key, string valueTemplate)
    {
        if (string.IsNullOrWhiteSpace(cultureCode)) throw new ArgumentNullException(nameof(cultureCode));
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
        if (string.IsNullOrWhiteSpace(valueTemplate)) throw new ArgumentNullException(nameof(valueTemplate));

        if (!_stringDictionary.TryGetValue(cultureCode, out var dict))
        {
            dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _stringDictionary[cultureCode] = dict;
        }
        dict[key] = valueTemplate;
    }

    private string GetMatchingCultureKey(CultureInfo culture)
    {
        string name = culture.Name;
        if (name.StartsWith("ko", StringComparison.OrdinalIgnoreCase)) return "ko";
        if (string.Equals(name, "zh-TW", StringComparison.OrdinalIgnoreCase) || 
            string.Equals(name, "zh-HK", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "zh-Hant", StringComparison.OrdinalIgnoreCase))
        {
            return "zh-TW";
        }
        if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "zh-CN";
        if (name.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return "ja";
        if (name.StartsWith("de", StringComparison.OrdinalIgnoreCase)) return "de";
        if (name.StartsWith("fr", StringComparison.OrdinalIgnoreCase)) return "fr";
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

        // 3. Chinese Simplified (zh-CN)
        var zhCnErrors = new Dictionary<KableErrorCode, string>
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

        // 4. Chinese Traditional (zh-TW)
        var zhTwErrors = new Dictionary<KableErrorCode, string>
        {
            [KableErrorCode.None] = "成功。",
            [KableErrorCode.DeviceDisconnected] = "硬體連線已中斷。（快速中止所有擱置的請求）",
            [KableErrorCode.DeviceTimeout] = "指令 '{0}' 在 {1}ms 後回應逾時。",
            [KableErrorCode.ProtocolViolation] = "發生通訊協定違規: {0}",
            [KableErrorCode.InvalidCast] = "預期回應類型為 '{0}'，但實際接收為 '{1}'。",
            [KableErrorCode.ConnectionFailed] = "無法與 '{0}' 建立連線。",
            [KableErrorCode.OperationCanceled] = "操作已取消。",
            [KableErrorCode.BufferOverflow] = "超出緩衝區容量。",
            [KableErrorCode.HeartbeatLost] = "心跳訊號遺失，判定連線中斷。",
            [KableErrorCode.HardwareFault] = "偵測到硬體故障: {0}"
        };

        // 5. Japanese (ja)
        var jaErrors = new Dictionary<KableErrorCode, string>
        {
            [KableErrorCode.None] = "成功。",
            [KableErrorCode.DeviceDisconnected] = "ハードウェア接続が切断されました。（保留中の全リクエストを即時中止）",
            [KableErrorCode.DeviceTimeout] = "コマンド '{0}' の応答が {1}ms 待機後にタイムアウトしました。",
            [KableErrorCode.ProtocolViolation] = "プロトコル違反が発生しました: {0}",
            [KableErrorCode.InvalidCast] = "期待された応答型 '{0}' と実際の型 '{1}' が一致しません。",
            [KableErrorCode.ConnectionFailed] = "'{0}' への接続確立に失敗しました。",
            [KableErrorCode.OperationCanceled] = "操作がキャンセルされました。",
            [KableErrorCode.BufferOverflow] = "バッファ容量の上限を超えました。",
            [KableErrorCode.HeartbeatLost] = "ハートビート信号が途絶えました。接続が切断されたと判断します。",
            [KableErrorCode.HardwareFault] = "ハードウェア障害が検出されました: {0}"
        };

        // 6. German (de)
        var deErrors = new Dictionary<KableErrorCode, string>
        {
            [KableErrorCode.None] = "Erfolgreich.",
            [KableErrorCode.DeviceDisconnected] = "Hardware-Verbindung wurde getrennt. (Alle ausstehenden Anfragen sofort abgebrochen)",
            [KableErrorCode.DeviceTimeout] = "Befehl '{0}' hat nach {1}ms eine Zeitüberschreitung verursacht.",
            [KableErrorCode.ProtocolViolation] = "Protokollverletzung aufgetreten: {0}",
            [KableErrorCode.InvalidCast] = "Erwarteter Antworttyp '{0}', jedoch '{1}' empfangen.",
            [KableErrorCode.ConnectionFailed] = "Verbindungsaufbau zu '{0}' fehlgeschlagen.",
            [KableErrorCode.OperationCanceled] = "Vorgang wurde abgebrochen.",
            [KableErrorCode.BufferOverflow] = "Pufferkapazität überschritten.",
            [KableErrorCode.HeartbeatLost] = "Heartbeat-Signal verloren. Verbindung als getrennt gewertet.",
            [KableErrorCode.HardwareFault] = "Hardwarefehler erkannt: {0}"
        };

        // 7. French (fr)
        var frErrors = new Dictionary<KableErrorCode, string>
        {
            [KableErrorCode.None] = "Succès.",
            [KableErrorCode.DeviceDisconnected] = "La connexion matérielle a été interrompue. (Annulation immédiate de toutes les requêtes)",
            [KableErrorCode.DeviceTimeout] = "La commande '{0}' a expiré après {1}ms.",
            [KableErrorCode.ProtocolViolation] = "Violation de protocole détectée: {0}",
            [KableErrorCode.InvalidCast] = "Type de réponse attendu '{0}', mais reçu '{1}'.",
            [KableErrorCode.ConnectionFailed] = "Échec de l'établissement de la connexion avec '{0}'.",
            [KableErrorCode.OperationCanceled] = "L'opération a été annulée.",
            [KableErrorCode.BufferOverflow] = "Capacité du tampon dépassée.",
            [KableErrorCode.HeartbeatLost] = "Signal de heartbeat perdu. Connexion considérée comme rompue.",
            [KableErrorCode.HardwareFault] = "Panne matérielle détectée: {0}"
        };

        _errorDictionary["en"] = enErrors;
        _errorDictionary["ko"] = koErrors;
        _errorDictionary["zh-CN"] = zhCnErrors;
        _errorDictionary["zh-TW"] = zhTwErrors;
        _errorDictionary["ja"] = jaErrors;
        _errorDictionary["de"] = deErrors;
        _errorDictionary["fr"] = frErrors;
    }
}
