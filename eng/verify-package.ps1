param([string]$PackagePath)

$ErrorActionPreference = "Stop"
if (-not $PackagePath) {
    $taskRoot = Split-Path $PSScriptRoot -Parent
    $packages = @(Get-ChildItem -LiteralPath "$taskRoot/artifacts/packages" -Filter "Cbor.*.nupkg")
    if ($packages.Count -ne 1) { throw "Expected one Cbor package; supply -PackagePath if multiple versions exist." }
    $PackagePath = $packages[0].FullName
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath))
try {
    $paths = @($archive.Entries | ForEach-Object { $_.FullName })
    $required = @(
        "lib/netstandard2.0/Cbor.CSharp.dll",
        "lib/netstandard2.1/Cbor.CSharp.dll",
        "lib/net8.0/Cbor.CSharp.dll",
        "lib/net9.0/Cbor.CSharp.dll",
        "lib/net10.0/Cbor.CSharp.dll",
        "analyzers/dotnet/cs/Cbor.SourceGenerator.dll",
        "analyzers/dotnet/cs/Cbor.SourceGenerator.CodeFixes.dll",
        "README.md",
        "LICENSE",
        "THIRD-PARTY-NOTICES.txt"
    )
    foreach ($entry in $required) {
        if ($paths -notcontains $entry) { throw "Package is missing $entry." }
    }
    if ($paths -match '^lib/[^/]+/(Cbor|Cbor\.Comparison\.Runtime)\.dll$') {
        throw "Package contains an obsolete or comparison-only runtime assembly name."
    }

    $nuspecEntry = $archive.Entries | Where-Object { $_.FullName -like "*.nuspec" } | Select-Object -First 1
    if (-not $nuspecEntry) { throw "Package has no nuspec." }
    $reader = [System.IO.StreamReader]::new($nuspecEntry.Open())
    try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $license = $nuspec.SelectSingleNode("//*[local-name()='metadata']/*[local-name()='license']")
    if (-not $license -or $license.type -ne "expression" -or $license.InnerText -ne "Unlicense AND MIT") {
        throw "Package license must identify original Unlicense code and the bundled MIT analyzer adaptation."
    }
    $dependencies = @($nuspec.SelectNodes("//*[local-name()='dependency']"))
    if (-not ($dependencies | Where-Object { $_.id -eq "SerializerFoundation" })) {
        throw "Foundation dependency is missing."
    }
    if ($dependencies | Where-Object { $_.id -like "Microsoft.CodeAnalysis*" }) {
        throw "Roslyn dependencies leaked into the runtime package."
    }
    $foundation = $dependencies | Where-Object { $_.id -eq "SerializerFoundation" }
    foreach ($dependency in $foundation) {
        if ($dependency.exclude -match "Analyzers") { throw "Foundation analyzers are excluded for consumers." }
    }

    Write-Host "CBOR package layout and dependency checks passed."
}
finally {
    $archive.Dispose()
}
