param([string]$Configuration = "Release")

$ErrorActionPreference = "Stop"
$taskRoot = Split-Path $PSScriptRoot -Parent
$comparisonDirectory = Join-Path $taskRoot "benchmarks/Cbor.Benchmarks.Comparison"
$upstreamDirectory = Join-Path $taskRoot "external/MessagePack-CSharp"
if (-not (Test-Path -LiteralPath "$upstreamDirectory/src/MessagePack/MessagePack.csproj")) {
    throw "Initialize the pinned dependency first: git submodule update --init external/MessagePack-CSharp"
}
$submoduleStatus = git -C $taskRoot submodule status -- external/MessagePack-CSharp
if ($LASTEXITCODE -ne 0 -or $submoduleStatus -notmatch '^ ') { throw "MessagePack must match the pinned submodule commit." }
$upstreamChanges = git -C $upstreamDirectory status --porcelain --untracked-files=no
if ($LASTEXITCODE -ne 0 -or $upstreamChanges) { throw "MessagePack source has local modifications." }

Push-Location -LiteralPath $comparisonDirectory
try {
    dotnet restore Cbor.Benchmarks.Comparison.csproj --locked-mode
    if ($LASTEXITCODE -ne 0) { throw "Comparison dependency restore failed." }
    dotnet build Cbor.Benchmarks.Comparison.csproj -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Comparison compilation failed." }
    dotnet run --project Cbor.Benchmarks.Comparison.csproj -c $Configuration --no-build --no-restore -- --verify
    if ($LASTEXITCODE -ne 0) { throw "Comparison fixture verification failed." }
}
finally {
    Pop-Location
}
