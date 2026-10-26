# 1.9.1 릴리즈 검증 — 2026-10-26

## 범위

동시 StartAsync 대기에서 정리용 취소가 확정된 연결 오류를 가리지 않도록 수정했다.
실제 호출자 취소 및 Stop/Dispose 동작은 유지한다. 공개 API와 대상 프레임워크는 그대로다.
개발 작업본과 분리한 체크아웃을 사용하고 미커밋 Protocol Pack 작업은 배포에서 제외했다.

요청에 따라 자동 라이브러리 CI의 Linux 작업을 제거했다. 빌드·테스트·NuGet 게시를
Windows에서 수행하며, GitHub Pages 문서 배포는 별도로 유지한다.

## 검증 결과

| 검사 | 결과 |
| --- | --- |
| 전체 Release 빌드 | 경고 0 / 오류 0 |
| 전체 테스트 | 12개 프로젝트, 412개 통과 / 실패 0 |
| 제어된 시작 경합 재현 검사 | 독립 프로세스 3회 통과 |
| 패키징 | 13개, 버전 1.9.1 |
| Generator 소비 검사 | 로컬 피드·독립 캐시에서 실제 생성·실행 성공 |

```powershell
dotnet build Kable.sln -c Release
dotnet test Kable.sln -c Release --no-build -m:1
./tests/Kable.Tests/Cases/Engine/Lifecycle/VerifyConcurrentStart.ps1
dotnet pack Kable.sln -c Release --no-build --no-restore -warnaserror:NU5128
./tests/Kable.Generators.Tests/Packaging/VerifyPackage.ps1 -SkipBuild
```

게시 워크플로도 시작 경합 재현 검사를 필수로 실행한다. 패키지 버전, 내부 Kable 의존성,
릴리즈 요약을 함께 확인한다. 기존 NuGet 1.9.0과 v1.9.0 태그는 수정하지 않는다.
최종 게시 결과는 [v1.9.1 릴리즈](https://github.com/Johnny-HW/Kable/releases/tag/v1.9.1)와
[NuGet](https://www.nuget.org/packages/Kable/1.9.1), 게시 워크플로 실행 기록에서 확인한다.
