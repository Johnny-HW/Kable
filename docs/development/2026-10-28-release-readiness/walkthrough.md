# 릴리즈 준비 완료 결과

검증일: 2026-10-28. Windows, .NET SDK 10.0.301.
남은 네 항목(문서 우선 배포 준비, OPC UA 보안, .NET 8 호환, 전체 통합·패키지 검증)을 완료했다.
실제 NuGet 업로드·원격 push·태그 생성은 수행하지 않았다.

## 문서부터 작성

코드 변경 전에 한국어/영어 RELEASE_GUIDE와 구현 계획을 작성했다.
지원 환경·설치·보안·업그레이드·검증·게시 순서를 정의하고, 검증 완료 후 실제 결과로 갱신했다.
README, 문서 인덱스, 사용법, 릴리즈 노트, CHANGELOG와 패키지 내부 README를 동기화했다.

## 구현

- OPC UA Options는 SignAndEncrypt + Basic256Sha256 + 엄격한 인증서 검증이 기본이다.
- Discovery 결과에서 요청한 모드/정책을 정확히 선택하고 비보안 경로로 낮추지 않는다.
- 요청한 호스트로 인증서 도메인을 검증한다. 개발용 신뢰 허용은 BadCertificateUntrusted만 대상으로 한다.
- PKI 루트 경로, 익명/사용자명·암호/인증서 identity를 지원한다.
- 사용자명 인증은 SignAndEncrypt가 필요하다. identity/암호 바이트 수명을 세션과 함께 정리한다.
- 기존 문자열 생성자의 익명·비보안 동작은 호환 경로로 유지했다.
- Modbus/MQTT/OPC UA 및 대응 테스트 프로젝트를 net8.0;net10.0으로 확장했다.
- Host/ConfigStudio의 비대화형 smoke 모드와 공유 메모리 샘플 실행 harness를 추가했다.
- 제품 패키지에 README와 LICENSE를 포함하고 과도한 성능 표현을 패키지 설명에서 정리했다.
- CI와 게시 workflow에 격리 패키지 소비자 및 샘플 검증 단계를 연결했다.
  게시 단계는 별도로 재생성한 패키지가 아니라 검증한 feed의 14개 제품 패키지를 사용한다.

## 전체 빌드·테스트

전체 Kable.sln Release 빌드: 경고 0, 오류 0.

| 테스트 프로젝트 | .NET 10 | .NET 8 |
| --- | ---: | ---: |
| Kable.Tests | 379 | — |
| Kable.Generators.Tests | 21 | — |
| Kable.Engine.Disruptor.Tests | 3 | — |
| Kable.Transport.Ipc.Tests | 3 | — |
| Kable.Host.Tests | 5 | — |
| Kable.Observability.OpenTelemetry.Tests | 1 | — |
| Kable.Grpc.Tests | 4 | — |
| Kable.SharedMemory.Tests | 102 | — |
| Kable.Modbus.Tests | 110 | 110 |
| Kable.Melsec.Tests | 11 | — |
| Kable.Mqtt.Tests | 3 | 3 |
| Kable.OpcUa.Tests | 11 | 11 |
| Kable.ProtocolPack.Tests | 14 | — |
| 합계 | 667 | 124 |

총 791회 실행, 실패/건너뜀 0. 13개 프로젝트/16개 프로젝트-런타임 조합이다.
실행 런타임은 .NET 8.0.27과 .NET 10.0.9다.
추가 CI 게이트인 제어된 동시 시작 실패 재현 3회와 기존 Generator 단독 격리 패키지 소비자도 통과했다.
이전 단계의 2분 지속 부하는 재실행하지 않았고 이번 전체 테스트에는 기본 3초 부하와 기존 FIFO/CID soak가 포함됐다.

OPC UA 보안 테스트는 두 런타임에서 실제 로컬 서버로 다음을 확인했다.

- 비보안 서버에 보안 Options로 접속하면 실패하고 비보안으로 낮추지 않음.
- 미신뢰 서버 인증서는 거부하고 명시적으로 신뢰 저장소에 추가한 서버는 암호화 연결에 성공.
- 올바른 사용자명/암호와 인증서 identity는 성공, 잘못된 암호는 거부.
- 개발용 미신뢰 인증서 허용을 켜도 만료/호스트명 오류를 거부.
- 기존 일괄 RW, 구독 복원, 취소·종료 테스트 통과.

