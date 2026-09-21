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
        if (name.StartsWith("es", StringComparison.OrdinalIgnoreCase)) return "es";
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

        // 8. Spanish (es)
        var esErrors = new Dictionary<KableErrorCode, string>
        {
            [KableErrorCode.None] = "Éxito.",
            [KableErrorCode.DeviceDisconnected] = "La conexión de hardware se ha desconectado. (Abortando solicitudes pendientes)",
            [KableErrorCode.DeviceTimeout] = "El comando '{0}' agotó el tiempo de espera tras {1}ms.",
            [KableErrorCode.ProtocolViolation] = "Violación de protocolo detectada: {0}",
            [KableErrorCode.InvalidCast] = "Se esperaba el tipo de respuesta '{0}', pero se recibió '{1}'.",
            [KableErrorCode.ConnectionFailed] = "Error al establecer conexión con '{0}'.",
            [KableErrorCode.OperationCanceled] = "La operación fue cancelada.",
            [KableErrorCode.BufferOverflow] = "Capacidad del búfer superada.",
            [KableErrorCode.HeartbeatLost] = "Señal de latido perdida. Conexión declarada muerta.",
            [KableErrorCode.HardwareFault] = "Falla de hardware detectada: {0}"
        };

        _errorDictionary["en"] = enErrors;
        _errorDictionary["ko"] = koErrors;
        _errorDictionary["zh-CN"] = zhCnErrors;
        _errorDictionary["zh-TW"] = zhTwErrors;
        _errorDictionary["ja"] = jaErrors;
        _errorDictionary["de"] = deErrors;
        _errorDictionary["fr"] = frErrors;
        _errorDictionary["es"] = esErrors;

        InitializeDefaultUiStrings();
    }

    private void InitializeDefaultUiStrings()
    {
        // 1. English
        _stringDictionary["en"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Header_CommInspector"] = "⚡ Kable Comm Inspector",
            ["Header_CommandConsole"] = "💬 Aperiodic Commands",
            ["Header_PeriodicTelemetry"] = "📈 Periodic Telemetry",
            ["Header_Alarms"] = "🚨 Active Alarms",
            ["Header_HexInspector"] = "Hex Dump / Inspector",
            ["Header_ManualInjection"] = "Manual Command Injection",
            ["Header_DeviceInfo"] = "1. Device Identification & Description",
            ["Header_TransportSelection"] = "2. Transport Layer Selection",
            ["Header_CodecRouter"] = "3. Codec & Transaction Router",
            ["Header_LiveTest"] = "4. Diagnostics & Live Simulation",
            ["Header_TomlPreview"] = "📄 Auto-Generated TOML Preview",

            ["Nav_Config"] = "⚙️ Comm Config",
            ["Nav_Packets"] = "📋 Packet Catalog",
            ["Nav_Inspector"] = "💬 Aperiodic Commands",
            ["Nav_Telemetry"] = "📈 Telemetry",
            ["Nav_Alarms"] = "🚨 Alarms",
            ["Nav_Export"] = "📄 Config Export",

            ["Header_PacketCatalog"] = "Hardware Packet Catalog & Traffic Classification",
            ["Lbl_TrafficType"] = "Traffic Type:",
            ["Lbl_Aperiodic"] = "Aperiodic (Command)",
            ["Lbl_Periodic"] = "Periodic (Telemetry)",
            ["Lbl_Alarm"] = "Alarm",
            ["Lbl_IntervalMs"] = "Interval (ms):",
            ["Btn_SimStart"] = "▶️ Start Simulation",
            ["Btn_SimStop"] = "⏹️ Stop Simulation",
            ["Btn_AddPacket"] = "➕ Add Packet",

            ["Col_Seq"] = "#",
            ["Col_Time"] = "Time",
            ["Col_Dir"] = "Dir",
            ["Col_Device"] = "Device",
            ["Col_Type"] = "Type",
            ["Col_Bytes"] = "Bytes",
            ["Col_Latency"] = "Latency",
            ["Col_PayloadPreview"] = "Payload Preview",
            ["Col_Parameter"] = "Parameter",
            ["Col_Value"] = "Current Value",
            ["Col_Unit"] = "Unit",
            ["Col_Frequency"] = "Update Rate (Hz)",
            ["Col_LastUpdated"] = "Last Updated",
            ["Col_AlarmCode"] = "Alarm Code",
            ["Col_Severity"] = "Severity",
            ["Col_AlarmMessage"] = "Message",
            ["Col_State"] = "State",

            ["Btn_Send"] = "Send",
            ["Btn_SendManual"] = "Send Manual Command",
            ["Btn_Pause"] = "Pause",
            ["Btn_Resume"] = "Resume",
            ["Btn_Clear"] = "Clear",
            ["Btn_Refresh"] = "Refresh",
            ["Btn_RunLiveTest"] = "⚡ Live Hardware Test",
            ["Btn_RunSimulation"] = "🔬 Mock Loopback Simulation",
            ["Btn_SaveConfig"] = "💾 Save Configuration",

            ["Lbl_Filter"] = "Filter:",
            ["Lbl_SelectPacket"] = "Select a packet to inspect hex dump...",
            ["Lbl_DeviceId"] = "Device ID:",
            ["Lbl_Description"] = "Description:",
            ["Lbl_Host"] = "Host IP:",
            ["Lbl_Port"] = "Port:",
            ["Lbl_SerialPort"] = "COM Port:",
            ["Lbl_BaudRate"] = "Baud Rate:",
            ["Lbl_Parity"] = "Parity:",
            ["Lbl_StopBits"] = "Stop Bits:",
            ["Lbl_DataBits"] = "Data Bits:",
            ["Lbl_PipeName"] = "Pipe Name:",
            ["Lbl_StatusIdle"] = "Idle (No test executed)",
            ["Lbl_StatusTesting"] = "Connecting and running loopback ping...",
            ["Lbl_StatusSuccess"] = "Communication verified! Latency: {0:F2} ms",
            ["Lbl_StatusFailed"] = "Communication test failed: {0}"
        };

        // 2. Korean
        _stringDictionary["ko"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Header_CommInspector"] = "⚡ Kable 통신 인스펙터",
            ["Header_CommandConsole"] = "💬 수시 명령 통신 (Aperiodic)",
            ["Header_PeriodicTelemetry"] = "📈 상시 텔레메트리 (Periodic)",
            ["Header_Alarms"] = "🚨 실시간 알람 (Alarms)",
            ["Header_HexInspector"] = "패킷 헥스 덤프 및 상세 분석",
            ["Header_ManualInjection"] = "수동 명령 전송 바",
            ["Header_DeviceInfo"] = "1. 장치 식별 및 설명",
            ["Header_TransportSelection"] = "2. 통신 선로 (Transport) 선택",
            ["Header_CodecRouter"] = "3. 패킷 코덱(Codec) 및 트랜잭션 라우터",
            ["Header_LiveTest"] = "4. 현장 통신 진단 및 시뮬레이션",
            ["Header_TomlPreview"] = "📄 자동 생성된 TOML 설정 미리보기",

            ["Nav_Config"] = "⚙️ 통신 설정 (Config)",
            ["Nav_Packets"] = "📋 패킷 카탈로그 (Packets)",
            ["Nav_Inspector"] = "💬 수시 명령 (Aperiodic)",
            ["Nav_Telemetry"] = "📈 상시 텔레메트리 (Telemetry)",
            ["Nav_Alarms"] = "🚨 실시간 알람 (Alarms)",
            ["Nav_Export"] = "📄 설정 내보내기 (Export)",

            ["Header_PacketCatalog"] = "통신 패킷 사전 및 상시/수시 트래픽 분류 설정",
            ["Lbl_TrafficType"] = "트래픽 구분:",
            ["Lbl_Aperiodic"] = "수시 명령 (Aperiodic)",
            ["Lbl_Periodic"] = "상시 텔레메트리 (Periodic)",
            ["Lbl_Alarm"] = "자발적 경보 (Alarm)",
            ["Lbl_IntervalMs"] = "주기(ms):",
            ["Btn_SimStart"] = "▶️ 시뮬레이터 가동 (스트리밍)",
            ["Btn_SimStop"] = "⏹️ 시뮬레이터 정지",
            ["Btn_AddPacket"] = "➕ 새 패킷 추가",

            ["Col_Seq"] = "순번(#)",
            ["Col_Time"] = "수신시각",
            ["Col_Dir"] = "방향",
            ["Col_Device"] = "장치ID",
            ["Col_Type"] = "트래픽구분",
            ["Col_Bytes"] = "바이트",
            ["Col_Latency"] = "지연시간",
            ["Col_PayloadPreview"] = "페이로드 미리보기",
            ["Col_Parameter"] = "파라미터명",
            ["Col_Value"] = "현재값",
            ["Col_Unit"] = "단위",
            ["Col_Frequency"] = "갱신주기(Hz)",
            ["Col_LastUpdated"] = "최종갱신",
            ["Col_AlarmCode"] = "알람 코드",
            ["Col_Severity"] = "심각도",
            ["Col_AlarmMessage"] = "경보 메시지",
            ["Col_State"] = "상태",

            ["Btn_Send"] = "전송",
            ["Btn_SendManual"] = "수동 명령 전송",
            ["Btn_Pause"] = "일시정지",
            ["Btn_Resume"] = "재개",
            ["Btn_Clear"] = "로그 초기화",
            ["Btn_Refresh"] = "새로고침",
            ["Btn_RunLiveTest"] = "⚡ 실제 장비 연결 진단 (Live Test)",
            ["Btn_RunSimulation"] = "🔬 오프라인 시뮬레이션 (Mock Loopback)",
            ["Btn_SaveConfig"] = "💾 설정 저장 및 TOML 파일 생성",

            ["Lbl_Filter"] = "필터 검색:",
            ["Lbl_SelectPacket"] = "패킷을 선택하면 헥스 덤프가 표시됩니다...",
            ["Lbl_DeviceId"] = "장치 식별자:",
            ["Lbl_Description"] = "장치 설명:",
            ["Lbl_Host"] = "호스트 IP:",
            ["Lbl_Port"] = "포트 번호:",
            ["Lbl_SerialPort"] = "시리얼 포트:",
            ["Lbl_BaudRate"] = "보드레이트:",
            ["Lbl_Parity"] = "패리티:",
            ["Lbl_StopBits"] = "정지비트:",
            ["Lbl_DataBits"] = "데이터비트:",
            ["Lbl_PipeName"] = "파이프 이름:",
            ["Lbl_StatusIdle"] = "대기 중 (통신 테스트 미실시)",
            ["Lbl_StatusTesting"] = "통신 연결 및 루프백 핑 시험 중...",
            ["Lbl_StatusSuccess"] = "통신 연결 정상 검증 완료! 지연시간: {0:F2} ms",
            ["Lbl_StatusFailed"] = "통신 연결 진단 실패: {0}"
        };

        // 3. Traditional Chinese (zh-TW)
        _stringDictionary["zh-TW"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Header_CommInspector"] = "⚡ Kable 通訊檢測器",
            ["Header_CommandConsole"] = "💬 非週期性指令 (Aperiodic)",
            ["Header_PeriodicTelemetry"] = "📈 週期性遙測 (Periodic)",
            ["Header_Alarms"] = "🚨 即時警報 (Alarms)",
            ["Header_HexInspector"] = "十六進位封包檢索器",
            ["Header_ManualInjection"] = "手動指令注入",
            ["Header_DeviceInfo"] = "1. 設備識別與描述",
            ["Header_TransportSelection"] = "2. 通訊線路 (Transport) 選擇",
            ["Header_CodecRouter"] = "3. 編解碼器與交易路由",
            ["Header_LiveTest"] = "4. 現狀診斷與即時模擬",
            ["Header_TomlPreview"] = "📄 自動產生之 TOML 設定預覽",

            ["Nav_Config"] = "⚙️ 通訊設定 (Config)",
            ["Nav_Inspector"] = "💬 隨機指令 (Aperiodic)",
            ["Nav_Telemetry"] = "📈 遙測數據 (Telemetry)",
            ["Nav_Alarms"] = "🚨 即時警報 (Alarms)",
            ["Nav_Export"] = "📄 匯出設定 (Export)",

            ["Col_Seq"] = "序號(#)",
            ["Col_Time"] = "時間",
            ["Col_Dir"] = "方向",
            ["Col_Device"] = "設備ID",
            ["Col_Type"] = "封包類別",
            ["Col_Bytes"] = "位元組",
            ["Col_Latency"] = "延遲",
            ["Col_PayloadPreview"] = "載荷預覽",
            ["Col_Parameter"] = "參數名稱",
            ["Col_Value"] = "當前數值",
            ["Col_Unit"] = "單位",
            ["Col_Frequency"] = "更新頻率(Hz)",
            ["Col_LastUpdated"] = "最後更新",
            ["Col_AlarmCode"] = "警報代碼",
            ["Col_Severity"] = "嚴重度",
            ["Col_AlarmMessage"] = "警報內容",
            ["Col_State"] = "狀態",

            ["Btn_Send"] = "送出",
            ["Btn_SendManual"] = "發送手動指令",
            ["Btn_Pause"] = "暫停",
            ["Btn_Resume"] = "繼續",
            ["Btn_Clear"] = "清空記錄",
            ["Btn_Refresh"] = "重新整理",
            ["Btn_RunLiveTest"] = "⚡ 實機連線診斷 (Live Test)",
            ["Btn_RunSimulation"] = "🔬 離線模擬 (Mock Loopback)",
            ["Btn_SaveConfig"] = "💾 儲存設定並產生 TOML",

            ["Lbl_Filter"] = "過濾篩選:",
            ["Lbl_SelectPacket"] = "選取封包以檢視十六進位傾印...",
            ["Lbl_DeviceId"] = "設備代碼:",
            ["Lbl_Description"] = "設備說明:",
            ["Lbl_Host"] = "主機 IP:",
            ["Lbl_Port"] = "連接埠:",
            ["Lbl_SerialPort"] = "序列埠:",
            ["Lbl_BaudRate"] = "鮑率:",
            ["Lbl_Parity"] = "同位檢查:",
            ["Lbl_StopBits"] = "停止位元:",
            ["Lbl_DataBits"] = "資料位元:",
            ["Lbl_PipeName"] = "管道名稱:",
            ["Lbl_StatusIdle"] = "待命 (尚未執行連線測試)",
            ["Lbl_StatusTesting"] = "連線與回環 Ping 測試中...",
            ["Lbl_StatusSuccess"] = "連線驗證成功！延遲: {0:F2} ms",
            ["Lbl_StatusFailed"] = "連線診斷失敗: {0}"
        };

        // 4. Simplified Chinese (zh-CN)
        _stringDictionary["zh-CN"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Header_CommInspector"] = "⚡ Kable 通信检测器",
            ["Header_CommandConsole"] = "💬 非周期性命令 (Aperiodic)",
            ["Header_PeriodicTelemetry"] = "📈 周期性遥测 (Periodic)",
            ["Header_Alarms"] = "🚨 实时报警 (Alarms)",
            ["Header_HexInspector"] = "十六进制报文分析器",
            ["Header_ManualInjection"] = "手动命令发送栏",
            ["Header_DeviceInfo"] = "1. 设备标识与描述",
            ["Header_TransportSelection"] = "2. 通信传输层 (Transport) 选择",
            ["Header_CodecRouter"] = "3. 编解码器与路由配置",
            ["Header_LiveTest"] = "4. 现场通信诊断与模拟",
            ["Header_TomlPreview"] = "📄 自动生成的 TOML 配置预览",

            ["Nav_Config"] = "⚙️ 通信配置 (Config)",
            ["Nav_Inspector"] = "💬 非周期命令 (Aperiodic)",
            ["Nav_Telemetry"] = "📈 实时遥测 (Telemetry)",
            ["Nav_Alarms"] = "🚨 实时报警 (Alarms)",
            ["Nav_Export"] = "📄 导出配置 (Export)",

            ["Col_Seq"] = "序号(#)",
            ["Col_Time"] = "时间",
            ["Col_Dir"] = "方向",
            ["Col_Device"] = "设备ID",
            ["Col_Type"] = "报文类型",
            ["Col_Bytes"] = "字节",
            ["Col_Latency"] = "延迟",
            ["Col_PayloadPreview"] = "负载预览",
            ["Col_Parameter"] = "参数名称",
            ["Col_Value"] = "当前值",
            ["Col_Unit"] = "单位",
            ["Col_Frequency"] = "刷新率(Hz)",
            ["Col_LastUpdated"] = "更新时间",
            ["Col_AlarmCode"] = "报警代码",
            ["Col_Severity"] = "严重级别",
            ["Col_AlarmMessage"] = "报警信息",
            ["Col_State"] = "状态",

            ["Btn_Send"] = "发送",
            ["Btn_SendManual"] = "发送手动命令",
            ["Btn_Pause"] = "暂停",
            ["Btn_Resume"] = "恢复",
            ["Btn_Clear"] = "清空记录",
            ["Btn_Refresh"] = "刷新",
            ["Btn_RunLiveTest"] = "⚡ 现场实机测试 (Live Test)",
            ["Btn_RunSimulation"] = "🔬 离线模拟 (Mock Loopback)",
            ["Btn_SaveConfig"] = "💾 保存配置并生成 TOML",

            ["Lbl_Filter"] = "过滤查询:",
            ["Lbl_SelectPacket"] = "选择报文查看十六进制转储...",
            ["Lbl_DeviceId"] = "设备标识:",
            ["Lbl_Description"] = "设备描述:",
            ["Lbl_Host"] = "主机 IP:",
            ["Lbl_Port"] = "端口:",
            ["Lbl_SerialPort"] = "串口号:",
            ["Lbl_BaudRate"] = "波特率:",
            ["Lbl_Parity"] = "校验位:",
            ["Lbl_StopBits"] = "停止位:",
            ["Lbl_DataBits"] = "数据位:",
            ["Lbl_PipeName"] = "命名管道:",
            ["Lbl_StatusIdle"] = "就绪 (未执行测试)",
            ["Lbl_StatusTesting"] = "正在测试连接与回环 Ping...",
            ["Lbl_StatusSuccess"] = "通信连接正常！延迟: {0:F2} ms",
            ["Lbl_StatusFailed"] = "通信诊断失败: {0}"
        };

        // 5. Japanese (ja)
        _stringDictionary["ja"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Header_CommInspector"] = "⚡ Kable 通信インスペクター",
            ["Header_CommandConsole"] = "💬 随時コマンド通信 (Aperiodic)",
            ["Header_PeriodicTelemetry"] = "📈 定常テレメトリ (Periodic)",
            ["Header_Alarms"] = "🚨 リアルタイム警報 (Alarms)",
            ["Header_HexInspector"] = "16進ダンプ & 詳細インスペクター",
            ["Header_ManualInjection"] = "手動コマンド送信バー",
            ["Header_DeviceInfo"] = "1. デバイス識別および説明",
            ["Header_TransportSelection"] = "2. 通信トランスポートの選択",
            ["Header_CodecRouter"] = "3. パケットCodec & トランザクションルーター",
            ["Header_LiveTest"] = "4. 現地通信診断 & シミュレーション",
            ["Header_TomlPreview"] = "📄 自動生成された TOML 設定プレビュー",

            ["Nav_Config"] = "⚙️ 通信設定 (Config)",
            ["Nav_Inspector"] = "💬 随時コマンド (Aperiodic)",
            ["Nav_Telemetry"] = "📈 定常テレメトリ (Telemetry)",
            ["Nav_Alarms"] = "🚨 リアルタイム警報 (Alarms)",
            ["Nav_Export"] = "📄 設定エクスポート (Export)",

            ["Col_Seq"] = "連番(#)",
            ["Col_Time"] = "時刻",
            ["Col_Dir"] = "方向",
            ["Col_Device"] = "装置ID",
            ["Col_Type"] = "種別",
            ["Col_Bytes"] = "バイト",
            ["Col_Latency"] = "遅延",
            ["Col_PayloadPreview"] = "ペイロード概要",
            ["Col_Parameter"] = "パラメータ名",
            ["Col_Value"] = "現在値",
            ["Col_Unit"] = "単位",
            ["Col_Frequency"] = "更新レート(Hz)",
            ["Col_LastUpdated"] = "最終更新",
            ["Col_AlarmCode"] = "警報コード",
            ["Col_Severity"] = "重要度",
            ["Col_AlarmMessage"] = "アラーム内容",
            ["Col_State"] = "状態",

            ["Btn_Send"] = "送信",
            ["Btn_SendManual"] = "手動コマンド送信",
            ["Btn_Pause"] = "一時停止",
            ["Btn_Resume"] = "再開",
            ["Btn_Clear"] = "ログ消去",
            ["Btn_Refresh"] = "更新",
            ["Btn_RunLiveTest"] = "⚡ 実機接続診断 (Live Test)",
            ["Btn_RunSimulation"] = "🔬 オフライン模擬 (Mock Loopback)",
            ["Btn_SaveConfig"] = "💾 設定保存および TOML 生成",

            ["Lbl_Filter"] = "絞り込み:",
            ["Lbl_SelectPacket"] = "パケットを選択すると16進ダンプが表示されます...",
            ["Lbl_DeviceId"] = "デバイス識別子:",
            ["Lbl_Description"] = "デバイス説明:",
            ["Lbl_Host"] = "ホスト IP:",
            ["Lbl_Port"] = "ポート番号:",
            ["Lbl_SerialPort"] = "COMポート:",
            ["Lbl_BaudRate"] = "ボーレート:",
            ["Lbl_Parity"] = "パリティ:",
            ["Lbl_StopBits"] = "ストップビット:",
            ["Lbl_DataBits"] = "データビット:",
            ["Lbl_PipeName"] = "パイプ名:",
            ["Lbl_StatusIdle"] = "待機中 (通信テスト未実施)",
            ["Lbl_StatusTesting"] = "接続およびループバック Ping 実行中...",
            ["Lbl_StatusSuccess"] = "通信接続を確認しました！遅延: {0:F2} ms",
            ["Lbl_StatusFailed"] = "通信診断に失敗しました: {0}"
        };

        // 6. German (de)
        _stringDictionary["de"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Header_CommInspector"] = "⚡ Kable Kommunikations-Inspektor",
            ["Header_CommandConsole"] = "💬 Aperiodische Befehle",
            ["Header_PeriodicTelemetry"] = "📈 Periodische Telemetrie",
            ["Header_Alarms"] = "🚨 Aktive Alarme",
            ["Header_HexInspector"] = "Hex-Dump / Inspektor",
            ["Header_ManualInjection"] = "Manuelle Befehlseingabe",
            ["Header_DeviceInfo"] = "1. Geräteidentifikation & Beschreibung",
            ["Header_TransportSelection"] = "2. Auswahl der Transportschicht",
            ["Header_CodecRouter"] = "3. Codec & Transaktions-Router",
            ["Header_LiveTest"] = "4. Diagnose & Live-Simulation",
            ["Header_TomlPreview"] = "📄 Automatisch generierte TOML-Vorschau",

            ["Nav_Config"] = "⚙️ Kommunikation (Config)",
            ["Nav_Inspector"] = "💬 Befehle (Aperiodic)",
            ["Nav_Telemetry"] = "📈 Telemetrie (Telemetry)",
            ["Nav_Alarms"] = "🚨 Alarme (Alarms)",
            ["Nav_Export"] = "📄 Exportieren (Export)",

            ["Col_Seq"] = "#",
            ["Col_Time"] = "Zeit",
            ["Col_Dir"] = "Richtung",
            ["Col_Device"] = "Gerät",
            ["Col_Type"] = "Typ",
            ["Col_Bytes"] = "Bytes",
            ["Col_Latency"] = "Latenz",
            ["Col_PayloadPreview"] = "Nutzdaten-Vorschau",
            ["Col_Parameter"] = "Parameter",
            ["Col_Value"] = "Aktueller Wert",
            ["Col_Unit"] = "Einheit",
            ["Col_Frequency"] = "Frequenz (Hz)",
            ["Col_LastUpdated"] = "Letzte Aktualisierung",
            ["Col_AlarmCode"] = "Alarm-Code",
            ["Col_Severity"] = "Schweregrad",
            ["Col_AlarmMessage"] = "Meldung",
            ["Col_State"] = "Status",

            ["Btn_Send"] = "Senden",
            ["Btn_SendManual"] = "Befehl manuell senden",
            ["Btn_Pause"] = "Pause",
            ["Btn_Resume"] = "Fortsetzen",
            ["Btn_Clear"] = "Löschen",
            ["Btn_Refresh"] = "Aktualisieren",
            ["Btn_RunLiveTest"] = "⚡ Live-Hardwaretest",
            ["Btn_RunSimulation"] = "🔬 Mock-Loopback Simulation",
            ["Btn_SaveConfig"] = "💾 Konfiguration speichern",

            ["Lbl_Filter"] = "Filter:",
            ["Lbl_SelectPacket"] = "Paket auswählen für Hex-Dump...",
            ["Lbl_DeviceId"] = "Geräte-ID:",
            ["Lbl_Description"] = "Beschreibung:",
            ["Lbl_Host"] = "Host-IP:",
            ["Lbl_Port"] = "Port:",
            ["Lbl_SerialPort"] = "COM-Port:",
            ["Lbl_BaudRate"] = "Baudrate:",
            ["Lbl_Parity"] = "Parität:",
            ["Lbl_StopBits"] = "Stoppbits:",
            ["Lbl_DataBits"] = "Datenbits:",
            ["Lbl_PipeName"] = "Pipe-Name:",
            ["Lbl_StatusIdle"] = "Bereit (Kein Test ausgeführt)",
            ["Lbl_StatusTesting"] = "Verbindung wird hergestellt...",
            ["Lbl_StatusSuccess"] = "Verbindung erfolgreich! Latenz: {0:F2} ms",
            ["Lbl_StatusFailed"] = "Verbindungstest fehlgeschlagen: {0}"
        };

        // 7. Spanish (es)
        _stringDictionary["es"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Header_CommInspector"] = "⚡ Inspector de Comunicación Kable",
            ["Header_CommandConsole"] = "💬 Comandos Aperiódicos",
            ["Header_PeriodicTelemetry"] = "📈 Telemetría Periódica",
            ["Header_Alarms"] = "🚨 Alarmas Activas",
            ["Header_HexInspector"] = "Volcado Hexadecimal / Inspector",
            ["Header_ManualInjection"] = "Inyección Manual de Comandos",
            ["Header_DeviceInfo"] = "1. Identificación y Descripción del Dispositivo",
            ["Header_TransportSelection"] = "2. Selección de Capa de Transporte",
            ["Header_CodecRouter"] = "3. Códec y Enrutador de Transacciones",
            ["Header_LiveTest"] = "4. Diagnóstico y Simulación en Vivo",
            ["Header_TomlPreview"] = "📄 Vista Previa de TOML Autogenerado",

            ["Nav_Config"] = "⚙️ Configuración (Config)",
            ["Nav_Inspector"] = "💬 Comandos (Aperiodic)",
            ["Nav_Telemetry"] = "📈 Telemetría (Telemetry)",
            ["Nav_Alarms"] = "🚨 Alarmas (Alarms)",
            ["Nav_Export"] = "📄 Exportar (Export)",

            ["Col_Seq"] = "#",
            ["Col_Time"] = "Hora",
            ["Col_Dir"] = "Dir",
            ["Col_Device"] = "Dispositivo",
            ["Col_Type"] = "Tipo",
            ["Col_Bytes"] = "Bytes",
            ["Col_Latency"] = "Latencia",
            ["Col_PayloadPreview"] = "Vista de Carga Útil",
            ["Col_Parameter"] = "Parámetro",
            ["Col_Value"] = "Valor Actual",
            ["Col_Unit"] = "Unidad",
            ["Col_Frequency"] = "Frecuencia (Hz)",
            ["Col_LastUpdated"] = "Última Actualización",
            ["Col_AlarmCode"] = "Código de Alarma",
            ["Col_Severity"] = "Severidad",
            ["Col_AlarmMessage"] = "Mensaje",
            ["Col_State"] = "Estado",

            ["Btn_Send"] = "Enviar",
            ["Btn_SendManual"] = "Enviar Comando Manual",
            ["Btn_Pause"] = "Pausar",
            ["Btn_Resume"] = "Reanudar",
            ["Btn_Clear"] = "Limpiar",
            ["Btn_Refresh"] = "Actualizar",
            ["Btn_RunLiveTest"] = "⚡ Prueba en Vivo de Hardware",
            ["Btn_RunSimulation"] = "🔬 Simulación Mock Loopback",
            ["Btn_SaveConfig"] = "💾 Guardar Configuración",

            ["Lbl_Filter"] = "Filtro:",
            ["Lbl_SelectPacket"] = "Seleccione un paquete para ver el volcado hexadecimal...",
            ["Lbl_DeviceId"] = "ID de Dispositivo:",
            ["Lbl_Description"] = "Descripción:",
            ["Lbl_Host"] = "IP del Host:",
            ["Lbl_Port"] = "Puerto:",
            ["Lbl_SerialPort"] = "Puerto COM:",
            ["Lbl_BaudRate"] = "Tasa de Baudios:",
            ["Lbl_Parity"] = "Paridad:",
            ["Lbl_StopBits"] = "Bits de Parada:",
            ["Lbl_DataBits"] = "Bits de Datos:",
            ["Lbl_PipeName"] = "Nombre de Canalización:",
            ["Lbl_StatusIdle"] = "Inactivo (Sin prueba ejecutada)",
            ["Lbl_StatusTesting"] = "Conectando y ejecutando ping...",
            ["Lbl_StatusSuccess"] = "¡Comunicación verificada! Latencia: {0:F2} ms",
            ["Lbl_StatusFailed"] = "Fallo en prueba de comunicación: {0}"
        };
    }
}
