param(
    [Parameter(Mandatory=$true)][string]$BodyCanonicalId,
    [Parameter(Mandatory=$true)][string]$BodyName,
    [Parameter(Mandatory=$true)][string]$SourceDds,
    [string]$StarBreaker = $env:STARSYNC_STARBREAKER,
    [string]$DataP4k = $env:STARSYNC_DATA_P4K,
    [int]$Mip = 0,
    [string]$AssetRoot
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($StarBreaker)) { throw 'Provide -StarBreaker or set STARSYNC_STARBREAKER.' }
if ([string]::IsNullOrWhiteSpace($DataP4k)) { throw 'Provide -DataP4k or set STARSYNC_DATA_P4K.' }
if (-not (Test-Path $StarBreaker)) { throw "StarBreaker not found: $StarBreaker" }
if (-not (Test-Path $DataP4k)) { throw "Data.p4k not found: $DataP4k" }

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($AssetRoot)) {
    $AssetRoot = Join-Path $repoRoot 'src\StarSyncUniverse\Assets\MapVisuals'
}

function ConvertTo-SafeId([string]$value) {
    $invalid = [System.IO.Path]::GetInvalidFileNameChars()
    $chars = foreach ($c in $value.ToCharArray()) {
        if ($c -eq ':' -or $invalid -contains $c) { '_' } else { $c }
    }
    -join $chars
}

$safeId = ConvertTo-SafeId $BodyCanonicalId
$targetDir = Join-Path $AssetRoot 'Surfaces\Overrides\by-canonical-id'
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
$targetPng = Join-Path $targetDir ($safeId + '.png')
$manifestPath = Join-Path $targetDir ($safeId + '.surface.json')

$arguments = @('dds','decode','--p4k',$DataP4k,'--mip',$Mip.ToString(),$SourceDds,$targetPng)
& $StarBreaker @arguments
if ($LASTEXITCODE -ne 0) { throw "StarBreaker DDS decode failed with exit code $LASTEXITCODE" }
if (-not (Test-Path $targetPng)) { throw "Decoded surface texture was not created: $targetPng" }

$hash = (Get-FileHash -Algorithm SHA256 -Path $targetPng).Hash.ToLowerInvariant()
$file = Get-Item $targetPng
$manifest = [ordered]@{
    schemaVersion = 1
    bodyCanonicalId = $BodyCanonicalId
    bodyName = $BodyName
    projection = 'EQUIRECTANGULAR_EXPECTED'
    sourceDds = $SourceDds
    sourceDataP4k = [IO.Path]::GetFileName($DataP4k)
    mip = $Mip
    outputFile = $file.Name
    outputBytes = $file.Length
    sha256 = $hash
    authority = 'PRESENTATION_ASSET_ONLY_FROM_LOCAL_DATA_P4K'
    registrationStatus = 'UNREGISTERED_UNTIL_AXIS_CALIBRATION'
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('O')
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 -Path $manifestPath

Write-Host "Surface texture override written: $targetPng"
Write-Host "Manifest: $manifestPath"
Write-Host "SHA256: $hash"
