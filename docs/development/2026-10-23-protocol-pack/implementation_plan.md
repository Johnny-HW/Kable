# Protocol Pack 구현 계획 (Kable)

작성일: 2026-10-23. 스키마: [PROTOCOL_PACK.md](../../PROTOCOL_PACK.md). 전체 제품 단계(SimPort Listen·UML·Export)는 SimPort [`implementation_plan.md`](../../../../../100.Utility/00.SimulationCommunication/docs/development/2026-10-23-protocol-pack/implementation_plan.md).

이 문서는 Kable 저장소에서 할 일만 적는다. `PROJECT_SPEC` / `SYSTEM_DESIGN` / `CONVENTIONS`를 지킨다. 파일당 300줄 미만. 테스트 먼저.

## 목적

`*.kable.yaml` 폴더를 불변 모델로 읽고, 같은 폴더를 Roslyn `AdditionalFiles`로 받아 AsciiLine 명령 타입을 컴파일 타임에 방출한다. 코덱 구현은 생성하지 않는다.

## 성공 지표

| # | 지표 | 확인 |
| :--- | :--- | :--- |
| 1 | MassHunter / ABB / Line-PLC 픽스처 로드 | ProtocolPack 테스트 |
| 2 | 잘못된 폴더는 예외 또는 Diagnostic. 파일을 부분 적용하지 않음 | Validator 테스트 |
| 3 | YAML 라운트립 후 의미 동등 | Yaml 테스트 |
| 4 | `oPON` → `IgnitePlasmaCommand.FormatWireMessage() == "oPON"` | Generators 테스트 |
| 5 | `oBATCH.{BatchPath}` → record 생성자 `string BatchPath` | Generators 테스트 |
| 6 | Reply enum에 `Ok`, `Busy`, `Fault`, `Unmatched` | Generators 테스트 |
| 7 | `endpoint` → `CreateDeviceOptions()` Host/Port | Generators 테스트 |
| 8 | 기존 `[DeviceCommand]` 손작성 테스트 전부 통과 | `Kable.Generators.Tests` |

## 비목표

DSL, UML, SimPort UI, 코덱 생성, Modbus RW 헬퍼(2차), MassHunter 손작성 명령 교체, Pages 게시.

---

## P1 — `Kable.ProtocolPack`

netstandard2.0. 생성기·SimPort가 참조. YamlDotNet.

### 파일

- `[NEW] src/Kable.ProtocolPack/Kable.ProtocolPack.csproj`
- `[NEW] src/Kable.ProtocolPack/Models/ProtocolPackDocument.cs`
- `[NEW] src/Kable.ProtocolPack/Models/CommandSpec.cs`
- `[NEW] src/Kable.ProtocolPack/Models/MapSpec.cs`
- `[NEW] src/Kable.ProtocolPack/Models/EndpointSpec.cs`
- `[NEW] src/Kable.ProtocolPack/Models/OutcomeSpec.cs`
- `[NEW] src/Kable.ProtocolPack/Loading/ProtocolPackLoader.cs`
- `[NEW] src/Kable.ProtocolPack/Loading/ProtocolPackValidator.cs`
- `[NEW] src/Kable.ProtocolPack/Loading/ProtocolPackYaml.cs`
- `[NEW] tests/Kable.ProtocolPack.Tests/Kable.ProtocolPack.Tests.csproj`
- `[NEW] tests/Kable.ProtocolPack.Tests/Fixtures/MassHunter-7700/` (SimPort samples 복사)
- `[NEW] tests/Kable.ProtocolPack.Tests/Fixtures/ABB-TALYS/`
- `[NEW] tests/Kable.ProtocolPack.Tests/Fixtures/Line-PLC/`
- `[NEW] tests/Kable.ProtocolPack.Tests/Loading/ProtocolPackLoaderTests.cs`
- `[NEW] tests/Kable.ProtocolPack.Tests/Loading/ProtocolPackValidatorTests.cs`
- `[NEW] tests/Kable.ProtocolPack.Tests/Loading/ProtocolPackYamlRoundtripTests.cs`
- `[MODIFY] Kable.sln`

생성기가 프로젝트 참조할 때:

```xml
<ProjectReference Include="..\Kable.ProtocolPack\Kable.ProtocolPack.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="true" />
```

YamlDotNet은 Pack 프로젝트에만 넣는다. Generators가 Pack을 Analyzer로 참조하면 파서를 복제하지 않는다.

### 검증 케이스 (테스트 먼저)

- 세 픽스처 로드. `codec` / 명령 수 / 맵 수.
- `protocolPackVersion: 2` 거절.
- `commands/x.kable.yaml`에 `codec:` 있으면 거절.
- match 중복 거절.
- `{BatchPath}`인데 params 없음 거절.
- Modbus `value: 1` 거절.
- MELSEC `code: QQ` 거절 (`MelsecDeviceCode` 밖). Pack이 Core를 참조하지 않으려면 허용 코드 테이블을 Pack에 상수로 둔다. 값은 `MelsecDeviceCode`와 동기화.
- 라운트립: 로드 → 저장 → 다시 로드, `name`/`match`/`values` 동일.