## 실행 확인

| 대상 | 결과 |
| --- | --- |
| QuickStart | 모의 TCP 질의·응답·자발 이벤트 및 설정 예제 실행 성공 |
| Kable.Host --smoke-test | 실제 Named Pipe echo, 종료 후 활성 세션 0 |
| ConfigStudio --smoke-test | 비표시 메인 창 로드·레이아웃·DataContext·종료 성공 |
| 공유 메모리 재연결 샘플 | UI 세션 두 번 연결/종료 후 장비 세션 유지, PING/PONG 성공, 진단 오류 0 |
| 패키지 WPF 소비자 | .NET 8/10 각각 공개 컨트롤 12개 초기화·레이아웃·종료 성공 |

GUI smoke는 로딩/종료 경로 검증이며 사용자 조작 전체나 화면 품질 검증은 아니다.

## 패키지 설치 검증

14개 제품 패키지를 로컬 pack했다. Host와 샘플/테스트 프로젝트는 제품 NuGet feed에서 제외했다.
모든 패키지의 버전(1.9.2), README metadata/파일, Apache-2.0 metadata/LICENSE 파일을 검사했다.
Modbus/MQTT/OPC UA의 net8.0/net10.0 DLL과 Generator의 세 가지 analyzer DLL을 확인했다.
Generator에 runtime dependency group이 없고 소비자 출력에 compiler 구현 DLL이 유출되지 않는 것도 확인했다.

제품 패키지는 source mapping으로 후보 feed에서만 복원했다.
외부 종속성은 기존 캐시를 읽기 전용 feed로 사용하거나 nuget.org에서 복원하되,
소비자의 global packages cache는 실행별 새 빈 디렉터리로 격리했다.
소비자 코드에는 ProjectReference나 로컬 소스 DLL HintPath가 없다.

- Console: .NET 8/10에서 attribute/YAML 코드 생성, PLC 변환, 실제 로컬 TCP 질의,
  MQTT 발행/구독, 엄격한 신뢰 설정의 보안 OPC UA 읽기/구독을 실행했다.
- Windows: .NET 8/10에서 실제 패키지의 WPF 컨트롤 12개를 로드했다.
- 총 네 가지 소비자 실행 모두 성공. SHA-256은 packages.json에 기록했다.

최종 검증 산출물(로컬의 무시된 artifacts 디렉터리):

- [패키지 manifest와 SHA-256](../../../artifacts/release-readiness/c7a7177b3fce4a078ac554d16d02821a/packages.json)
- [검증한 패키지 feed](../../../artifacts/release-readiness/c7a7177b3fce4a078ac554d16d02821a/feed/)
- 전체 테스트 TRX: artifacts/release-readiness/solution-tests/
- 패키지 실행 로그: artifacts/release-readiness/package-verification.log
- 최종 빌드·샘플 실행 로그: artifacts/release-readiness/sample-verification.log

## 재현

```powershell
./tools/release/VerifySolution.ps1
./tools/release/VerifyPackages.ps1
```

첫 번째 스크립트는 빌드·전체 테스트·샘플을 실행한다. 이미 통과한 테스트를 재실행하지 않고
샘플만 재확인할 때는 -SkipTests, 기존 빌드를 사용할 때는 -SkipBuild를 명시할 수 있다.
두 번째 스크립트는 실행별 새 feed/cache/consumer와 packages.json을 만든다.
CI/게시 workflow YAML은 파싱 검증했고 동일한 로컬 단계가 통과했다. GitHub 원격 실행은 수행하지 않았다.

## 게시 전 남은 결정과 경계

현재 작업 트리에 원래부터 있던 미커밋 변경은 보존했다. 위 결과는 그 변경을 포함한 현재 트리를 기준으로 한다.
실제 게시 시에는 원래 작업까지 통합한 릴리즈 커밋과 패키지 버전을 확정하고 동일 검증을 재현해야 한다.
NuGet 계정/버전 가용성 확인, 최종 게시 승인, 업로드와 태그 생성은 아직 실행하지 않았다.
물리 장비·장기 운전·다른 OS·UI 전체 사용 시나리오는 이번 검증 범위 밖이다.
