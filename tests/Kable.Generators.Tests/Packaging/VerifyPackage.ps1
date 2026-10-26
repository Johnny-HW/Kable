param(
    [string]$Configuration = 'Release',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$checkRoot = Join-Path ([IO.Path]::GetTempPath()) ('kable-generator-package-' + [Guid]::NewGuid().ToString('N'))
$feed = Join-Path $checkRoot 'feed'
$consumer = Join-Path $checkRoot 'consumer'
$cache = Join-Path $checkRoot 'packages'
$version = '0.0.0-packagecheck'
New-Item -ItemType Directory -Force -Path $feed, $consumer | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $consumer 'commands') | Out-Null

function Invoke-DotNet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $($Arguments -join ' ')" }
}

$generatorProject = Join-Path $repoRoot 'src/Kable.Generators/Kable.Generators.csproj'
$runtimeProject = Join-Path $repoRoot 'src/Kable/Kable.csproj'
[xml]$generatorConfig = Get-Content -LiteralPath $generatorProject -Raw
$hasProtocolPack = $generatorConfig.SelectNodes(
    "//*[local-name()='ProjectReference'][contains(@Include, 'Kable.ProtocolPack')]").Count -ne 0
if (-not $SkipBuild) {
    Invoke-DotNet -Arguments @('build', $generatorProject, '-c', $Configuration)
    Invoke-DotNet -Arguments @('build', $runtimeProject, '-c', $Configuration, '-f', 'net10.0')
}
Invoke-DotNet -Arguments @('pack', $generatorProject, '-c', $Configuration, '--no-build', '--no-restore',
    '-o', $feed, "-p:PackageVersion=$version", '-warnaserror:NU5128')

$package = Join-Path $feed "Kable.Generators.$version.nupkg"
$zip = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $entries = @($zip.Entries | ForEach-Object { $_.FullName })
    $expectedDlls = @('Kable.Generators.dll')
    if ($hasProtocolPack) { $expectedDlls += @('Kable.ProtocolPack.dll', 'YamlDotNet.dll') }
    foreach ($dll in $expectedDlls) {
        if ($entries -notcontains "analyzers/dotnet/cs/$dll") { throw "Missing analyzer dependency: $dll" }
    }
    if (@($entries | Where-Object { $_ -match '^(lib|ref)/.*\.dll$' }).Count -ne 0) {
        throw 'Generator must not expose runtime library assemblies.'
    }
    $nuspec = $zip.Entries | Where-Object { $_.FullName -like '*.nuspec' }
    $reader = [IO.StreamReader]::new($nuspec.Open())
    try { [xml]$metadata = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ($metadata.SelectNodes("//*[local-name()='dependencies']/*").Count -ne 0) {
        throw 'Analyzer-only package must not declare runtime dependency groups.'
    }
} finally { $zip.Dispose() }

$runtimeDll = [Security.SecurityElement]::Escape(
    (Join-Path $repoRoot "src/Kable/bin/$Configuration/net10.0/Kable.dll"))
$escapedFeed = [Security.SecurityElement]::Escape($feed)
@"
<configuration>
  <packageSources><clear /><add key="local" value="$escapedFeed" /></packageSources>
</configuration>
"@ | Set-Content -LiteralPath (Join-Path $checkRoot 'NuGet.Config') -Encoding utf8
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable><NuGetAudit>false</NuGetAudit>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>obj/generated</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Kable.Generators" Version="$version" PrivateAssets="all" />
    <Reference Include="Kable"><HintPath>$runtimeDll</HintPath></Reference>
    <AdditionalFiles Include="**/*.kable.yaml" />
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $consumer 'Consumer.csproj') -Encoding utf8
if ($hasProtocolPack) {
@'
protocolPackVersion: 1
name: PackageProbe
codec: AsciiLine
csharp:
  namespace: PackageProbe
'@ | Set-Content -LiteralPath (Join-Path $consumer 'protocol.kable.yaml') -Encoding utf8
@'
name: Ping
match: PING
ok:
  replies:
    - PONG
'@ | Set-Content -LiteralPath (Join-Path $consumer 'commands/ping.kable.yaml') -Encoding utf8
}
$packChecks = ''
if ($hasProtocolPack) {
    $packChecks = @'
if (new PackageProbe.PingCommand().FormatWireMessage() != "PING") throw new Exception("YAML generator failed.");
if (PackageProbe.PackageProbeProtocol.CreateDeviceOptions().DeviceId != "PackageProbe") throw new Exception("Options generation failed.");
'@
}
@"
using System;
using Kable.Generators;
if (new HandwrittenCommand().FormatWireMessage() != "HELLO") throw new Exception("Attribute generator failed.");
$packChecks
Console.WriteLine("Generator package consumption succeeded.");
[DeviceCommand("HELLO")]
public readonly partial record struct HandwrittenCommand;
"@ | Set-Content -LiteralPath (Join-Path $consumer 'Program.cs') -Encoding utf8

$consumerProject = Join-Path $consumer 'Consumer.csproj'
Invoke-DotNet -Arguments @('restore', $consumerProject, '--configfile', (Join-Path $checkRoot 'NuGet.Config'), '--packages', $cache)
Invoke-DotNet -Arguments @('run', '--project', $consumerProject, '-c', $Configuration, '--no-restore')
$assets = Get-Content -LiteralPath (Join-Path $consumer 'obj/project.assets.json') -Raw | ConvertFrom-Json
if (@($assets.libraries.PSObject.Properties).Count -ne 1) {
    throw 'Unexpected transitive runtime packages in generator-only consumer.'
}
foreach ($dll in @('Kable.Generators.dll', 'Kable.ProtocolPack.dll', 'YamlDotNet.dll')) {
    if (Test-Path -LiteralPath (Join-Path $consumer "bin/$Configuration/net10.0/$dll")) {
        throw "Compiler implementation leaked into application output: $dll"
    }
}
Write-Host "Package validation passed. Artifacts: $checkRoot"
