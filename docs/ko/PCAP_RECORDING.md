# PCAP 기록과 조회 스트림

`PcapStreamObserver`는 파일 I/O를 단일 비동기 워커로 분리합니다.
기존 `PcapStreamObserver(string filePath)`는 유지되며 용량은 아래 옵션으로 설정합니다.

```csharp
var observer = new PcapStreamObserver("traffic.pcap", new PcapStreamObserverOptions
{
    RecordingCapacity = 1000,
    CommandCapacity = 1000,
    PeriodicCapacity = 1000,
    AlarmCapacity = 1000
});
```

모든 용량은 양수이며 기본값은 각각 1,000건입니다. 기록 큐 용량에는 현재 쓰기 중인
1건이 포함되지 않습니다. 패킷별 원시 바이트는 최대 65,535바이트를 소유 복사하고,
PCAP에는 잘라내기 전 원본 길이도 저장합니다. 조회 스트림 역시 이 스냅샷을 공유합니다.

기록 큐가 가득 차거나 디스크 쓰기가 실패하면 기록을 영구 중단합니다.
생산자인 `OnPacketTrace`에는 기록 오류를 던지지 않아 장비 통신을 계속할 수 있습니다.
`LastError`로 오류를 확인하고 `Completion`을 await하여 워커의 실패를 관찰합니다.
기록 실패 시 아직 쓰지 않은 큐 항목은 폐기하며, 복구하려면 새 옵저버를 생성합니다.

정상 `DisposeAsync`는 새 접수를 차단하고 수락한 모든 기록을 순서대로 배출한 뒤
flush와 스트림 폐기를 기다립니다. 동시·반복 호출은 같은 결과를 공유하며 실패가 있으면
정리를 마친 뒤 같은 오류를 반환합니다. 종료 이후 trace는 무시합니다.
종료는 협력적인 스트림 I/O 완료에 의존하며 강제 배출 제한시간은 두지 않습니다.

`CommandStream`, `PeriodicStream`, `AlarmStream`은 각각 유한 `DropOldest` 조회 버퍼입니다.
미구독·느린 소비자 상황에서 가장 오래된 항목을 버리며, 누락 수는 각각
`DroppedCommandCount`, `DroppedPeriodicCount`, `DroppedAlarmCount`에 누적됩니다.
조회 누락은 파일 기록에 영향을 주지 않습니다. 무손실 파일 기록은 정상 종료한 옵저버에서
수락된 항목에 한하며, 조회 스트림 자체에는 무손실 보장이 없습니다.
종료 시 세 스트림을 완료하고, 기록 실패 시 완료 오류도 전달합니다.
잔여 조회 항목을 읽은 뒤 `ChannelReader.Completion`에서 완료 결과를 관찰할 수 있습니다.
