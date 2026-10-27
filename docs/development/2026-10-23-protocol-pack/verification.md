# Protocol Pack 문서 동기화 검증

검증일: 2026-10-27. 현재 작업 트리의 구현을 기준으로 한다.

## 변경 범위

- PROTOCOL_PACK.md: 구현 전 상태를 수정하고 YAML 스키마 계약 유지.
- PROTOCOL_PACK_USAGE.md: 생성기 출력, 설정, 진단 및 구현 제한 분리.
- 한·영 구현 구조/사용법/색인/사이드바: 새 프로젝트와 계약 문서 연결.
- 생산 코드와 기존 미커밋 변경은 수정하지 않음.

## 빌드 및 테스트

아래 명령은 복원과 해당 프로젝트 빌드를 포함하며 모두 종료 코드 0이다.

| 명령 | 통과 | 실패/건너뜀 |
| :--- | ---: | :--- |
| `dotnet test tests/Kable.ProtocolPack.Tests --nologo -v quiet` | 14 | 0/0 |
| `dotnet test tests/Kable.Generators.Tests --nologo -v quiet` | 21 | 0/0 |
| `dotnet test tests/Kable.Tests --nologo -v quiet` | 355 | 0/0 |
| `dotnet test tests/Kable.Melsec.Tests --nologo -v quiet` | 11 | 0/0 |
| `dotnet test tests/Kable.Modbus.Tests --nologo -v quiet` | 37 | 0/0 |

총 438개 통과. Windows, Debug, net10.0 테스트 실행 결과다.
전체 솔루션 Release 빌드·모든 타깃·실장비 통신·NuGet 게시·Pages 배포는 수행하지 않았다.

## 계약 대조

- CommandTypeEmitter/ReplyEnumEmitter: 명령 포맷과 enum 생성. 응답 파서는 없음.
- OptionsFactoryEmitter: DeviceId와 endpoint 복사. role에 따른 서버 생성 없음.
- MapConstantsEmitter: 이름·주소·길이·Table/Code 상수만 생성.
- ProtocolPackValidator: 제한된 검증 범위와 알 수 없는 필드 무시를 문서에 명시.
- Loader/Persistence: 가변 모델·동기 파일 I/O·저장 롤백 미보장 명시.
- RTU/SLMP와 dual-role 테스트: 요청/응답 지원 범위 및 FIFO 계약 명시.

## 문서 검사 및 한계

추가된 저장소 내부 링크 대상 존재와 수정 문서의 300줄 미만 여부를 검사한다.
SimPort 저장소 상대 링크는 기존 교차 저장소 참조이며 Pages에서는 동작을 보장하지 않는다.
영문 탐색 경로는 공통 한국어 스키마 문서임을 표시한다.
1.9.1 배포 기능으로 오인하지 않도록 현재 소스와 배포 확인을 구분한다.
