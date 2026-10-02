param([string]$Configuration = "Release")

$ErrorActionPreference = "Stop"
$taskRoot = Split-Path $PSScriptRoot -Parent
$consumerProject = Join-Path $PSScriptRoot "PackageConsumer/PackageConsumer.csproj"
# A unique cache prevents an older build of the same unreleased package version from hiding defects.
$consumerCache = Join-Path $taskRoot ("artifacts/consumer-cache/" + [Guid]::NewGuid().ToString("N"))
dotnet restore $consumerProject --configfile "$PSScriptRoot/consumer.NuGet.Config" --packages $consumerCache
if ($LASTEXITCODE -ne 0) { throw "Package consumer restore failed." }

dotnet build $consumerProject -c $Configuration --no-restore "-p:RestorePackagesPath=$consumerCache"
if ($LASTEXITCODE -ne 0) { throw "Package consumer compilation failed." }
foreach ($framework in @("net8.0", "net9.0", "net10.0")) {
    dotnet run --project $consumerProject -c $Configuration -f $framework --no-build --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Package consumer failed on $framework." }
}

$previousErrorAction = $ErrorActionPreference
try {
    $ErrorActionPreference = "Continue"
    $diagnostics = dotnet build $consumerProject -c $Configuration -f net10.0 --no-restore "-p:RestorePackagesPath=$consumerCache" -p:CborVerifyOwnershipViolation=true 2>&1
    $ownershipExitCode = $LASTEXITCODE
}
finally { $ErrorActionPreference = $previousErrorAction }
$diagnostics | Set-Content -LiteralPath "$taskRoot/artifacts/package-ownership-check.log"
if ($ownershipExitCode -eq 0 -or -not ($diagnostics -match "error SF002") -or -not ($diagnostics -match "error CBOR003") -or
    -not ($diagnostics -match "ContextCollectionOwnershipViolation.cs.*error CBOR003")) {
    throw "The installed package did not enforce single-owner buffers and contexts; see artifacts/package-ownership-check.log."
}
# Restore the positive variant and a successful native-command exit status after the expected failure.
dotnet build $consumerProject -c $Configuration -f net10.0 --no-restore "-p:RestorePackagesPath=$consumerCache"
if ($LASTEXITCODE -ne 0) { throw "Package consumer did not recover after the negative ownership check." }
Write-Host "Fresh package installation, all consumer tiers, and SF002/CBOR003 enforcement passed."