```
dotnet test tests/Kable.ProtocolPack.Tests
```

---

## P6 — AdditionalFiles 생성기

P1 이후. SimPort P2와 병렬 가능.

### 파일

- `[MODIFY] src/Kable.Generators/Kable.Generators.csproj` — ProtocolPack 참조
- `[NEW] src/Kable.Generators/ProtocolPack/ProtocolPackSyntaxProvider.cs` — AdditionalFiles 수집
- `[NEW] src/Kable.Generators/ProtocolPack/ProtocolPackEmitter.cs` — 진입
- `[NEW] src/Kable.Generators/ProtocolPack/CommandTypeEmitter.cs` — 명령 record
- `[NEW] src/Kable.Generators/ProtocolPack/ReplyEnumEmitter.cs`
- `[NEW] src/Kable.Generators/ProtocolPack/OptionsFactoryEmitter.cs`
- `[MODIFY] src/Kable.Generators/ProtocolSourceGenerator.cs` — Pack 파이프라인 등록만. 기존 DeviceCommand 경로 유지
- `[NEW] tests/Kable.Generators.Tests/ProtocolPack/AsciiLinePackGenerationTests.cs`
- `[NEW] tests/Kable.Generators.Tests/ProtocolPack/EndpointOptionsGenerationTests.cs`
- `[NEW] tests/Kable.Generators.Tests/Fixtures/MassHunter-7700/`
- `[MODIFY] tests/Kable.Generators.Tests/SourceGeneratorExecutionTests.cs` — 손작성 경로 회귀만 확인. 비대하면 수정하지 않고 새 파일만 추가

방출 규칙:

- 파일 이름: `{CommandName}Command.g.cs`, `{CommandName}Reply.g.cs`, `{PackName}Protocol.g.cs`
- 네임스페이스: 부모 `csharp.namespace`. 없으면 `Kable.Generated.{PackName}`
- `typeKind` 기본: `readonly partial record struct`
- `match`의 `{Name}` → 생성자 파라미터. `params[].type` 매핑: string/int/double
- `[DeviceCommand(template, IsUrgent = ...)]` 부여 후 기존 `FormatWireMessage` 경로 재사용 **또는** Pack emitter가 같은 본문을 직접 씀. 중복 구현을 피하려면 Pack이 부분 타입만 방출하고 `ProtocolSourceGenerator`의 DeviceCommand 실행에 태운다. AdditionalFiles만 있고 사용자 타입이 없으면 Pack emitter가 `IDeviceWireCommand`까지 한 번에 방출하는 편이 단순하다. **선택: Pack emitter가 완전 타입을 방출.** 기존 생성기는 손작성 `[DeviceCommand]` 전용으로 둔다.

Diagnostic:

- YAML 파싱 실패 → Error, 파일 경로 포함
- 검증 실패 → Error
- `csharp.namespace` 없는 폴더 → Info + 기본 네임스페이스

Modbus/MELSEC 1차: `{PackName}Maps.g.cs`에 주소 상수만. RW API는 다음 마일스톤.

### 검증

Generator driver 테스트 (이미 `SourceGeneratorExecutionTests`가 쓰는 패턴):

1. MassHunter YAML AdditionalFiles → `IgnitePlasmaCommand` 컴파일, `FormatWireMessage() == "oPON"`.
2. `StartBatch` 픽스처 추가 시 `new StartBatchCommand("a.b")` → `oBATCH.a.b`.
3. `IgnitePlasmaReply`에 `Busy`/`Fault`.
4. `MassHunter7700Protocol.CreateDeviceOptions().Port == 9000`.
5. 기존 `TestIgniteCommand` 등 손작성 테스트 통과.
6. 깨진 YAML → Diagnostic, 컴파일러 크래시 없음.

```
dotnet test tests/Kable.Generators.Tests
dotnet test tests/Kable.Tests
```

`Kable.Tests`는 Pack과 무관해야 한다. 실패하면 생성기가 Core 공개 API를 깨뜨린 것이다.

---

## 이후 (이 계획 밖)

- Modbus/MELSEC 맵 → typed read helper
- HardwareLib MassHunter `Protocol/`를 Pack으로 전환 (손작성 `[DeviceCommand]` 제거)
- `docs/ko/_sidebar.md`에 PROTOCOL_PACK 링크, Pages 게시
- JSON 표면 (`*.kable.json`) 생성기가 YAML에서 덤프

## 커밋

`test:` Validator 실패 케이스 → `feat:` 로더 → `feat:` 생성기 AsciiLine → `test:` AdditionalFiles driver. 단계 실패 시 해당 커밋만 reset.
