# Generator 패키징 정리 — 2026-10-26

## 변경

`Kable.Generators`는 컴파일러 도구이므로 `SuppressDependenciesWhenPacking`을
활성화했다. `analyzers/dotnet/cs`에 포함한 구현 DLL을 앱의 실행용 NuGet
의존성으로도 중복 선언하지 않도록 했다. NU5128을 NoWarn으로 숨기지 않았다.
기존 Protocol Pack 프로젝트 참조와 DLL 포함 설정은 그대로 유지했다.

## 검증

- 수정 전 패키징 검증에서 `-warnaserror:NU5128`으로 실패를 재현했다.
- 수정 후 같은 옵션으로 패키지 생성에 성공했고, 런타임 의존성 그룹이 없음을 확인했다.
- 현재 작업본의 Generator, ProtocolPack, YamlDotNet DLL이 analyzer 폴더에 포함됨을 확인했다.
- 별도 임시 소비 프로젝트에서 로컬 피드만 사용하고 독립 패키지 캐시로 복원했다.
- 소비 프로젝트의 속성 기반 명령, YAML 기반 명령과 옵션 생성 결과를 실제 실행으로 확인했다.
- 앱 실행 폴더에 컴파일러 구현 DLL이 복사되지 않고 추가 NuGet 의존성도 없음을 확인했다.
- Generator 테스트 프로젝트를 다시 빌드하고 테스트 21개가 모두 통과했다.

재현 명령:

```powershell
./tests/Kable.Generators.Tests/Packaging/VerifyPackage.ps1
```

이미 Release 빌드를 마쳤다면 `-SkipBuild`를 지정할 수 있다.
검증 스크립트는 Windows CI의 전체 빌드·패키징 다음 단계에서도 실행한다.
Protocol Pack 참조가 있는 구성은 YAML 생성까지 검증하고, 참조가 없는 구성은
기본 속성 기반 생성을 검증한다. 임시 결과 경로는 실행 완료 시 출력한다.

패키지 README 미포함 안내는 경고가 아닌 별도 안내이며, 이번 의존성 정리에는 포함하지 않았다.
