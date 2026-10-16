# CID 소유권과 동시 시작 취소 수정 계획

작성일: 2026-10-16. 두 결함에 대한 사용자의 추가 개발 요청으로 승인된 범위다.

## 계약과 설계

SSOT 문서와 기존 연결 수명 계약을 따른다. 공개 API와 3계층 경계는 유지한다.

### CID 정리

응답 디스패치는 CID 항목을 제거한 뒤 결과를 완료한다. 이전 요청의 finally가
실행되기 전에 같은 CID가 새 요청으로 등록될 수 있다. 기존 CID만 사용한
TryRemove는 이 새 요청을 삭제한다.

finally에서 `(CID, 자신이 등록한 TaskCompletionSource)` 쌍이 일치할 때만
원자적으로 제거한다. netstandard2.0에서도 지원하는 ConcurrentDictionary의
ICollection<KeyValuePair<...>>.Remove 구현을 사용한다. 조회 후 별도 삭제하지 않는다.
값은 참조형 TCS이므로 동일 인스턴스로 소유권을 비교한다. 새 할당은 추가하지 않는다.
응답·타임아웃·취소의 기존 완료 방식과 중복 in-flight CID 거부는 유지한다.

### 동시 StartAsync

최초 호출자만 연결 팩토리를 실행한다. 후속 호출자는 공유 시작 작업을
기존 취소 대기 도우미와 호출별 linked token으로 기다린다.
후속 호출 취소는 그 호출의 대기만 종료하고 최초 연결·다른 대기자를 취소하지 않는다.
최초 시작 실패는 취소하지 않은 대기자에게 그대로 전달한다.

## 대상 파일 (각 300줄 미만)

- `src/Kable/Engine/KableSession.Messaging.cs`: CID 소유권을 원자적으로 확인해 삭제.
- `src/Kable/Engine/Lifecycle/KableSession.Start.cs`: 후속 시작 대기에 취소 전달.
- `tests/Kable.Tests/Cases/Engine/Messaging/CidOwnershipTests.cs`:
  응답 추적 콜백에서 같은 CID로 다음 요청을 등록해 경쟁 구간을 결정적으로 재현.
  이전 요청이 정상 반환하거나 응답 타입 변환에 실패해도 새 요청은 보존되어야 한다.
- `tests/Kable.Tests/Cases/Engine/Lifecycle/ConcurrentStartCancellationTests.cs`:
  연결 완료를 gate로 제어해 후속 호출 취소·공유 연결 유지·실패 전달을 검증.
- `docs/development/2026-10-16-request-ownership/walkthrough.md`: 결과 및 한계 기록.

## 순서와 검증

1. 재현 테스트를 먼저 작성하고 기존 구현에서 실패를 확인한다.
2. CID 소유권 수정 후 관련 회귀 테스트·빌드를 검증하고 fix 커밋한다.
3. 시작 취소 수정 후 관련 회귀 테스트·빌드를 검증하고 fix 커밋한다.
4. 전체 솔루션 빌드와 프로젝트 순차 테스트로 회귀를 확인한다.
   이전 작업에서 관찰한 부하/시간 민감 테스트는 결과를 숨기지 않고 기록한다.
5. 문서·코드 차이, 파일 줄 수, 작업 트리 상태를 확인하고 완료 문서를 커밋한다.
