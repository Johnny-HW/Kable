# NuGet 1.9.2 게시 결과

2026-10-28 사용자 요청에 따라 NuGet 1.9.2를 게시했다.

## 게시 대상

- 제품 패키지 14개, 버전 1.9.2. Host 실행 프로그램은 제품 NuGet 패키지 대상이 아니다.
- 릴리즈 태그: `v1.9.2`.
- 게시 소스: `272644bafb92b0b527aa98b15db3142667022f56`.
- [NuGet Kable 1.9.2](https://www.nuget.org/packages/Kable/1.9.2).
- [배포 작업: 성공](https://github.com/Johnny-HW/Kable/actions/runs/40648015717).
- NuGet.org와 GitHub Packages 양쪽 업로드 성공. NuGet.org는 제품 14개 모두 Created 및 pushed 응답 확인.
- NuGet 공식 다운로드 인덱스에 14개 버전이 반영된 뒤 공식 피드에서 설치·실행까지 확인했다.

| 패키지 | 버전 |
| --- | --- |
| Kable | 1.9.2 |
| Kable.Core | 1.9.2 |
| Kable.Engine.Disruptor | 1.9.2 |
| Kable.Generators | 1.9.2 |
| Kable.Grpc | 1.9.2 |
| Kable.Melsec | 1.9.2 |
| Kable.Modbus | 1.9.2 |
| Kable.Mqtt | 1.9.2 |
| Kable.Observability.OpenTelemetry | 1.9.2 |
| Kable.OpcUa | 1.9.2 |
| Kable.ProtocolPack | 1.9.2 |
| Kable.SharedMemory | 1.9.2 |
| Kable.Transport.Ipc | 1.9.2 |
| Kable.UI.Wpf | 1.9.2 |

## 검증

- 기존 설계 보강 검증: 전체 테스트 815/815 통과, 경고·오류 0.
- 릴리즈 메타데이터 갱신 후 로컬 패키지 14개 검사와 격리 소비자 검사 통과.
- CI 구성 수정 후 전체 Release 빌드 및 테스트 815/815 재통과.
- 배포 작업에서 Release 빌드, 전체 테스트, 동시 Start 회귀, 패키지 검사, 문서화된 샘플 smoke 통과.
- 게시 후 로컬 후보 feed와 기존 패키지 캐시를 제외하고 NuGet.org만 설정한 새 캐시로 복원했다.
- 공식 패키지 소비자: .NET 8.0.27 / .NET 10.0.9 콘솔 및 WPF 모두 통과. WPF 컨트롤 12개씩 로드.
- 설치된 Kable 제품 패키지 14개가 모두 버전 1.9.2이며 source가 NuGet.org인 것을 확인했다.
- [공식 피드 패키지 SHA-256](published-packages.json)은 실제 NuGet 복원 캐시의 nupkg로 계산했다.

## 게시 전 발견한 CI 구성 문제

최초 배포 검증에서는 공유 메모리 테스트의 보조 프로세스가 솔루션에 빠져 Debug로 빌드됐고,
Release 테스트가 실행할 파일을 찾지 못했다. 이때 게시 단계는 실행되지 않았다.
보조 프로젝트를 솔루션에 등록하고 publish workflow에 명시적인 Release 빌드와 --no-build 테스트를 적용했다.
아직 게시되지 않았던 후보 태그를 해당 구성 커밋으로 갱신한 뒤 정상 게시했다.
제품 런타임 기능 변경은 포함하지 않았다.

별도 CI에서는 테스트 서버 종료 중 소켓 예외가 한 차례 발생했다. 테스트를 제외하거나 완화하지 않고
같은 커밋의 실패 작업을 한 번 재실행했으며 [최종 CI도 성공](https://github.com/Johnny-HW/Kable/actions/runs/40648011874)했다.
이는 해당 테스트의 종료 경합이 완전히 제거됐다는 뜻은 아니다.

## 설치

```powershell
dotnet add package Kable --version 1.9.2
```

필요한 확장 패키지도 1.9.2로 맞춘다. [업그레이드·보안 안내](../../ko/RELEASE_GUIDE.md)를 참고한다.
게시 패키지의 내장 README는 게시 직전 후보 문구를 포함한다. 현재 게시 상태는 이 문서와
저장소의 README·릴리즈 노트에 기록했다. 이미 게시된 버전의 바이너리는 덮어쓰지 않는다.
실장비·장기 운전은 이번 게시 검증 범위에 포함하지 않는다.

## 실행 기록

- 로컬 후보 로그: `artifacts/publish-1.9.2-packages.log`.
- CI 구성 수정 검증: `artifacts/publish-1.9.2-build-fix.log`, `artifacts/publish-1.9.2-tests-fix.log`.
- 배포 작업 로그: `artifacts/publish-1.9.2-success.log`.
- 공식 피드 소비자 로그: `artifacts/publish-1.9.2-public-consumers.log`.
- 공식 피드 소비자·격리 캐시: `artifacts/nuget-publication/1.9.2-20261028/`.

게시 대상은 위 태그의 커밋으로 고정했으며, 이후 작업 트리에서 진행 중인 변경은 게시물에 포함하지 않았다.
