# Kable release preparation and installation

1.9.2 is a release candidate in the repository, not a statement that it is published.
Read the [Korean release guide](../ko/RELEASE_GUIDE.md) for the complete procedure and
[validation report](../development/2026-10-28-release-readiness/walkthrough.md) for measured results.

## Runtime and installation

Kable/Core target netstandard2.0, .NET 8 and .NET 10. Modbus, MQTT and OPC UA target
.NET 8 and .NET 10. WPF is Windows-only; inspect each package's manifest
for its exact supported target frameworks. Keep Kable product package versions aligned.

After publication, install with `dotnet add package Kable --version 1.9.2`.
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
