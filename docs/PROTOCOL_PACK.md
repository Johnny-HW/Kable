# Protocol Pack — 스키마와 코드 생성 계약

작성일: 2026-10-23. 갱신일: 2026-10-27. 상태: **작업 트리에 로더·검증기·생성기 구현 존재, 배포 미확인**.

이 문서는 1.9.2 배포 예정 소스 기준이다. Protocol Pack은 1.9.2에 포함할 기능이며 실제 게시 완료를 뜻하지 않는다. 검증 결과는 [verification.md](development/2026-10-23-protocol-pack/verification.md)를 본다.

이 문서는 프로토콜 폴더의 **필드와 생성기 출력**의 단일 기준이다. SimPort 화면·Listen·Export는 [SimPort `docs/PROTOCOL_PACK.md`](../../../100.Utility/00.SimulationCommunication/docs/PROTOCOL_PACK.md)를 본다. 구현 검증은 [verification.md](development/2026-10-23-protocol-pack/verification.md)를 본다.

상대 경로가 다른 클론에서 깨지면 SimPort 저장소 `00.SimulationCommunication/docs/PROTOCOL_PACK.md`를 연다.

---

## 1. 무엇을 푸는가

Kable을 쓰는 장비 프로젝트는 통신과 Configuration을 끝낸 뒤, **같은 프로토콜 폴더**를 SimPort에 열어 테스트한다. 그 폴더를 프로그램 `Protocol/`에 넣으면 빌드가 **명령 타입과 응답 매칭 코드**를 만든다.

SimPort가 장비 카탈로그(D/W/R/M, 명령 프리셋)를 소유하지 않는다. 폴더가 비면 감시만 한다.

## 2. 고정 결정

| 결정 | 내용 |
| :--- | :--- |
| SSOT | 폴더 하나. YAML이 문서이자 런타임 계약이다. |
| 표면 | `*.kable.yaml`. 현재 입력은 YAML. JSON 파일 탐색·전용 DSL은 미지원. |
| 세션 | 1 연결 = 1 코덱 = N 명령. 명령 파일에 codec를 두지 않는다. |
| 명령 파일 | AsciiLine은 명령당 `commands/*.kable.yaml`. |
| 맵 파일 | Modbus/MELSEC은 `maps/*.kable.yaml`. 명령 파일이 아니다. |
| 에러 | 명령 `ok` / `errors[]`. 패밀리 `unmatched`. SimPort 기본은 ok, 실패는 이름로 주입. |
| Modbus 값 | `addr + values[]` 또는 `range.start/count + fill`. 스칼라 `value` 금지. |
| UML | 시퀀스 뷰. 자유 그리기 금지. 저장은 YAML. |
| 코드 생성 | 폴더를 `AdditionalFiles`로 넣으면 `Kable.Generators`가 타입을 방출. |
| 코덱 | 생성하지 않는다. 이름은 기존 Kable 코덱이다. |

## 3. 폴더

```
MassHunter-7700/
├─ protocol.kable.yaml          codec, endpoint, unmatched, csharp
└─ commands/
     ├─ qSTAT.kable.yaml        match, ok, errors
     └─ oPON.kable.yaml

ABB-TALYS/
├─ protocol.kable.yaml
└─ maps/
     ├─ holding.kable.yaml
     └─ coils.kable.yaml

Line-PLC/
├─ protocol.kable.yaml
└─ maps/
     ├─ D.kable.yaml
     └─ X.kable.yaml
```

부모 파일은 연결당 하나다. 다른 코덱이 필요하면 폴더를 하나 더 둔다.

## 4. 코덱 이름

`protocol.kable.yaml`의 `codec`는 아래만 허용한다. SimPort `SimProtocolKind`와 같다.

| codec | Kable 구현 | 자식 파일 |
| :--- | :--- | :--- |
| `AsciiLine` | `AsciiLineCodec` | `commands/` |
| `ModbusRtu` | `ModbusRtuCodec` | `maps/` |
| `ModbusTcp` | `ModbusTcpCodec` | `maps/` |
| `MelsecSlmp3E` | `MelsecSlmpCodec` | `maps/` |

## 5. 부모 파일 스키마

```yaml
protocolPackVersion: 1
name: MassHunter-7700
codec: AsciiLine

endpoint:
  transport: Tcp          # Tcp | Serial | NamedPipe
  role: Server            # Server = SimPort listen, Client = 장비 앱
  host: 127.0.0.1
  port: 9000

unmatched:
  replies:
    - NAK

csharp:
  namespace: Icpms.MassHunter.Protocol
  typeKind: readonly partial record struct
```

