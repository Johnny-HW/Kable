# 종합 검토 수정 계획

2026-10-30. 사용자 요청에 따라 계획과 실행을 함께 진행한다.
원본 감사 및 재감사 문서는 보존한다.

## 수정 순서와 대상

| 단계 | 범위 | 주요 대상 |
|---|---|---|
| 1 | 응답 검증, 분할 수신, 노이즈 후 재파싱 | MelsecSlmpCodec, Slmp3EFrame, MelsecPlcClient, ModbusRtuCodec, KableModbusDeviceChannel, ModbusTcpCodec |
| 2 | Abort/Dispose 분리, 리스너 소유권, 시뮬레이터 지연 생성 및 소유권 | IpcNamedPipe, GrpcConnectionContext, KableClientBuilder, 새 SimulatorConnectionFactory |
| 3 | 지표 스냅샷 동기화, 공유 메모리 포인터 수명 및 초기화 | ZeroAllocLatencyCollector, SharedMemoryWaveformBuffer 및 분리된 수명 관리 파일 |
| 4 | 코덱 진단, 세션 상태, 관찰자 예외 격리, DeviceId | DelimitedFrameCodec, LengthFieldCodec, KableSession 부분 클래스, SessionHealthMonitor |

## 검증 방법

- 각 단계에서 회귀 테스트를 먼저 작성하고 수정 전 실패를 확인한다.
- 잘못된 응답 거부뿐 아니라 정상 응답, 분할 수신, 취소, 반복 종료를 확인한다.
- 동시성은 유한 시간 테스트와 경계 조건으로 확인한다. 스트레스 통과를 안전성의 증명으로 표현하지 않는다.
- IPC, gRPC, MELSEC, SharedMemory 전용 테스트와 Kable.Tests를 실행한다.
- 지원 대상 프레임워크 빌드를 확인하고 검증된 단계별 커밋을 작성한다.
- 새 파일은 300줄 미만으로 나눈다. 기존 공개 인터페이스를 변경하지 않는다.

## 적용하지 않는 주장

- 무조건적인 OS 2시간 단절 감지, 모든 항목 P0, 의도적 벤치마크 조작은 근거가 없으므로 구현 요구로 취급하지 않는다.
- FIFO 타임아웃 종료, 재연결 시 새 세션 생성, 분석용 스냅샷 할당은 명시된 계약을 유지한다.
- 메모리 소유권을 바꾸는 무조건적인 배열 풀링, 동기 Dispose 삭제 등 호환성 변경은 포함하지 않는다.

## 완료

4단계 수정을 완료했다. 재연결 중 명령 해제 경합과 기반 인메모리 파이프 정리도 함께 보완했다.
전체 Release 직렬 테스트 941개가 통과했다. 마지막 종료 루프 실패 관찰 보완 후 관련 테스트 75개와
Release 전체 빌드를 추가 확인했다.

상세 결과·성능 비용·계약 변화·검증 한계는 [수정 결과](design_critique_remediation_results.md)에 기록한다.
