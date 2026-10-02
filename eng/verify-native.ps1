param(
    [Parameter(Mandatory = $true)]
    [string]$RuntimeIdentifier,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$taskRoot = Split-Path $PSScriptRoot -Parent
$outputPath = Join-Path $taskRoot "artifacts/aot/$RuntimeIdentifier"
dotnet publish "$taskRoot/tests/Cbor.Tests.NativeAot/Cbor.Tests.NativeAot.csproj" -c $Configuration -r $RuntimeIdentifier -o $outputPath -p:CborAotRestore=true
if ($LASTEXITCODE -ne 0) { throw "Native publish failed." }
$executable = Join-Path $outputPath "Cbor.Tests.NativeAot"
if ($RuntimeIdentifier.StartsWith("win-")) { $executable += ".exe" }
& $executable
if ($LASTEXITCODE -ne 0) { throw "Native smoke failed." }