`endpoint` 필드는 `KableDeviceOptions`와 맞춘다.

| 필드 | 대응 |
| :--- | :--- |
| `transport` | `KableDeviceOptions.Transport` |
| `host`, `port` | TCP |
| `portName`, `baudRate`, `parity`, `dataBits`, `stopBits` | Serial |
| `pipeName`, `serverName` | NamedPipe |

Serial 예:

```yaml
endpoint:
  transport: Serial
  role: Server
  portName: COM1
  baudRate: 9600
  parity: None
  dataBits: 8
  stopBits: One
```

Modbus 부모 추가 필드:

```yaml
codec: ModbusTcp
unitId: 1
unmatched:
  exception: 1
outOfRange:
  exception: 2
```

MELSEC 부모 추가 필드:

```yaml
codec: MelsecSlmp3E
unmatched:
  endCode: 0xC059
outOfRange:
  endCode: 0xC056
```

`csharp`는 생성기 전용이다. SimPort는 무시한다.

## 6. 명령 파일 스키마 (AsciiLine)

```yaml
name: IgnitePlasma
match: oPON
urgent: false

params: []                 # match에 {Name}이 있으면 필수

ok:
  replies:
    - ACK

errors:
  - name: busy
    replies:
      - ERR:BUSY
  - name: fault
    replies:
      - ERR:FAULT
```

파라미터가 있는 명령:

```yaml
name: StartBatch
match: "oBATCH.{BatchPath}"
params:
  - name: BatchPath
    type: string            # string | int | double
ok:
  replies:
    - ACK
```

| 필드 | 규칙 |
| :--- | :--- |
| `name` | C# 타입 이름 어근. `IgnitePlasma` → `IgnitePlasmaCommand`. |
| `match` | 수신 프레임 매칭. v1은 Exact. `{Param}`은 생성기 템플릿이자 StartsWith 매칭. |
| `params` | `match`의 `{ }`와 이름이 같아야 한다. |
| `ok.replies` | 정상 시 SimPort가 보내는 줄. 여러 줄이면 순서대로 송신. |
| `errors[].name` | 주입 키. `busy`, `fault` 등. 명령 안에서 유일. |
| `urgent` | `[DeviceCommand(IsUrgent = true)]`와 동일. |

v1 `match`는 Exact 또는 `{Param}` 템플릿이다. Regex 매칭은 v2.

## 7. 맵 파일 스키마

### Modbus

```yaml
name: StatusBlock
table: holding              # holding | input | coil | discrete
addr: 100
values:
  - 1
  - 0
  - 42
```

빈 구간:

```yaml
name: Spare
table: holding
range:
  start: 200
  count: 16
fill: 0
```

coil/discrete의 `values`는 bool이다. 한 파일에 `---`로 여러 블록을 둘 수 있다. UML 목록은 블록 `name`이다.

금지: 스칼라 `value`. FC 구간과 안 맞는다.

### MELSEC

```yaml
name: DataBlock
code: D                     # MelsecDeviceCode: D, W, R, ZR, M, X, Y, L, F, B
addr: 1000
points: 8
values:
  - 0
  - 0
```

`code`는 SimPort가 발명한 목록이 아니라 `MelsecDeviceCode`다. 파일에 없는 디바이스는 맵에 없다.

## 8. 매칭 흐름

```
수신 프레임
  │
  ├─ 1. 코덱 TryDecode
  │
  ├─ 2. commands[].match 로 명령 고름
  │        │
  │        ├─ 없음  → unmatched
  │        │
  │        └─ 있음  → 3. outcome
  │                 ├─ ok        → 기본
  │                 └─ errors[]  → SimPort가 이름로 고름
  │
  └─ 송신
```

생성기는 `IgnitePlasmaReply.Busy` 같은 enum 이름만 만든다. 수신 문자열을 enum으로 변환하는 파서나 런타임 명령 매처는 생성하지 않는다. 앱 또는 SimPort가 응답 해석을 구현해야 한다. 위 흐름은 소비 측의 계약이다.

## 9. 구현과 사용법

[생성기 출력·프로젝트 설정·진단·코덱 제약](PROTOCOL_PACK_USAGE.md)을 참고한다. 스키마와 실제 구현의 제한을 함께 확인해야 한다.
