param([string]$Configuration = "Release")

$ErrorActionPreference = "Stop"
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    dotnet restore Cbor.slnx --locked-mode
    if ($LASTEXITCODE -ne 0) { throw "Locked restore failed." }
    dotnet build Cbor.slnx -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Build failed." }
    dotnet test Cbor.slnx -c $Configuration --no-build --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Tests failed." }
    dotnet format whitespace Cbor.slnx --verify-no-changes --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Whitespace verification failed." }
    dotnet pack src/Cbor/Cbor.csproj -c $Configuration --no-build --no-restore -o artifacts/packages
    if ($LASTEXITCODE -ne 0) { throw "Package creation failed." }
    & "$PSScriptRoot/verify-package.ps1"
    & "$PSScriptRoot/verify-package-consumer.ps1" -Configuration $Configuration
}
finally {
    Pop-Location
}
