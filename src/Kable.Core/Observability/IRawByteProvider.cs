namespace Kable.Observability;

using System;

/// <summary>
/// 물리 통신 프레임의 원시 바이트 덤프를 제공하는 메시지 계약 인터페이스.
/// </summary>
public interface IRawByteProvider
{
    ReadOnlyMemory<byte> ToRawBytes();
}
