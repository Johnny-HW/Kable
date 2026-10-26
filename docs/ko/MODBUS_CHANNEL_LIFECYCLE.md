# Modbus RTU 채널 수명과 실패 처리

`KableModbusDeviceChannel`은 Open, 요청, Close를 하나의 비동기 게이트로 직렬화합니다.
요청 timeout은 게이트 대기까지 포함하며 `Timeout.InfiniteTimeSpan`을 허용합니다.
송신 전 취소나 잘못된 timeout은 기존 연결에 영향을 주지 않습니다.

출력 버퍼에 프레임을 인코딩하기 시작한 뒤 timeout, 취소 또는 I/O 오류가 발생하면
연결을 분리하고 Abort 및 비동기 폐기를 수행합니다. 이전 장비 응답이 다음 요청에
매칭되는 일을 막기 위한 계약이며 자동 재시도나 명령 재전송은 하지 않습니다.

| 원인 | 요청 결과 |
| --- | --- |
| 요청 timeout | `TimeoutException` |
| 호출자의 취소 | `OperationCanceledException` |
| 물리 단절 또는 Close/Dispose에 의한 중단 | `DeviceDisconnectedException` |

Close/Dispose는 진행 중 연결 시도와 I/O를 취소하고 정리를 기다립니다.
취소와 timeout이 동시에 발생하면 호출자 취소를 우선합니다.
실패 정리 중 예외는 원래 요청의 실패를 덮지 않습니다.

실패 후 `IsOpen`은 false입니다. 다시 사용할 때는 `OpenAsync`로 새 연결을 얻습니다.
연결 팩토리는 매 연결마다 새 컨텍스트를 반환해야 합니다. `DisposeAsync` 이후에는
다시 열 수 없고, 반복 Dispose는 같은 종료 결과를 공유합니다.
obsolete 동기 Open/Close는 호환성용이며 신규 호출은 비동기 API를 사용합니다.
