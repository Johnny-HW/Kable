namespace Kable.Melsec.Protocol;

/// <summary>
/// 미쓰비시 MC 프로토콜 디바이스 코드 (SLMP 3E 바이너리 기준)
/// </summary>
public enum MelsecDeviceCode : byte
{
    // 워드/비트 공용 또는 워드 전용
    D = 0xA8, // 데이터 레지스터 (Data Register) - Word
    W = 0xB4, // 링크 레지스터 (Link Register) - Word
    R = 0xAF, // 파일 레지스터 (File Register) - Word
    ZR = 0xB0, // 확장 파일 레지스터 (File Register Consecutive) - Word

    // 비트 전용
    M = 0x90, // 내부 릴레이 (Internal Relay) - Bit/Word
    X = 0x9C, // 입력 릴레이 (Input Relay) - Bit/Word (16진수 주소)
    Y = 0x9D, // 출력 릴레이 (Output Relay) - Bit/Word (16진수 주소)
    L = 0x92, // 래치 릴레이 (Latch Relay) - Bit/Word
    F = 0x93, // 공보 릴레이 (Annunciator) - Bit/Word
    B = 0xA0  // 링크 릴레이 (Link Relay) - Bit/Word (16진수 주소)
}
