# 사용 가이드 갱신 검증 기록

검증일: 2026-10-16. [게시 계획](implementation_plan.md)에 따른 문서 변경이다.

## 변경 내용

- 저장소 README와 한국어·영어 Pages 첫 화면을 최소 Simple 질의 예제로 시작하도록 정리했다.
- Simple/Profile/Builder와 프로토콜 클라이언트의 선택 표를 제공했다.
- 통신용 Thread/Task.Run의 필요 여부와 앱 소유 반복 Task의 취소·await를 설명했다.
- 연결 제한시간과 요청 제한시간, 설정 우선순위를 예제로 구분했다.
- 응답/이벤트 분류, UI 스레드 전환, 예외별 다음 행동을 설명했다.
- 선택적 재연결과 자동 명령 재전송 금지, 비동기 종료 순서를 예제로 제공했다.
- 언어별 시작·설정·실패 안내와 영어 수명 계약을 사이드바에 연결했다.
- 첫 화면의 무조건적인 zero-allocation 표현을 버퍼 재사용 및 실제 측정 범위로 정리했다.

## 로컬 검증

- 변경 문서의 로컬 Markdown 링크와 Docsify 언어별 라우트를 확인했다.
- 변경/추가 문서는 모두 300줄 미만이다.
- 완결된 C# 예제 25개를 현재 Kable 프로젝트 참조로 컴파일했다.
  결과: 경고 0 / 오류 0. 앱 소유 window/viewModel/client가 필요한 기존 UI 조각 1개는
  문맥 예제로 구분하고 독립 컴파일 대상에서 제외했다.
- `dotnet run --project samples/Kable.QuickStart/Kable.QuickStart.csproj --no-restore`:
  설정 기본값 READY, 로컬 TCP 질의 2개와 자발 이벤트 수신을 확인하고 정상 종료했다.
- 로컬 Docsify 화면에서 한국어 첫 화면 → 첫 연결 안내,
  영어 첫 화면 → 첫 연결 안내의 실제 클릭과 렌더링을 확인했다.
- Docsify는 이 사이트 설정에서 본문 Markdown 링크를 사이트 루트 기준으로 해석한다.
  새 언어 문서의 내부 링크는 ko/... 또는 en/... 대상으로 맞췄다.
- `git diff --check`를 통과했다. 제품·테스트·샘플 코드와 워크플로는 변경하지 않았다.

## 게시 경로 및 확인 기준

원격 저장소는 Johnny-HW/Kable이며 GitHub Pages는 workflow 방식이다.
기존 pages.yml은 main의 docs 변경을 감지해 docs 폴더를 업로드하고 배포한다.
새 NuGet 패키지 릴리스나 저장소 권한 변경은 하지 않는다.

- [Pages 배포 기록](https://github.com/Johnny-HW/Kable/actions/workflows/pages.yml)
- [한국어 시작 안내](https://johnny-hw.github.io/Kable/#/ko/GETTING_STARTED)
- [English getting started](https://johnny-hw.github.io/Kable/#/en/GETTING_STARTED)

이 문서가 포함된 커밋의 Pages workflow 성공과 실제 제공된 Markdown 내용을 확인한다.
웹에서 게시 문서를 확인할 때는 위 배포 기록의 커밋과 현재 저장소 커밋을 함께 비교한다.

## 한계

실제 TCP/시리얼 장비를 사용하는 예제는 컴파일을 검증했으며 실행했다고 주장하지 않는다.
PeriodicTimer 예제는 .NET 8/10용으로 표시했다. 기존 다른 언어의 아키텍처 문서는 유지하고,
새 사용 중심 가이드는 한국어·영어로 제공한다.
