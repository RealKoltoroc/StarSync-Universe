param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\StarSyncUniverse\StarSyncUniverse.csproj'
$releaseRoot = Join-Path $repoRoot 'release'
$publishRoot = Join-Path $releaseRoot 'publish\win-x64'
$zipPath = Join-Path $releaseRoot 'StarSyncUniverse_0.7.77_win-x64.zip'
$checksumsPath = Join-Path $releaseRoot 'CHECKSUMS.txt'

if (Test-Path $publishRoot) {
    Remove-Item $publishRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null

Write-Host 'Publishing StarSyncUniverse 0.7.77 for win-x64...'
dotnet publish $project -c $Configuration -r win-x64 --self-contained false -o $publishRoot
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

# Public binary packages do not include PDBs because they can retain local source paths.
Get-ChildItem $publishRoot -Recurse -Filter '*.pdb' -File -ErrorAction SilentlyContinue | Remove-Item -Force

$required = @(
    'StarSyncUniverse.exe',
    'StarSyncUniverse.dll',
    'StarSyncUniverse.Contracts.dll'
)
foreach ($file in $required) {
    if (-not (Test-Path (Join-Path $publishRoot $file))) {
        throw "Publish output is missing required file: $file"
    }
}

# Include redistribution/readme notices with the runnable package.
Copy-Item (Join-Path $repoRoot 'LICENSE') (Join-Path $publishRoot 'LICENSE.txt') -Force
Copy-Item (Join-Path $repoRoot 'THIRD_PARTY_NOTICES.md') (Join-Path $publishRoot 'THIRD_PARTY_NOTICES.md') -Force
Copy-Item (Join-Path $repoRoot 'README.md') (Join-Path $publishRoot 'README.md') -Force
Copy-Item (Join-Path $repoRoot 'docs\StarSyncUniverse_UserGuide.md') (Join-Path $publishRoot 'USER_GUIDE.md') -Force

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal

$exeHash = (Get-FileHash (Join-Path $publishRoot 'StarSyncUniverse.exe') -Algorithm SHA256).Hash
$zipHash = (Get-FileHash $zipPath -Algorithm SHA256).Hash
@(
    "StarSyncUniverse.exe  SHA256  $exeHash",
    "StarSyncUniverse_0.7.77_win-x64.zip  SHA256  $zipHash"
) | Set-Content $checksumsPath -Encoding UTF8

Write-Host "EXE SHA256: $exeHash"
Write-Host "ZIP SHA256: $zipHash"
Write-Host "Publish directory: $publishRoot"
Write-Host "Archive: $zipPath"
Write-Host "Checksums: $checksumsPath"
