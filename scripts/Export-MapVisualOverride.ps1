param(
    [Parameter(Mandatory=$true)][string]$CanonicalId,
    [Parameter(Mandatory=$true)][string]$SocpakPattern,
    [string]$DataP4k = $env:STARSYNC_DATA_P4K,
    [string]$StarBreaker = $env:STARSYNC_STARBREAKER,
    [string]$OutputRoot = ".\src\StarSyncUniverse\Assets\MapVisuals\Overrides\by-canonical-id",
    [ValidateSet("none","colors","textures","all")][string]$Materials = "none",
    [int]$Lod = 2,
    [int]$Mip = 4,
    [int]$MemoryCapMb = 4096,
    [switch]$NoInterior,
    [switch]$IncludeLights
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($DataP4k)) {
    throw "Provide -DataP4k or set STARSYNC_DATA_P4K."
}
if ([string]::IsNullOrWhiteSpace($StarBreaker)) {
    throw "Provide -StarBreaker or set STARSYNC_STARBREAKER."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not [IO.Path]::IsPathRooted($StarBreaker)) {
    $StarBreaker = Join-Path $repoRoot $StarBreaker
}
if (-not [IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot $OutputRoot
}

if (-not (Test-Path $StarBreaker)) { throw "StarBreaker not found: $StarBreaker" }
if (-not (Test-Path $DataP4k)) { throw "Data.p4k not found: $DataP4k" }

$invalid = [IO.Path]::GetInvalidFileNameChars()
$safeId = -join ($CanonicalId.ToCharArray() | ForEach-Object { if ($invalid -contains $_) { '_' } else { $_ } })
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$output = Join-Path $OutputRoot ($safeId + ".glb")

$args = @(
    "socpak", "export", $SocpakPattern, $output,
    "--p4k", $DataP4k,
    "--materials", $Materials,
    "--lod", $Lod,
    "--mip", $Mip,
    "--mem-cap", $MemoryCapMb
)
if ($NoInterior) { $args += "--no-interior" }
if (-not $IncludeLights) { $args += "--no-lights" }

Write-Host "Exporting map visual override..."
Write-Host "Canonical ID : $CanonicalId"
Write-Host "SOC pattern  : $SocpakPattern"
Write-Host "Output       : $output"

& $StarBreaker @args
if ($LASTEXITCODE -ne 0) { throw "StarBreaker failed with exit code $LASTEXITCODE" }
if (-not (Test-Path $output)) { throw "Expected GLB was not created: $output" }

$item = Get-Item $output
$hash = Get-FileHash -Algorithm SHA256 $output
$manifest = [ordered]@{
    schema = "starsync-map-visual-override/v1"
    canonicalId = $CanonicalId
    sourceSocpakPattern = $SocpakPattern
    sourceDataP4k = [IO.Path]::GetFileName($DataP4k)
    outputFile = $item.Name
    bytes = $item.Length
    sha256 = $hash.Hash.ToLowerInvariant()
    materials = $Materials
    lod = $Lod
    mip = $Mip
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    authority = "PRESENTATION_ASSET_ONLY_FROM_LOCAL_DATA_P4K"
}
$manifestPath = [IO.Path]::ChangeExtension($output, ".manifest.json")
$manifest | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 $manifestPath

Write-Host "Created $output"
Write-Host "Manifest $manifestPath"
