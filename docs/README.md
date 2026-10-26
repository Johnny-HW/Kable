# Kable documentation

Connect a device, send a query, then add polling and recovery when needed.
일반 통신을 위해 별도 Thread나 Task.Run을 만들 필요 없습니다.

**1.9.0 is being prepared; it has not been published.**
[Release notes](releases/1.9.0.md) · [한국어 릴리즈 노트](releases/1.9.0.ko.md)

| Language | Start here | Configuration | Failures and shutdown |
| --- | --- | --- | --- |
| 한국어 | [처음 연결하기](ko/GETTING_STARTED.md) | [설정과 이벤트](ko/DEVELOPER_USAGE.md) | [실패 처리와 종료](ko/ERROR_HANDLING.md) |
| English | [First connection](en/GETTING_STARTED.md) | [Configuration and events](en/DEVELOPER_USAGE.md) | [Failures and shutdown](en/ERROR_HANDLING.md) |

## Other language documentation

- [繁體中文](zh-tw/README.md)
- [简体中文](zh-cn/README.md)
- [日本語](ja/README.md)
- [Deutsch](de/README.md)
- [Español](es/README.md)

The usage-first guides above are maintained in Korean and English.
Other languages retain their existing architecture documentation.

## Choose an API

| Need | API |
| --- | --- |
| String command/reply | KableSimple |
| Periodic polling and cache | KableProfileManager |
| Custom framing | KableClientBuilder and a codec |
| Connection recovery | ReconnectingKableClient |

Kable targets .NET 8, .NET 10 and netstandard2.0. Pipeline buffers are reused;
string decoding and asynchronous requests allocate. See [measured scope](ko/PERFORMANCE_BASELINE.md).
