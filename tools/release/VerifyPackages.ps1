param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$work = Join-Path $repo ('artifacts/release-readiness/' + [guid]::NewGuid().ToString('N'))
$feed = Join-Path $work 'feed'
$cache = Join-Path $work 'cache'
New-Item -ItemType Directory -Path $feed, $cache -Force | Out-Null

function Invoke-DotNet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $($Arguments -join ' ')" }
}

# Stop inheritance from repository build customizations in the clean consumers.
'<Project />' | Set-Content -LiteralPath (Join-Path $work 'Directory.Build.props') -Encoding utf8
'<Project />' | Set-Content -LiteralPath (Join-Path $work 'Directory.Build.targets') -Encoding utf8
$version = (& dotnet msbuild (Join-Path $repo 'src/Kable/Kable.csproj') -getProperty:PackageVersion).Trim()
if ($LASTEXITCODE -ne 0 -or !$version) { throw 'Could not resolve package version.' }
$projects = Get-ChildItem -LiteralPath (Join-Path $repo 'src') -Directory | ForEach-Object {
    $project = Join-Path $_.FullName ($_.Name + '.csproj')
    if ((Test-Path -LiteralPath $project) -and $_.Name -ne 'Kable.Host') { $project }
}
foreach ($project in $projects) {
    Invoke-DotNet @('pack', $project, '-c', $Configuration, '-o', $feed, '--nologo', '-v', 'minimal')
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$manifest = @()
foreach ($package in Get-ChildItem -LiteralPath $feed -Filter '*.nupkg') {
    $zip = [IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $names = @($zip.Entries.FullName)
        if ('README.md' -notin $names -or 'LICENSE' -notin $names) { throw "Missing README/license: $($package.Name)" }
        $entry = $zip.Entries | Where-Object FullName -like '*.nuspec' | Select-Object -First 1
        $reader = [IO.StreamReader]::new($entry.Open())
        try { [xml]$spec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $id = $spec.SelectSingleNode("//*[local-name()='metadata']/*[local-name()='id']").InnerText
        $packVersion = $spec.SelectSingleNode("//*[local-name()='metadata']/*[local-name()='version']").InnerText
        if ($packVersion -ne $version) { throw "Version mismatch: $id" }
        if ($spec.SelectSingleNode("//*[local-name()='readme']").InnerText -ne 'README.md') { throw "README metadata missing: $id" }
        if ($spec.SelectSingleNode("//*[local-name()='license']").InnerText -ne 'Apache-2.0') { throw "License metadata mismatch: $id" }
        if ($id -eq 'Kable.Generators') {
            foreach ($dll in @('Kable.Generators.dll', 'Kable.ProtocolPack.dll', 'YamlDotNet.dll')) {
                if ("analyzers/dotnet/cs/$dll" -notin $names) { throw "Missing analyzer dependency: $dll" }
            }
            if ($spec.SelectNodes("//*[local-name()='dependencies']/*").Count) { throw 'Generator must not have runtime dependencies.' }
        }
        if ($id -in @('Kable.Modbus', 'Kable.Mqtt', 'Kable.OpcUa')) {
            foreach ($tfm in @('net8.0', 'net10.0')) {
                if ("lib/$tfm/$id.dll" -notin $names) { throw "Missing $tfm asset: $id" }
            }
        }
        $manifest += [pscustomobject]@{ id = $id; version = $packVersion; file = $package.Name
            sha256 = (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256).Hash
            frameworks = @($names | Where-Object { $_ -like 'lib/*/*.dll' } | ForEach-Object { ($_ -split '/')[1] } | Sort-Object -Unique) }
    } finally { $zip.Dispose() }
}
if ($manifest.Count -ne $projects.Count) { throw 'Package count mismatch.' }
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $work 'packages.json') -Encoding utf8
$escapedFeed = [Security.SecurityElement]::Escape($feed)
$dependencyCache = [Security.SecurityElement]::Escape((Join-Path $env:USERPROFILE '.nuget/packages'))
@"
<configuration>
  <packageSources><clear />
    <add key="candidate" value="$escapedFeed" />
    <add key="dependency-cache" value="$dependencyCache" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="candidate"><package pattern="Kable*" /></packageSource>
    <packageSource key="dependency-cache"><package pattern="*" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content -LiteralPath (Join-Path $work 'NuGet.Config') -Encoding utf8
foreach ($kind in @('Console', 'Windows')) {
    $consumer = Join-Path $work $kind
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Consumers/$kind") -Destination $consumer -Recurse
    $project = (Get-ChildItem -LiteralPath $consumer -Filter '*.csproj' | Select-Object -First 1).FullName
    Invoke-DotNet @('restore', $project, '--configfile', (Join-Path $work 'NuGet.Config'), '--packages', $cache, "-p:KableVersion=$version", '--nologo')
    $frameworks = if ($kind -eq 'Console') { @('net8.0', 'net10.0') } else { @('net8.0-windows', 'net10.0-windows') }
    foreach ($tfm in $frameworks) {
        Invoke-DotNet @('run', '--project', $project, '-c', $Configuration, '-f', $tfm, '--no-restore', "-p:KableVersion=$version")
    }
    $assets = Get-Content -LiteralPath (Join-Path $consumer 'obj/project.assets.json') -Raw | ConvertFrom-Json
    foreach ($library in $assets.libraries.PSObject.Properties) {
        if ($library.Value.type -eq 'project') { throw 'Consumer unexpectedly uses a project reference.' }
    }
}
Write-Host "Verified $($manifest.Count) packages on .NET 8/10. Manifest and consumers: $work"
if ($env:GITHUB_OUTPUT) { "feed=$feed" | Add-Content -LiteralPath $env:GITHUB_OUTPUT -Encoding utf8 }
