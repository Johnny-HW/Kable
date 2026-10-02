# 📋 Changelog

All notable changes to the **Kable** project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.7.0] - 2026-10-02

### Added
- **High-Frequency Latency Profiler & Zero-Alloc Metric Runner (`Kable.Benchmarks`)**:
  - Implemented `ZeroAllocLatencyCollector` backed by pre-allocated circular ring buffer for zero-overhead, 0-GC latency sampling.
  - Added [LatencyProfileRunner.cs](file:///d:/Johnny/00.New/02.SoftwareLib/01.Kable/tests/Kable.Benchmarks/LatencyProfileRunner.cs) evaluating 50,000 round-trip loopback requests:
    - **Mean**: 21.84 μs, **P50**: 20.10 μs, **P95**: 31.90 μs, **P99**: 55.60 μs, **CV**: 1.2386.
- **Deterministic Time Virtualization (`TimeProvider`)**:
  - Integrated `TimeProvider` into `HeartbeatOptions` and `KableSession` with multi-targeting support (`netstandard2.0`, `net8.0`, `net10.0`).
  - Virtualized heartbeat watchdog and timeout test suite (`HeartbeatWatchdogTests`) using `FakeTimeProvider` for zero-wall-clock-flakiness CI verification.

### Fixed
- **SharedMemory TOCTOU Concurrency Safety (`Kable.SharedMemory`)**:
  - Eliminated `AccessViolationException` caused by concurrent pointer access during `Dispose()` via atomic in-flight I/O tracking (`_activeIoCount`) and spin-wait unmapping guardrails.
  - Added concurrent read/write vs. dispose stress test suite (`SharedMemorySafetyTests`).

### Performance
- **MQTT Telemetry Zero-Copy & Allocation Reduction (`Kable.Mqtt`)**:
  - `PublishRawAsync`: Replaced buffer duplication (`.ToArray()`) with zero-copy `ArraySegment<byte>` extraction via `MemoryMarshal.TryGetArray`.
  - `PublishMetricAsync`: Switched JSON serialization to `Utf8JsonWriter` over streaming buffers.

### Refactoring & Samples
- **Sample Directory Layout Standardization**:
  - Reorganized root `DEMO/` application into standard layout at `samples/Kable.ConfigStudio/` with full Release build validation.
- **Zero-GC & Micro-Latency Audit Report Completion**:
  - Finalized [DESIGN/zero_gc_audit_report.md](file:///d:/Johnny/00.New/02.SoftwareLib/01.Kable/DESIGN/zero_gc_audit_report.md) with comprehensive verification across all 12 test projects (320 tests, 100% pass).

## [1.6.0] - 2026-10-01

### Added
- Configured default request timeouts, same-message-type request extension, and `KableSimple.OpenAsync(KableDeviceOptions)`.
- Builder configuration for session queues, alarm handling, and heartbeat.
- Opt-in `ReconnectingKableClient` with finite attempts, fresh connections, preserved subscriptions, and no command replay.
- Compiled advanced usage examples and CI execution of QuickStart.

### Fixed
- Legacy `TimeoutMs` remains effective until `ConnectTimeoutMs` is explicitly supplied.
- Simple clients release resources on initialization failure and validate default timeout options.
- ASCII/UTF-8 encoding writes directly to output buffers on modern .NET; netstandard2.0 uses pooled arrays.

### Documentation
- Clarified event delivery, lifecycle and recovery contracts; restored top-level specification entry points.
- Replaced blanket allocation claims with scoped Release measurements for 16 codec/session cases.

## [1.5.0] - 2026-09-30

### Added
- **Core Engine Concurrency & Reliability Hardening (`KableSession`)**:
  - **Unified Cleanup & Exception Propagation (`KableSession.Lifecycle.cs`)**:
    - Performed cleanup exactly once by the first caller, while all concurrent and subsequent callers await `_cleanupTcs.Task`.
    - Eliminated early return on stopped/disposed state, ensuring calls route through the shared cleanup task without accessing disposed synchronization primitives (`TC_REL_30`).
    - Guaranteed identical failure propagation across concurrent/subsequent callers without swallowing exceptions, and ensured underlying transport connection disposal exactly once.
    - Concurrent `StartAsync` deduplication: Guarantees that simultaneous connection attempts invoke `ConnectAsync` exactly once (`TC_REL_19`).
    - Single cleanup runner safety: Protects against infinite wait or deadlock scenarios during hardware link termination (`TC_REL_18`).
  - **Decoupled Asynchronous Alarm Spool Architecture (`AlarmSpooler`)**:
    - Added configurable `AlarmQueueCapacity` and `AlarmOverflowMode` (`ThrowAndAbort`, `SpoolToStorage`, `DropOldestWithWarning`) to isolate high-frequency alarm floods from standard request-response transaction routing (`TC_REL_01`–`TC_REL_17`).
    - Dedicated background spool worker using bounded channels to decouple persistent disk/network logging from the core dispatch loop (`TC_REL_21`, `TC_REL_22`).
    - Graceful worker drain contract: Decoupled from arbitrary timeouts to respect `AlarmSpoolDrainTimeout` until full completion (`TC_REL_26`, `TC_REL_29`).
    - Accurate accounting & forensics: Tracks in-flight cancellations as `InDoubt` and unprocessed alarms on shutdown, exposing `AlarmSpoolSummary` on `KableSession` and logging `ALARM_SPOOL_DRAIN_SUMMARY` trace events (`TC_REL_27`, `TC_REL_28`).
    - Cancellation token propagation: `OnAlarmOverflowAsync` receives cancellation token and respects `AlarmSpoolTimeout`, preventing orphan worker task leaks on session dispose (`TC_REL_23`–`TC_REL_25`).
  - **Stream Task Accumulation Defense (`KableSession.Stream.cs`)**:
    - Reused single-slot wait tasks in `GetStreamAsync` to eliminate memory and task growth under continuous streaming subscriptions.
    - Dedicated cancellation token source immediately drains and cancels pending channel waiters when the underlying stream breaks (`TC_REL_20`).
- **Session Industrial Reliability Test Suite (`SessionIndustrialReliabilityTests`)**:
  - 30 comprehensive industrial test cases (`TC_REL_01` to `TC_REL_30`) verifying edge-case concurrency, link failure recovery, worker leak prevention, and queue overflow behaviors.
- **BenchmarkDotNet Performance Validation Suite (`Kable.Benchmarks`)**:
  - Micro-benchmarks for Zero-GC validation, ASCII delimiter codecs, Modbus-TCP framing, and telemetry pipeline throughput.
- **Demo Dashboard & Telemetry Code Export (`DEMOApp`)**:
  - Enhanced industrial dashboard views with alarm monitoring panel, dynamic status indicators, and C# struct code export generator for telemetry models.

### Changed
- **Architectural Decomposition**:
  - Modularized `KableSession.cs` into focused partial classes: `KableSession.Lifecycle.cs`, `KableSession.Messaging.cs`, `KableSession.Loops.cs`, and `KableSession.Stream.cs`.
- **Packaging & Governance**:
  - Harmonized Apache-2.0 open-source licensing across all project files in `Directory.Build.props`.
  - Configured multi-target CI matrix (`.NET 10.0`, `.NET 8.0 LTS`) and automated build validation.

---

## [1.4.0] - 2026-09-23

### Added
- **Industrial Security & Process Guard (`Kable.Host`, `Kable.Grpc.Security`)**:
  - `GuardedProcessLauncher`: Hardened external process execution engine implementing `IProcessLauncher` with strict binary allowlisting, argument injection prevention, working directory checks, and process isolation.
  - `GrpcAuthInterceptor`: Bearer token authentication middleware for gRPC streaming transport.
  - `GrpcConcurrencyInterceptor`: Inbound connection and request concurrency throttle preventing edge broker saturation.
  - `SecurityOptions` & `BusyGuard`: Process mutual-exclusion guards and process host security configuration.
- **Enterprise UI Library (`Kable.UI.Wpf`)**:
  - Reusable WPF UI component and styling library inspired by modern Apple/iOS design language.
  - Clean Apple white theme, rounded DataGrid rows, custom titlebars, and segmented navigation.
  - `LiveInspectorView`: Master-Detail layout with dual live packet streams and split inspector.
  - `RawPacketLogStreamView`: Reverse-chronological streaming packet viewer (newest packets first) with search filtering and file export.
  - `AperiodicCommandConsole`: Interactive command console with automatic engineering-unit to raw hex converter sliders and real-time telemetry verification.
- **In-Memory Hardware Simulator & Packet Classification**:
  - Built-in hardware simulator engine with realistic pacing (600ms–1000ms streaming cadence).
  - Packet catalog categorizing and classifying inbound/outbound industrial telegrams.
- **Dynamic 7-Language Localization (i18n)**:
  - Runtime language switching across 7 languages: English (`en`), Korean (`ko`), Traditional Chinese (`zh-TW`), Simplified Chinese (`zh-CN`), Japanese (`ja-JP`), German (`de-DE`), and French (`fr-FR`).
  - Decoupled UI resource dictionary bindings for zero-restart localization.
- **Configuration & Telemetry Observability**:
  - `KableDeviceOptions` POCO record and `.UseOptions(options)` fluent builder extension for unified configuration injection.
  - `CommandDefinition` and `ScheduledCommandItem` abstractions unifying periodic telemetry polling and aperiodic command dispatch.
  - Industrial alarm lifecycle states, deadband telemetry filtering, and formatted HexDump packet tracing.
  - PCAP network packet capture and replay engine for offline protocol diagnostics.

---

## [1.3.1] - 2026-09-21

### Added
- **Multi-Language Documentation Engine**:
  - Modernized technical documentation portal built on Docsify with responsive navigation.
  - Interactive Mermaid.js architectural diagrams via `docsify-mermaid`.
  - Multilingual documentation support across 7 languages in `docs/{lang}/`.
  - Automated deployment workflow to GitHub Pages.
- **IPC & Profile Enhancements**:
  - Added NamedPipe IPC transport integration to the `KableSimple` async facade.
  - Added automated test cases for string profile clients, zero-allocation weak state subscriptions, and NamedPipe IPC.

### Changed
- Decomposed and decoupled `ProfileManager` for cleaner separation of concerns.
- Integrated `DefaultCommandTimeout` directly into the background periodic polling query loop.

### Fixed
- **Transport Test Suite Stabilization**:
  - Added `HardwareTransportTestCollection` disabling xUnit parallelization across TCP listener and Named Pipe test classes to eliminate OS port/pipe collision flakiness.
  - Ensured deterministic dynamic port selection and guaranteed `DisposeAsync` / broker shutdown in gRPC and MQTT integration tests.
  - Eliminated race conditions in session resilience and telemetry stream tests.

---

## [1.3.0] - 2026-09-18

### Added
- **Industrial Protocol Adapters**:
  - **`Kable.Modbus`**: Modbus-TCP master with MBAP zero-allocation framing, TransactionId pipelining, and async lifecycle management.
  - **`Kable.Melsec`**: Mitsubishi SLMP / MC Protocol 3E binary frame driver for Q/L/iQ-R PLCs.
  - **`Kable.Mqtt`**: High-performance telemetry publisher and subscriber built on `MQTTnet`.
  - **`Kable.OpcUa`**: OPC UA client node bridge built on official `OPCFoundation.NetStandard.Opc.Ua` stack with dynamic node browsing and value subscriptions.
  - **`Kable.Grpc`**: Full-duplex bidirectional streaming transport adapter using protocol buffer contracts (`kable_transport.proto`).
- **High-Speed Shared Memory IPC (`Kable.SharedMemory`)**:
  - Zero-copy lock-free ring buffer backed by `MemoryMappedFile` for ultra-low latency sub-millisecond IPC.
  - Dedicated waveform buffer for streaming analog sensor arrays and oscilloscope data.
- **Async Facade & Codec Templates (`KableSimple`)**:
  - `KableSimple` facade offering intuitive 3-line asynchronous device connectivity.
  - Resilient base codec templates in `Kable.Core`: `DelimitedFrameCodec` and `LengthFieldCodec`.
- **Standalone Visual Tooling (`Kable.ConfigStudio`)**:
  - Standalone WPF visual builder for zero-typo device configuration and TOML generation.
  - Live echo testing and real-time inspector for Modbus-TCP, Melsec SLMP, MQTT, and OPC UA.
- **Build & Optimization**:
  - Central `Directory.Build.props` enabling Tiered Compilation, Profile-Guided Optimization (PGO), and Release optimizations.
- **Documentation & Governance**:
  - `docs/06_INDUSTRIAL_HIGH_RELIABILITY_COMM_ROADMAP.md` covering 13 industrial protocols, fieldbus selection matrix, and determinism prerequisites.
  - Established open-source governance matrix and `THIRD_PARTY_LICENSES.md`.

### Changed
- Standardized all adapters with modern .NET best practices: DIP interfaces, Microsoft `IOptions` pattern with fail-fast validation, `ILogger` injection, and DI extensions.
- Introduced single outbound writer pump in session transport to prevent interleaved frame corruption.

### Fixed
- Fixed P0/P1 reliability defects in session routing, non-blocking DI resolution, and TOML parser fidelity.

---

## [1.2.0] - 2026-09-07

### Added
- **Weak-Coupled Telemetry Stream & State Bus (`Kable.Observability.DeviceTelemetryStream<T>`)**:
  - Lock-free, weak-coupling status streaming engine based on `Channel<T>` with `BoundedChannelFullMode.DropOldest`.
  - Solves memory leaks from standard C# `event EventHandler` and eliminates UI thread-marshalling bottlenecks.
  - Multi-consumer dynamic broadcast support with automatic conflation for high-frequency hardware telemetry.
- **Automated Modbus-RTU Framing & Zero-Allocation Codec (`Kable.Codecs.ModbusRtuCodec`)**:
  - `IProtocolCodec<ReadOnlyMemory<byte>>` implementation with automatic CRC-16 append on encode and automated frame validation on decode.

---

## [1.1.0] - 2026-09-07

### Added
- **Industrial Checksum & CRC Engine (`Kable.Core.Checksums`)**:
  - High-performance, zero-allocation (`ReadOnlySpan<byte>`) industrial integrity verification suite.
  - `Crc16Modbus`: 256-byte static LUT-based Modbus RTU CRC-16 (0xA001) with in-place buffer append and validation.
  - `Crc16Ccitt`: CRC-16 CCITT (0x1021 / XModem) for wafer aligners, robotic stages, and motor controllers.
  - `IndustrialChecksums`: Modbus ASCII LRC (2's complement), Barcode/RFID XOR BCC (Block Check Character), and Sum8 algorithms.
  - Full architectural documentation in `docs/05_INDUSTRIAL_CHECKSUMS.md` and complete unit test coverage in `Kable.Tests`.
- **QA Test Engineering Master Plan & Suite**:
  - Comprehensive QA master plan and gap analysis specifications in `docs/qa_test_engineering/`.
  - 21 new test cases covering extreme byte fragmentation, sliding window framing, multi-segment UTF-8 boundaries, and 100-concurrent FIFO request fairness.
  - Automated TCP RST injection, NamedPipe server crash, and SerialPort cable disconnection fault-injection tests.
  - Total automated test suite expanded to **96 tests with 100% pass rate**.
- **Robustness & Protocol Violation Defense**:
  - `AsciiLineCodec` now enforces `MaxFrameSize` limit (default 64KB) to prevent unbounded memory growth (OOM) under delimiter absence.
  - Added `ProtocolViolationException` for protocol framing violations.
  - Re-entrant thread-safe `DisposeAsync` across all transport connection contexts.

---

## [1.0.0] - 2026-09-04

### Added
- **Bedrock Transport Layer (`Kable.Transports`)**:
  - `TcpConnectionContext` and `TcpConnectionFactory` with zero-delay socket pipelines.
  - `TcpConnectionListener` for server-side socket acceptance.
  - `NamedPipeConnectionContext` and `NamedPipeConnectionFactory` for ultra-fast local IPC.
  - `SerialPortConnectionContext` and `SerialPortConnectionFactory` for industrial RS-232C hardware.
- **Protocol Codec Engine (`Kable.Codecs`)**:
  - Zero-allocation `AsciiLineCodec` supporting custom delimiters, encodings, and autonomous alarm recognition.
  - `IProtocolCodec<T>` abstraction supporting correlation IDs and out-of-band message separation.
- **Reactive Device Session (`Kable.Engine`)**:
  - `KableSession<T>` implementing `IDeviceSession<T>`.
  - Hybrid Transaction Router: automatic FIFO lock serialization for legacy ASCII devices vs. lock-free interleaved multiplexing for correlation ID protocols.
  - RSocket-style interactions: `RequestAsync<T>`, `SendAsync`, `Stream`, and out-of-band `SendUrgentAsync`.
  - Fail-Fast safety policy: immediate dispatch of `DeviceDisconnectedException` upon link termination.
- **Tri-Stream Observability (`Kable.Observability`)**:
  - `CommObserver` with three independent bounded ringbuffers (`PeriodicTelemetry`, `AperiodicCommand`, `SpontaneousAlarm`).
  - `DropOldest` full mode preventing UI lagging under high-frequency telemetry floods.
- **Roslyn Incremental Source Generator (`Kable.Generators`)**:
  - `[DeviceCommand]` declarative attribute generating zero-allocation `IDeviceWireCommand` implementations.
  - Multi-parameter template string interpolation.
- **Dependency Injection & Fluent Builder (`Kable.Extensions`)**:
  - Fluent `KableClientBuilder<T>` for 3-line session initialization.
  - `AddKable()` and `AddKableSession<T>()` service extensions for Microsoft.Extensions.DependencyInjection.
- **Multi-Targeting**: Native cross-compilation support for `.NET 10.0`, `.NET 8.0 (LTS)`, and `.NET Standard 2.0`.
