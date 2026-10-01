# 시스템 설계 기준

[아키텍처](ko/01_ARCHITECTURE_OVERVIEW.md), [핵심 인터페이스](ko/02_CORE_INTERFACES.md), [연결 수명 계약](ko/CONNECTION_LIFECYCLE.md)을 기준으로 한다.

공개 API의 실제 선언은 `src/Kable.Core/`를 따른다. 기존 인터페이스를 구현한 사용자의 호환성을 유지하도록 편의 API는 확장 메서드와 별도 상위 래퍼로 제공한다.
