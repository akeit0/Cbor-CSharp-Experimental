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

# Build a fresh package for this platform job, then consume it without project references.
dotnet restore "$taskRoot/src/Cbor/Cbor.csproj" --locked-mode
if ($LASTEXITCODE -ne 0) { throw "Package dependency restore failed." }
dotnet pack "$taskRoot/src/Cbor/Cbor.csproj" -c $Configuration --no-restore -o "$taskRoot/artifacts/packages"
if ($LASTEXITCODE -ne 0) { throw "Native consumer package creation failed." }
& "$PSScriptRoot/verify-package.ps1"

$consumerProject = Join-Path $PSScriptRoot "PackageConsumer/PackageConsumer.csproj"
$consumerCache = Join-Path $taskRoot ("artifacts/native-consumer-cache/" + [Guid]::NewGuid().ToString("N"))
$consumerOutput = Join-Path $taskRoot "artifacts/aot/packaged/$RuntimeIdentifier"
dotnet restore $consumerProject -r $RuntimeIdentifier --configfile "$PSScriptRoot/consumer.NuGet.Config" --packages $consumerCache -p:TargetFrameworks=net10.0 -p:CborVerifyNativeConsumer=true
if ($LASTEXITCODE -ne 0) { throw "Packaged Native AOT consumer restore failed." }
dotnet publish $consumerProject -c $Configuration -f net10.0 -r $RuntimeIdentifier --no-restore -o $consumerOutput "-p:RestorePackagesPath=$consumerCache" -p:TargetFrameworks=net10.0 -p:CborVerifyNativeConsumer=true
if ($LASTEXITCODE -ne 0) { throw "Packaged Native AOT consumer publish failed." }
$consumerExecutable = Join-Path $consumerOutput "PackageConsumer"
if ($RuntimeIdentifier.StartsWith("win-")) { $consumerExecutable += ".exe" }
& $consumerExecutable
if ($LASTEXITCODE -ne 0) { throw "Packaged Native AOT consumer execution failed." }
Write-Host "Project and fresh packaged Native AOT consumers passed on $RuntimeIdentifier."
