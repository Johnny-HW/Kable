> Latest release: **1.10.1** (2026-10-29). [Release notes](../releases/1.10.1.md) · [Publication verification](https://github.com/Johnny-HW/Kable/actions/runs/40816337009).

# Kable releases and installation

1.10.1 was published on NuGet on 2026-10-29. See the [official package](https://www.nuget.org/packages/Kable/1.10.1) and [publication verification](https://github.com/Johnny-HW/Kable/actions/runs/40816337009).
Read the [Korean release guide](../ko/RELEASE_GUIDE.md) for the complete procedure and
[validation report](../development/2026-10-28-server-release/walkthrough.md) for measured results.

## Runtime and installation

Kable/Core target netstandard2.0, .NET 8 and .NET 10. Modbus, MQTT and OPC UA target
.NET 8 and .NET 10. WPF is Windows-only; inspect each package's manifest
for its exact supported target frameworks. Keep Kable product package versions aligned.

Install with `dotnet add package Kable --version 1.10.1`.
Before publication, use the isolated local-feed package consumer verification under tools/release/.
The QuickStart sample uses a local simulated device and needs no hardware.

## OPC UA security and migration

The Options constructor defaults to SignAndEncrypt, Basic256Sha256 and strict server
certificate validation. It requires an exact matching endpoint and never falls back to None.
Configure PkiRootPath; verify the server certificate before adding it to trusted/certs,
and configure the server to trust the client's application certificate.

Use UserName/Password for username authentication or UserCertificate for certificate identity.
Username credentials require SignAndEncrypt. Do not commit credentials or private keys.
AutoAcceptUntrustedCertificates is a development opt-in; it does not permit other errors
such as expired certificates or wrong hostnames.

The existing string constructor retains the legacy anonymous, unsecured connection path.
For new applications use `new OpcUaClientBridge(new OpcUaOptions { EndpointUrl = ... })`.
Options/DI users must configure the server's security policy and certificate trust when upgrading.

## Release gates

Write and review documentation first. Build and test the full solution, run samples and UI/Host
smoke checks, then pack locally. Inspect README, license, dependencies, framework assets and
generator dependencies. Install the packages into clean consumers with isolated caches and run
them on .NET 8 and .NET 10. Record package SHA-256 hashes and results. Publish only after
the final package set and documentation have been reviewed; local validation does not publish.

Physical hardware, driver behavior, other operating systems and multi-day operation are outside
this validation. Queue overflow may drop data; reconnection does not replay missed data or writes.

Run `./tools/release/VerifySolution.ps1` and `./tools/release/VerifyPackages.ps1` on Windows.
The latter writes a separate local feed, isolated cache, clean consumers and a SHA-256 package
manifest under artifacts/release-readiness/. Product packages must come from that local feed;
third-party dependencies may use the existing cache as a read-only feed or nuget.org.

## Session hardening migration (1.9.2)

Alarm classification uses `KableSessionOptions.IsAlarmMessage`, or the optional
`IAlarmMessageClassifier<TMessage>` implemented by a codec. Arbitrary ToString substring matches
are removed: `NO_FAULT` is a normal reply. AsciiLineCodec reserves $/#/!/* followed by ALARM
(case insensitive) and end-of-string, colon, underscore or whitespace. Define device-specific
FAULT/EMERGENCY frames explicitly. The options predicate replaces the codec alarm rule.

Wait telemetry delivery now runs in a separate bounded worker. `InboundQueueCapacity` bounds the
consumer queue and `InboundDeliveryCapacity` bounds its backlog (both default to 10,000).
One message can additionally be held by the worker. Saturating the backlog terminates the session
with `TelemetryBufferOverflowException`; it cannot indefinitely block reply matching or alarm routing.
DropOldest/DropNewest/DropWrite use the consumer queue directly; inspect `DroppedTelemetryCount`.
Options are copied at session construction. Keep classification and observer callbacks short.

Custom decoders must consume input on success. False with consumed invalid bytes retries immediately;
false without progress awaits more input. Success without consumption or input growth is a protocol violation.

Always await DisposeAsync after Abort. TCP/NamedPipe cleanup completes the pipelines even after abort,
and concurrent disposers share completion. MQTT/OPC UA cleanup releases channels, sessions and identities
when a recovery worker fails. Cleanup errors can surface as AggregateException after resource release.
FIFO cancellation/timeout and existing alarm spool accounting/drain contracts are preserved.

## Server hosting (1.10.0)

See [the server guide](SERVER_HOSTING.md) and [release notes](../releases/1.10.0.md).
