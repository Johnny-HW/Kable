# 동시 시작 실패 결과 보존 — 2026-10-26

## 원인과 수정

연결 실패를 `_startTcs`에 기록한 다음 정리 코드가 세션 토큰을 취소한다.
동시 시작 대기는 공유 Task와 연결된 취소 토큰을 함께 기다리므로, 비동기 완료 알림의
전달 순서에 따라 취소하지 않은 호출자도 원래 연결 오류 대신 취소 예외를 받을 수 있었다.

`StartAsync`의 후속 호출 대기에서만 처리한다. 취소 예외를 받더라도 호출자 토큰이
취소되지 않았고 공유 시작 Task가 이미 완료됐으면 그 결과를 다시 await한다.
완료된 Task만 확인하므로 미완료 연결을 기다리느라 Stop/Dispose가 지연되지 않는다.
일반 요청의 공통 대기 함수, FIFO/CID 처리와 공개 API는 변경하지 않았다.

## 검증

| 검사 | 결과 |
| --- | --- |
| 변경 전 제어 재현 검사 | 원래 연결 오류 대신 OperationCanceledException을 받아 실패 |
| 변경 후 제어 재현 검사 | 독립 프로세스 3회 모두 통과 |
| 전체 Release 빌드 | 경고 0 / 오류 0 |
| 전체 개발 작업본 테스트 | 13개 프로젝트, 464개 통과 / 실패 0 |
| Core 테스트 | 355개 통과 |

추가 단위 테스트는 호출자를 직접 취소한 뒤 다른 호출자에게 연결 실패가 발생하는 경우,
연결 중 Stop/Dispose, 취소를 무시하는 연결 팩토리가 완료되기 전 대기 호출자 중단을 검증한다.
재현 검사는 별도 프로세스의 스레드 풀만 제어해 실제 StartAsync를 실행한다.
원본 예외 객체, 호출자 취소 여부, 연결 시도 횟수와 종료 상태를 검사한다.
테스트 실행기의 스레드 풀이나 테스트 임계값은 변경하지 않는다.

```powershell
./tests/Kable.Tests/Cases/Engine/Lifecycle/VerifyConcurrentStart.ps1
dotnet build Kable.sln -c Release --no-restore
dotnet test Kable.sln -c Release --no-build -m:1
```

Windows CI에도 제어 재현 검사를 추가했다. 빌드·테스트 로그는 각각
`%TEMP%/kable-start-outcome-build.log`, `%TEMP%/kable-start-outcome-tests.log`에,
TRX는 `%TEMP%/kable-start-outcome-results/`에 저장했다.

## 적용 범위

464개는 기존 미커밋 Protocol Pack 작업도 포함하는 개발 작업본의 검증 수치다.
해당 작업은 이번 수정 커밋에 포함하지 않았다. CHANGELOG의 Unreleased에 수정 사항을 기록했다.
게시된 NuGet 1.9.0과 v1.9.0 태그는 변경하지 않았고, 추가 게시도 수행하지 않았다.
