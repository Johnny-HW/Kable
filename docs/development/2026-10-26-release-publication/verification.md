# 1.9.0 릴리즈 검증 — 2026-10-26

## 확정 범위

커밋된 소스를 별도 체크아웃에서 빌드했다. 개발 작업본의 미커밋 변경은 포함하지 않았다.
Protocol Pack 생성 기능, Kable.ProtocolPack 패키지와 작업 중이던 코덱 확장은 제외했다.
릴리즈 문서의 최종 검증 수치는 개발 작업본의 459개 테스트와 구분한다.

## 결과

| 검증 | 결과 |
| --- | --- |
| 전체 Release 빌드 | 경고 0 / 오류 0 |
| 전체 테스트 | 12개 프로젝트, 407개 통과 / 실패 0 |
| Core 테스트 | 335개 통과 |
| Generator 테스트 | 8개 통과 |
| NuGet 패키징 | 13개, 버전 1.9.0 |
| Generator 소비 검증 | 로컬 피드·독립 캐시에서 속성 기반 생성 및 실행 성공 |

검증 명령:

```powershell
dotnet build Kable.sln -c Release
dotnet test Kable.sln -c Release --no-build -m:1
dotnet pack Kable.sln -c Release --no-build --no-restore -warnaserror:NU5128
./tests/Kable.Generators.Tests/Packaging/VerifyPackage.ps1 -SkipBuild
```

테스트는 TRX 및 90초 중단 감시를 함께 사용했다. 테스트 임계값은 변경하지 않았다.
게시·Windows CI도 프로젝트를 순차 검증하도록 맞췄고 각 패키지 push의 종료 코드를 검사한다.
Generator의 NU5128 없이 패키징이 완료됐다. 패키지 README 미포함 안내는 별도로 남아 있다.

## 게시 확인

태그는 v1.9.0이며 [게시 워크플로](https://github.com/Johnny-HW/Kable/actions/workflows/publish.yml)가
NuGet.org 및 GitHub Packages 게시를 수행한다. 실제 실행 결과는 워크플로 기록에서 확인한다.
[GitHub 릴리즈](https://github.com/Johnny-HW/Kable/releases/tag/v1.9.0)와
[NuGet 패키지](https://www.nuget.org/packages/Kable/1.9.0)에 최종 배포 정보를 연결한다.
