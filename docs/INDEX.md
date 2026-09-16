# Kable Reactive Hardware Communication Architecture (INDEX)

> This document serves as the master documentation index for **`Kable`**, a standardized industrial hardware communication conduit library combining **Microsoft Bedrock's transport abstraction (Pipelines / ConnectionContext)** with **RSocket's reactive interaction model (Request / Send / Stream)**.

---

## 💎 4 Confirmed Architectural Decisions

1. **Hybrid Transaction Engine**:
   - Legacy ASCII instruments without correlation tokens: Automatically serialized via an asynchronous preemptive FIFO lock (`SemaphoreSlim(1, 1)`), preventing response interleaving.
   - Modern protocols with Correlation IDs: Lock-free high-speed pipelining and out-of-order multiplexing.
2. **Fail-Fast Disconnection Safety Policy**:
   - Immediate dispatch of `DeviceDisconnectedException` upon link termination, driving physical hardware to an immediate safe state without dangerous blind retries.
3. **Strict Separation of Concerns**:
   - Core Communication Engine (`Kable`): Emits pure 0-GC telemetry records (`OnPacketTrace`) with zero file I/O overhead.
   - Logging & UI Rendering: Handled asynchronously by injected loggers and bounded UI ringbuffers (`ChannelReader`, `DropOldest`).
4. **Clean Legacy Elimination**:
   - Unified around the modern `IDeviceSession<T>` interface rather than maintaining incomplete legacy adapter shims.

---

## 🏛️ SSOT Master Specifications & Governance
- **[PROJECT_SPEC.md (Project Mission & Architecture)](file:///d:/Johnny/Kable/docs/PROJECT_SPEC.md)**: Core mission, 4 architectural decisions, 3-tier topology.
- **[SYSTEM_DESIGN.md (System Design & Interface Spec)](file:///d:/Johnny/Kable/docs/SYSTEM_DESIGN.md)**: `IConnectionContext`, `IProtocolCodec`, `IDeviceSession` specifications.
- **[CONVENTIONS.md (Coding Standards & Strict Rules)](file:///d:/Johnny/Kable/docs/CONVENTIONS.md)**: 300~500 line limits, prohibition of synchronous blocking, zero-GC guidelines.
- **[AGENTS.md (AI Agent Governance Rules)](file:///d:/Johnny/Kable/AGENTS.md)**: SSOT compliance, TDD, and micro-commit workflows.
- **[CONTRIBUTING.md (Development Guidelines)](file:///d:/Johnny/Kable/CONTRIBUTING.md)**: Multi-targeting, zero-allocation principles, PR standards.
- **[CHANGELOG.md (Release Version History)](file:///d:/Johnny/Kable/CHANGELOG.md)**: Semantic Versioning change log.

---

## 📚 Technical Documentation Index

### 1. [01. Architecture Overview](file:///d:/Johnny/Kable/docs/01_ARCHITECTURE_OVERVIEW.md)
- **Core Philosophy**: Bedrock Transport + RSocket Interaction 3-tier pipeline.
- **Hybrid Transaction Router**: Dynamic switching between FIFO serialization and lock-free interleaving.
- **Class Diagrams**: Integrated Mermaid architectural diagrams.

### 2. [02. Core Interfaces Specification](file:///d:/Johnny/Kable/docs/02_CORE_INTERFACES.md)
- **`IConnectionContext`**: Bedrock standard `PipeReader Input` / `PipeWriter Output` 0-GC pipe contracts.
- **`IProtocolCodec<T>`**: Bidirectional 0-allocation framing and autonomous message detection.
- **`IDeviceSession<T>`**: Reactive interaction API contracts (`RequestAsync`, `SendAsync`, `Stream`, `SendUrgentAsync`).
- **Standard Exceptions**: `DeviceDisconnectedException`, `DeviceTimeoutException`, `ProtocolViolationException`.

### 3. [03. Observability & Logging Specification](file:///d:/Johnny/Kable/docs/03_OBSERVABILITY_LOGGING.md)
- **Separation of Concerns**: Pure 0-GC in-engine dispatch vs. external disk logging.
- **Traffic Classification**: `TrafficKind` (Periodic Telemetry vs. Aperiodic Command vs. Spontaneous Alarm).
- **`ICommObserver`**: UI responsiveness guarantee via `DropOldest` bounded ringbuffer channels.

### 4. [04. Implementation & Directory Layout](file:///d:/Johnny/00.New/02.SoftwareLib/01.Kable/docs/04_IMPLEMENTATION_LAYOUT.md)
- **Standalone Repository**: Clean separation of `src/Kable`, `src/Kable.Generators`, and `tests/`.
- **Pure Namespaces**: `Kable.Core/Transports/Codecs/Engine/Exceptions/Observability/Generators`.
- **Packaging**: NuGet package consumption guide and Dependency Injection registration.

### 5. [05. Industrial Checksums Specification](file:///d:/Johnny/00.New/02.SoftwareLib/01.Kable/docs/05_INDUSTRIAL_CHECKSUMS.md)
- **CRC & Checksum**: CRC-16 (Modbus, CCITT), XOR LRC, Two's Complement 0-allocation checksum implementations.

### 6. [06. Industrial High Reliability Comm Roadmap](file:///d:/Johnny/00.New/02.SoftwareLib/01.Kable/docs/06_INDUSTRIAL_HIGH_RELIABILITY_COMM_ROADMAP.md)
- **13 Industrial Protocols Spectrum**: EtherCAT, gRPC, OPC UA, DDS, Named Pipe, Shared Memory(MMF), PROFINET IRT, EtherNet/IP CIP Safety 등 총망라.
- **Kable Current vs Unsupported**: 현재 지원(`Serial`, `TCP`, `NamedPipe`)과 향후 로드맵(`gRPC`, `OPC UA`, `MMF`) 명세.

### 7. [07. Open-Source Licensing & Compliance Guide](file:///d:/Johnny/00.New/02.SoftwareLib/01.Kable/docs/07_OPENSOURCE_LICENSING_AND_COMPLIANCE.md)
- **Permissive License Matrix**: `Apache-2.0`, `MIT`, `BSD-3-Clause` 기반 100% 비공개 상용 장비 탑재 안전성 보증.
- **Compliance Checklist**: 배포 시 `THIRD_PARTY_LICENSES.txt` 작성법 및 GPL/AGPL 블랙리스트 금지 규정.