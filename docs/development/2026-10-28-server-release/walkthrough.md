# Kable 1.10.0 서버 릴리즈 검증

## 승인 범위

사용자 승인에 따라 기존 미커밋 1:N 서버 기능을 검토·보완하고 문서를 개선한 뒤
1.10.0으로 게시한다. 기준은 PROJECT_SPEC, SYSTEM_DESIGN, CONVENTIONS다.
프로덕션은 Engine/Server, 테스트는 Cases/Engine/Server로 대응하고 각 파일을 300줄 미만으로 유지했다.

## 기능 검증

- 신규 안전성 테스트 8개가 수정 전 모두 실패하고 수정 후 통과했다.
- 서버 테스트 총 36개 통과: 수명, 최대 연결 수, 정확한 큐 용량, 실제 역압력,
  개별 전송·브로드캐스트, FIFO 순서, 이벤트 격리, 코덱 팩토리·송신·디코더 장애, TCP 종료.
- 송수신 루프와 전송 컨텍스트의 중복 파이프 완료가 TCP 종료를 막는 현상을 재현했다.
  파이프 정리를 연결 컨텍스트로 일원화한 뒤 TCP 종료 회귀가 통과했다.
- Stop은 송신 큐 드레인 후 제한시간에 Abort한다. 호출자 취소는 공유 정리를 중단하지 않는다.
- 문자열·이벤트·스냅샷·브로드캐스트의 할당을 문서에 명시했다.

## 전체 검증

- VerifySolution.ps1: Release 빌드 경고 0, 오류 0; 테스트 851/851 통과.
- 기존 테스트 815회에 서버 관련 테스트 36회를 추가했다.
- 동시 Start 실패 전파 회귀 3회와 격리 Generator 패키지 소비자 통과.
- QuickStart, Host, ConfigStudio 및 SharedMemory smoke 통과.
- 로그: artifacts/server-safety-before.log, server-tests.log, server-solution.log.

## 패키지 및 게시

1.10.0 제품 패키지 14개를 공통 버전으로 만든다. Host는 실행 프로그램으로 게시 대상에서 제외한다.
로컬 후보 feed 및 격리 캐시로 .NET 8/10 콘솔·WPF를 검증하고, 게시 후 공식 피드에서 재검증한다.
콘솔 소비자 ServerProbe는 패키지의 실제 TCP 서버 수신·브로드캐스트·종료를 실행한다.
게시 결과와 패키지 SHA-256은 완료 후 아래에 기록한다.

## 범위 제한

Windows에서 검증했다. 실장비와 장기 부하 시험은 포함하지 않는다.
커스텀 리스너·연결은 Stop/Abort로 I/O를 깨우고 DisposeAsync를 완료해야 한다.
종료 드레인 기한 뒤 자원 정리가 추가로 걸릴 수 있다.

## 로컬 후보 패키지 검증 완료

- 14개 패키지 README·LICENSE·버전·TFM·Generator 의존성 검사 통과.
- 격리 콘솔 .NET 8/10 통과. 새 ServerProbe의 TCP 서버 왕복 포함.
- 격리 WPF .NET 8/10에서 컨트롤 12개씩 로드 성공.
- [로컬 후보 SHA-256](candidate-packages.json). 게시 바이너리 해시와는 별도 기록이다.
- 로그: artifacts/server-packages.log.
