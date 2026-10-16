# 사용 순서 중심 문서 갱신 및 GitHub Pages 게시

작성일: 2026-10-16. 사용자가 문서 갱신과 GitHub Pages 업로드를 요청했다.

## 승인 범위와 기준

PROJECT_SPEC, SYSTEM_DESIGN, CONVENTIONS와 실제 공개 API를 기준으로 한다.
제품 기능과 공개 API를 바꾸지 않고 기존 사용법을 발견하고 따라 하기 쉽게 정리한다.
내부 비동기 통신, 연결/요청 제한시간, FIFO 종료 계약과 선택적 재연결을 정확히 설명한다.
사용자 요청에 따라 검증한 문서를 main에 게시하고 기존 Pages 워크플로 완료와 실제 제공 내용을 확인한다.

## 변경 파일 (각 300줄 미만)

- `README.md`, `README.ko.md`: 최소 Simple 예제, API 선택 표, 설정·실패 안내 링크를 앞에 배치.
- `docs/README.md`: 언어별 시작 가이드로 연결하는 포털.
- `docs/ko/README.md`, `docs/en/README.md`: Pages의 사용 중심 첫 화면.
- `docs/ko/GETTING_STARTED.md`, `docs/en/GETTING_STARTED.md`: 연결, Profile/Builder 선택, Task/스레드 설명.
- `docs/ko/DEVELOPER_USAGE.md`, `docs/en/DEVELOPER_USAGE.md`: 설정 우선순위, 이벤트 및 명시적 취소 예제.
- `docs/ko/ERROR_HANDLING.md`, `docs/en/ERROR_HANDLING.md`: 실패별 다음 행동, 재연결·종료 예제.
- `docs/ko/CONNECTION_LIFECYCLE.md`, `docs/en/CONNECTION_LIFECYCLE.md`: 수명 계약 및 동시 시작 취소 안내.
- `docs/ko/_sidebar.md`, `docs/en/_sidebar.md`, `docs/_sidebar.md`: 새 문서 탐색 경로.
- `docs/index.html`: 현재 API 범위에 맞는 사이트 소개 메타데이터.
- 이 폴더의 검증/게시 보고서.

## 검증 및 배포

1. 변경한 Markdown/HTML의 로컬 링크와 파일 줄 수를 검사한다.
2. 완결된 C# 예제 블록을 현재 라이브러리 참조로 컴파일한다.
   실제 장비가 필요한 예제를 실행했다고 주장하지 않는다.
3. 기존 하드웨어 불필요 QuickStart를 빌드·실행한다.
4. 문서를 검증 후 docs 커밋으로 기록하고 main으로 push한다. 강제 push는 하지 않는다.
5. 해당 커밋의 Deploy GitHub Pages 성공, 사이트 HTTP 응답과 새 문서 내용/탐색을 확인한다.

GitHub Pages는 workflow 방식으로 docs 폴더를 게시한다. 현재 원격/로컬 main은 동기화돼 있다.
새 NuGet 릴리스나 애플리케이션 배포는 이번 범위에 포함하지 않는다.
