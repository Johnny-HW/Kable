# 문서 우선 릴리즈 준비

사용자 승인: 남은 네 항목 전체 진행 및 배포 전 문서 작성 요청 (2026-10-28).

## 순서와 대상

1. 배포 안내를 먼저 작성: docs/ko/RELEASE_GUIDE.md, docs/en/RELEASE_GUIDE.md.
   지원 환경·패키지 선택·설치·보안·업그레이드·검증·게시 순서를 명시한다.
   기존 문서/릴리즈 노트에 링크하고 검증 전 수치는 완료로 쓰지 않는다.
2. OPC UA 보안: src/Kable.OpcUa/OpcUaOptions.cs, Security/ 및 bridge 생성/연결 부분.
   Options API는 SignAndEncrypt + 명시한 정책 + 인증서 검증이 기본이다.
   기존 문자열 생성자의 비보안 동작은 호환 경로로 유지하고 문서에 구분한다.
   익명·사용자명/암호·인증서 identity, PKI 경로, 정확한 엔드포인트 선택과 실패 폐쇄를 제공한다.
   tests/Kable.OpcUa.Tests/Security/ 로컬 서버 통합 테스트를 먼저 작성한다.
3. .NET 8: Modbus/MQTT/OPC UA의 src 및 tests csproj를 net8.0;net10.0으로 확장.
   net8.0에서 제공하지 않는 테스트 API는 공통 BCL 또는 조건부 코드로 대체한다.
   두 런타임에서 기능·장애 테스트를 실제 실행한다.
4. 통합 검증: Kable.sln 전체 Release 빌드/테스트, QuickStart 실행, Host IPC 테스트,
   WPF/ConfigStudio/공유 메모리 샘플 빌드와 가능한 비대화형 실행 확인.
   GUI가 필요한 검증은 짧은 self-test 모드 또는 별도 STA smoke harness로 종료까지 확인한다.
5. 패키지: 패키지 README/라이선스/TFM/종속성/Analyzer 구성 확인 후 모든 제품 패키지를 로컬 pack.
   tools/release/에 재현 스크립트와 package-consumer 예제를 두고, 빈 프로젝트에서
   ProjectReference 없이 로컬 feed와 격리된 NuGet 캐시로 설치·빌드·실행한다.
6. 결과와 릴리즈 노트 갱신: walkthrough.md에 빌드·테스트·샘플·패키지 실제 결과 기록.

## 계약

- 파일당 300줄 미만으로 분리하고 기능 테스트를 먼저 작성한다.
- 기존 미커밋 변경과 버전 1.9.2는 보존한다. 선택한 이번 작업 파일/변경만 커밋한다.
- 안전한 Options 기본값 변경과 기존 문자열 생성자의 호환 동작을 업그레이드 안내에 명시한다.
- 문서화와 로컬 패키지 검증을 끝내기 전 외부 게시·태그 생성은 하지 않는다.
- 물리 장비·장기 운전·타 OS 검증은 이번 범위에 포함하지 않는다.
- 검증된 단계별 마이크로 커밋을 만든다.
