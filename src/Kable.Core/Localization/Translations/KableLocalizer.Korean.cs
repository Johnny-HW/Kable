namespace Kable.Localization;

using System;
using System.Collections.Generic;

public sealed partial class KableLocalizer
{
    private void InitializeKoreanTranslations()
    {
        _errorDictionary["ko"] = new Dictionary<KableErrorCode, string>
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
            ["Lbl_StatusFailed"] = "통신 연결 진단 실패: {0}",

            // Command Console & Packet Catalog Specific Keys
            ["Header_CommandSubTitle"] = "⚡ 수시(Aperiodic) 명령 전용 통신 콘솔",
            ["Header_CommandPresets"] = "🎛️ 명령 & 파라미터 제어",
            ["Lbl_SearchCommands"] = "명령어 검색 (예: TEMP, PRESS, FLOW...)",
            ["Lbl_ConvertedPreview"] = "환산값:",
            ["Btn_InjectSend"] = "명령 주입 (Send)",
            ["Lbl_SelectPacketHint"] = "목록에서 패킷을 선택하면 바이트 상세가 표시됩니다.",
            ["Lbl_ManualSend"] = "수동 전송:",
            ["Btn_ExportLog"] = "💾 로그 저장",
            ["Header_HexInspectorByte"] = "🔍 헥스 덤프 & 바이트 분석기",
            ["Header_RawPacketStream"] = "📡 원본 패킷 스트림 로그",
            ["Lbl_RawPacketHint"] = "목록에서 패킷을 선택하면 상세 바이트 분석이 표시됩니다.",
            ["Lbl_RegisteredCount"] = "{0}개 등록",
            ["Btn_StartSim"] = "🔬 가상 시뮬레이터 시작",
            ["Btn_StreamingActive"] = "🟢 스트리밍 중",
            ["Btn_RefreshPorts"] = "🔄 포트 새로고침",
            ["Lbl_TomlFooter"] = "Kable 실시간 멀티 프로토콜 엔진 • 프로파일 검증 완료",
            ["Col_Active"] = "활성",
            ["Col_PacketId"] = "패킷 ID",
            ["Col_PacketName"] = "명칭 (Name)",
            ["Col_TrafficKind"] = "트래픽 구분 (Traffic Kind)",
            ["Col_Interval"] = "주기 (ms)",
            ["Col_SimulatedBase"] = "가상 기준값",
            ["Col_PayloadFormat"] = "송신 페이로드 / 포맷",
            ["Lbl_PacketCatalogDesc"] = "장비가 송수신하는 패킷을 정의하고, 상시(Periodic) 및 수시(Aperiodic) 주기와 시뮬레이션 값을 관리합니다."
        };
    }
}
