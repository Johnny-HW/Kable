param([string]$Configuration = 'Release', [switch]$SkipBuild, [switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
function Invoke-DotNet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $($Arguments -join ' ')" }
}
Push-Location $repo
try {
    if (!$SkipBuild) { Invoke-DotNet @('build', 'Kable.sln', '-c', $Configuration, '--nologo', '-v', 'minimal') }
    if (!$SkipTests) {
        Invoke-DotNet @('test', 'Kable.sln', '-c', $Configuration, '--no-build', '-m:1', '--nologo',
            '--logger', 'trx;LogFilePrefix=release-solution', '--results-directory', 'artifacts/release-readiness/solution-tests')
        & ./tests/Kable.Tests/Cases/Engine/Lifecycle/VerifyConcurrentStart.ps1 -Configuration $Configuration
        & ./tests/Kable.Generators.Tests/Packaging/VerifyPackage.ps1 -Configuration $Configuration -SkipBuild
    }
    Invoke-DotNet @('run', '--project', 'samples/Kable.QuickStart/Kable.QuickStart.csproj', '-c', $Configuration, '--no-build')
    Invoke-DotNet @('run', '--project', 'src/Kable.Host/Kable.Host.csproj', '-c', $Configuration, '--no-build', '--', '--smoke-test')
    Invoke-DotNet @('run', '--project', 'samples/Kable.ConfigStudio/Kable.ConfigStudio.csproj', '-c', $Configuration, '--no-build', '--', '--smoke-test')
    Invoke-DotNet @('run', '--project', 'tools/release/SharedMemorySmoke/SharedMemorySmoke.csproj', '-c', $Configuration)
} finally { Pop-Location }
