# 06. 산업용 차세대 고신뢰성 통신 기술 스펙트럼 및 Kable 로드맵

- **문서 번호**: KABLE-SPEC-06
- **버전**: v1.0.0
- **작성일**: 2026-09-16
- **모듈 위치**: `02.SoftwareLib/01.Kable/docs/06_INDUSTRIAL_HIGH_RELIABILITY_COMM_ROADMAP.md`

---

## 1. 개요 및 배경

전통적인 장비 통신은 주로 **Raw TCP/IP Socket**이나 **RS-232C/485 직렬 통신**에 의존해 왔습니다.
그러나 반도체 클린룸, 초정밀 화학 제어, 배터리 공정 등 현대 첨단 장비에서는 다음과 같은 한계로 인해 **결정론적(Deterministic) 고신뢰성 통신 기술**이 도입되고 있습니다:

1. **Raw TCP/IP의 한계**:
   - 혼잡 제어 및 Nagle 알고리즘 등으로 인한 불시의 수십~수백 ms 지터(Jitter).
   - 비정상 단선(Unplug) 발생 시 TCP Keep-Alive 감지에 수 초~수십 초 소요.
   - 바이트 스트림 파편화(Fragmentation)로 인한 패킷 파싱 오류 위험.
2. **Kable의 포지셔닝**:
   - `01.Kable`은 현재 `System.IO.Pipelines` 기반의 **0-GC 버퍼 파이프라인, Fail-Fast 즉시 단선 감지, RS-485/TCP/NamedPipe 지원**을 완료한 상태입니다.
   - 본 문서는 현재 Kable 지원 범위와 미지원 고신뢰성 프로토콜 전체 스펙트럼을 투명하게 비교하고 확장 로드맵을 정의합니다.

---

## 2. 전체 산업용 통신 프로토콜 지원 현황 비교 매트릭스

| 계층 | 기술 / 프로토콜 | 주 사용처 | 신뢰도 / 결정론 (Determinism) | Kable 현재 지원 여부 | 향후 대응 방안 |
| :--- | :--- | :--- | :--- | :---: | :--- |
| **PC 내부 IPC** | **1. Named Pipe IPC** | 동일 PC 내 프로세스 격리 (Daemon) | ★★★★★ (OS 커널 직결) | **✅ 지원 완료** | `UseNamedPipe()` 탑재 |
| | **2. Shared Memory (MMF)** | 초고속 비전 영상, 파형 데이터 | ★★★★★ (0µs 제로카피) | ❌ 미지원 | 초고주파 계측용 MMF Transport 검토 |
| **PC ↔ PC / 원격** | **3. Raw TCP Socket** | 일반 네트워크 장비 연동 | ★★★☆☆ (지터 발생 가능) | **✅ 지원 완료** | `UseTcp()` 탑재 |
| | **4. gRPC (Protobuf)** | PC 간 / 모듈 간 고속 제어 표준 | ★★★★☆ (스키마 타입 보장) | ❌ 미지원 | Kable 상위 gRPC Gateway 어댑터 구축 |
| | **5. OPC UA (IEC 62541)** | 반도체 설비 상위 표준 (TSN 결합) | ★★★★★ (표준 보안/모델링) | ❌ 미지원 | OPC UA .NET Standard 스택 바인딩 |
| | **6. DDS (Data Distribution)** | 분산 실시간 제어, 로봇 연계 | ★★★★★ (P2P Zero-Broker) | ❌ 미지원 | CycloneDDS / OpenDDS C# 바인딩 |
| | **7. MQTT (Sparkplug B)** | 센서/유틸리티 텔레메트리 수집 | ★★★☆☆ (QoS 1, 2) | ❌ 미지원 | 경량 브로커 수집 클라이언트 모듈화 |
| **필드버스 / 구동계** | **8. RS-232C / RS-485** | 시리얼 펌프, 센서, 유량계 | ★★★★☆ (FIFO 엄격 보장) | **✅ 지원 완료** | `UseSerialPort()` 탑재 |
| | **9. EtherCAT** | 초정밀 서보 모터, 실시간 IO | ★★★★★ (**지터 < 1µs 확정성**) | ❌ 미지원 (하드웨어 버스) | SOEM 마스터 또는 전용 NIC 드라이버 연동 |
| | **10. PROFINET (IRT)** | 지멘스 PLC 기반 산업 라인 | ★★★★★ (지터 < 1µs) | ❌ 미지원 | 지멘스 산업용 통신 보드 연동 |
| | **11. EtherNet/IP (CIP Safety)**| 로크웰 PLC 기반 안전 제어 | ★★★★★ (SIL 3 안전 보장) | ❌ 미지원 | CIP 스택 라이브러리 연동 |
| | **12. CC-Link IE TSN** | 일본/국내 반도체 라인 | ★★★★★ (TSN 패킷 우선순위) | ❌ 미지원 | 전용 통신 보드 드라이버 연동 |
| | **13. CANopen / DeviceNet** | 노이즈 극심한 모터/밸브 버스 | ★★★★☆ (차동 신호 내노이즈) | ❌ 미지원 | Kvaser / PEAK CAN SDK 어댑터 |

