# Protocol Pack — 생성기 사용법과 구현 제약

갱신일: 2026-10-27. 현재 작업 트리 기준, 배포 미확인. [스키마 계약](PROTOCOL_PACK.md)을 함께 참고한다.

## 1. 생성기가 만드는 것 / 안 만드는 것

빌드 입력: 프로젝트의 `Protocol/**/*.kable.yaml` (`AdditionalFiles`).

AsciiLine에서 `oPON.kable.yaml` + 부모 `csharp.namespace`면:

```csharp
namespace Icpms.MassHunter.Protocol;

[DeviceCommand("oPON")]
public readonly partial record struct IgnitePlasmaCommand;

public enum IgnitePlasmaReply
{
    Ok,
    Busy,
    Fault,
    Unmatched
}
```

`oBATCH.{BatchPath}`면:

```csharp
[DeviceCommand("oBATCH.{BatchPath}")]
public readonly partial record struct StartBatchCommand(string BatchPath);
```

부모 `endpoint`에서 옵션 팩토리:

```csharp
public static class MassHunter7700Protocol
{
    public const string Codec = "AsciiLine";
    public static Kable.Configuration.KableDeviceOptions CreateDeviceOptions()
    {
        return new Kable.Configuration.KableDeviceOptions
        {
            DeviceId = "MassHunter-7700",
            Transport = "Tcp",
            Host = "127.0.0.1",
            Port = 9000
        };
    }
}
```

현재 Modbus/MELSEC 생성: 맵 상수(시작 주소, 길이, 이름). 레지스터 RW 헬퍼는 2차.

| 만든다 | 만들지 않는다 |
| :--- | :--- |
| 명령 record + `FormatWireMessage` | `IProtocolCodec` 본체 |
| Reply enum (`ok`/`errors`/`Unmatched`) | 장비 검증 (`.b` 확장자 등) |
| `KableDeviceOptions` 시드 | 세션 수명, UI |
| 맵 주소 상수 | SimPort 화면 |

기존 `[DeviceCommand]` 손작성은 유지한다. 폴더가 있으면 생성기가 같은 모양을 방출하므로 **폴더와 손작성 타입 이름을 겹치면 안 된다**.

## 2. 프로젝트에 넣는 방법

```xml
<ItemGroup>
  <AdditionalFiles Include="Protocol\**\*.kable.yaml" />
</ItemGroup>
<ItemGroup>
  <PackageReference Include="Kable" Version="$(KableVersion)" />
  <PackageReference Include="Kable.Generators" Version="$(KableVersion)" PrivateAssets="all" />
</ItemGroup>
```

`KableVersion`에는 Protocol Pack 구현이 포함된 검증된 패키지 버전을 지정한다. 아직 배포 여부는 확인하지 않았다. 생성된 타입의 인터페이스·특성은 기존 생성기가 제공한다.

SimPort **Export**의 목표 계약은 이 `Protocol/` 트리를 zip 또는 폴더 복사로 내보내는 것이다. SimPort UI·Export 구현 상태는 이 저장소의 검증 범위에 포함하지 않는다.

## 3. 버전

`protocolPackVersion: 1`. 현재 검증기는 정확히 버전 1만 허용한다. 필드 추가는 하위 호환. 필드 의미 변경은 버전을 올린다.

## 4. 샘플 위치

현재 로더 픽스처는 `tests/Kable.ProtocolPack.Tests/Fixtures/`에 있다. 생성기용 입력은 `tests/Kable.Generators.Tests/Fixtures/`에 있다.

- `MassHunter-7700/` — AsciiLine
- `ABB-TALYS/` — Modbus TCP
- `Line-PLC/` — MELSEC SLMP 3E

`samples/ProtocolPacks/`로 배포된 샘플은 이 문서에서 보장하지 않는다.

## 5. 현재 API와 제약

- `ProtocolPackLoader.Load(folder)`는 부모와 바로 아래 `commands/`, `maps/`의 YAML을 읽고 검증한 후 가변 모델을 반환한다. 파일 I/O는 동기식이므로 통신 핫패스에서 호출하지 않는다.
- `ProtocolPackValidator.Validate(document)`는 오류 목록, `EnsureValid(document)`는 오류 시 `ProtocolPackException`을 제공한다.
- `ProtocolPackLoader.Save(folder, document)`는 검증 후 저장한다. YAML 라운트립을 지원하지만 파일 시스템 트랜잭션이나 쓰기 실패 시 롤백은 보장하지 않는다. 별도 출력 폴더 사용을 권장한다.
- 검증은 버전·코덱·이름·중복 match·누락 placeholder·자식 codec 금지·스칼라 value 금지·MELSEC 코드 등을 확인한다. 알 수 없는 YAML 필드는 무시한다. 주소 범위·endpoint·C# 식별자 전체 검증은 보장하지 않는다.
- 옵션 팩토리는 DeviceId와 endpoint 필드를 복사한다. `role`, `unitId`, `unmatched`, `outOfRange`로 서버·코덱·세션을 자동 생성하지 않는다.
- 맵 상수는 `Name`, `Addr`, `Length`, 해당하는 `Table` 또는 `Code`를 제공한다. Length는 points → range.count → values 개수 순으로 정한다.
- 명령은 `IDeviceWireCommand`, `IsUrgent`, `FormatWireMessage()`를 구현한다. 문자열 포맷은 할당을 수반하며 double의 문화권 독립 포맷을 보장하지 않는다. 전체 기능이 0-GC인 것은 아니다.
- 기본 네임스페이스는 `Kable.Generated.{정규화된PackName}`이다. 손작성 타입과 생성 타입의 이름 충돌은 피해야 한다.

| 진단 | 의미 |
| :--- | :--- |
| `KABLEPACK001` | YAML 파싱 실패 (Error) |
| `KABLEPACK002` | 계약 검증 실패 (Error) |
| `KABLEPACK003` | namespace 누락, 기본값 사용 (Info) |
| `KABLEPACK004` | 부모 없는 YAML 무시 (Warning) |
| `KABLEPACK005` | 생성기 예기치 않은 실패 (Error) |

## 6. 요청·응답 코덱 범위

`ModbusRtuCodec`은 FC01–04 요청/응답, FC05/06, FC15/16 요청/ACK와 예외 프레임을 후보 길이와 CRC로 판별한다. 상관 ID가 없으므로 세션의 FIFO 수명 계약은 유지된다. 모든 Modbus 확장 FC를 지원한다고 해석하지 않는다.

`MelsecSlmpCodec`은 3E 바이너리 요청(`0x0050`)과 응답(`0x00D0`)을 인코드·디코드한다. `Slmp3EFrame.IsRequest`, `IsResponse`, `EndCode`로 구별한다. `IsSuccess`는 EndCode가 0인지 검사하므로 정상 응답 판정에는 `IsResponse`도 확인한다. 요청에는 Command/Subcommand, 응답에는 EndCode가 있으며 3E 상관 ID는 제공하지 않는다.
