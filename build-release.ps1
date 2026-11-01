param(
	[string]$Version = '0.7.0',
	[string]$RuntimeIdentifier = 'win-x64',
	[switch]$SelfContained
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$appOutput = Join-Path $root 'artifacts\app'
$payloadFolder = Join-Path $root 'src\MateLauncher.Installer\payload'
$payloadZip = Join-Path $payloadFolder 'MateLauncher.zip'
$releaseFolder = Join-Path $root 'release'
$selfContainedValue = if ( $SelfContained ) { 'true' } else { 'false' }
$suffix = if ( $SelfContained ) { 'self-contained' } else { 'framework-dependent' }

New-Item -ItemType Directory -Force $appOutput, $payloadFolder, $releaseFolder | Out-Null

dotnet publish ( Join-Path $root 'src\MateLauncher.App' ) -c Release -r $RuntimeIdentifier --self-contained $selfContainedValue -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -p:Version=$Version -o $appOutput
if ( $LASTEXITCODE -ne 0 ) { throw 'App publish failed' }

Compress-Archive -Path ( Join-Path $appOutput '*' ) -DestinationPath $payloadZip -Force

$setupOutput = Join-Path $root 'artifacts\setup'
dotnet publish ( Join-Path $root 'src\MateLauncher.Installer' ) -c Release -r $RuntimeIdentifier --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -p:Version=$Version -o $setupOutput
if ( $LASTEXITCODE -ne 0 ) { throw 'Installer publish failed' }

$setupTarget = Join-Path $releaseFolder "MateLauncher-Setup-$Version-$RuntimeIdentifier.exe"
Copy-Item ( Join-Path $setupOutput 'MateLauncher.Setup.exe' ) $setupTarget -Force

$portableTarget = Join-Path $releaseFolder "MateLauncher-$Version-$RuntimeIdentifier-$suffix.zip"
Compress-Archive -Path ( Join-Path $appOutput '*' ) -DestinationPath $portableTarget -Force

$setupHash = ( Get-FileHash $setupTarget -Algorithm SHA256 ).Hash.ToLower()
Set-Content -Path ( $setupTarget + '.sha256' ) -Value "$setupHash  $( Split-Path $setupTarget -Leaf )" -Encoding ascii

Write-Output "Installer : $setupTarget"
Write-Output "Portable  : $portableTarget"