---

## 3. 계층별 통신 기술 세부 해설

### 3.1 PC 내부 초고속 IPC (In-PC Communication)
1. **Named Pipe (현재 지원)**:
   - Windows/Linux 커널의 파이프 메모리를 활용하여 네트워크 카드(NIC)를 거치지 않음.
   - 단일 PC에서 장비 GUI 프로세스와 하드웨어 백그라운드 서비스(Pump Daemon 등)를 격리할 때 최적의 성능 제공.
2. **MemoryMappedFile (공유 메모리 - MMF)**:
   - 초당 수 기가바이트의 비전 카메라 검사 영상이나 고속 아날로그 파형 데이터를 복사(Copy) 없이 메모리 포인터로 직결.

### 3.2 분산 PC 및 상위 시스템 연동 (High-Level Network)
1. **gRPC (HTTP/2 + Protocol Buffers)**:
   - 현대 소프트웨어 장비 제어의 사실상 표준.
   - 강타입(Strongly-Typed) 계약으로 클라이언트-서버 간 데이터 규격 불일치를 컴파일 타임에 원천 차단.
2. **OPC UA over TSN**:
   - 반도체/스마트팩토리 표준(IEC 62541). 장비 내부 변수와 객체를 표준 트리 구조로 탐색 및 보안 암호화 통신.
3. **DDS (Data Distribution Service)**:
   - 중앙 서버/브로커 없이 장비 내 모든 노드가 P2P로 데이터를 주고받으며, 22가지의 세부 QoS(신뢰도, 지연시간, 수명)를 설정 가능.

### 3.3 물리 구동계 필드버스 (Hard Real-Time Fieldbus)
1. **EtherCAT**:
   - 일반 이더넷 프레임이 슬레이브 노드를 통과하는 동안 데이터를 실시간으로 읽고 쓰는 'Processing-on-the-fly' 구조.
   - 100마이크로초 이내에 수십 축 모터의 위치를 완전 동기화.
2. **CIP Safety / PROFINET IRT**:
   - 소프트웨어 명령만으로 비상 정지(EMO)의 기능 안전 규격(SIL 3 / Ple)을 공식 충족하는 산업용 통신.

---

## 4. Kable의 단계별 진화 로드맵 (Expansion Roadmap)

```mermaid
graph TD
    subgraph Current ["Kable 현재 지원 (v1.0)"]
        K1["System.IO.Pipelines (0-GC Engine)"]
        K2["Raw TCP/IP Socket (UseTcp)"]
        K3["Serial Port RS-232/485 (UseSerialPort)"]
        K4["NamedPipe IPC (UseNamedPipe)"]
    end

    subgraph Phase2 ["Kable 2단계 확장 (Software IPC 고도화)"]
        P2_1["gRPC 양방향 스트리밍 어댑터"]
        P2_2["Shared Memory (MMF) 초고속 버퍼 채널"]
    end

    subgraph Phase3 ["Kable 3단계 확장 (스마트 팩토리 & 산업 표준)"]
        P3_1["OPC UA Client 통신 세션 지원"]
        P3_2["MQTT / Sparkplug B 텔레메트리 송출기"]
    end

    Current --> Phase2
    Phase2 --> Phase3
```

- **Phase 1 (완료)**: `Serial`, `TCP`, `NamedPipe`를 아우르는 0-GC 리액티브 파이프라인.
- **Phase 2 (단기 계획)**: 장비 프로세스 격리 및 원격 시뮬레이터를 위한 **`gRPC Transport 어댑터`** 개발.
- **Phase 3 (중장기 계획)**: 스마트 캐비닛 및 공장 상위 연동을 위한 **`OPC UA` / `MQTT Sparkplug B` 커넥터** 모듈화.
