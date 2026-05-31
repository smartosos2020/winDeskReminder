param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = "1.0.0",

    [ValidateSet("win-x64")]
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "WinDeskReminder.csproj"
$publishDir = Join-Path $repoRoot "artifacts\publish\$Runtime-$Version-single"
$installerDir = Join-Path $repoRoot "artifacts\installer"
$toolsDir = Join-Path $repoRoot ".tools"
$wix = Join-Path $toolsDir "wix.exe"

if (-not (Test-Path $wix)) {
    dotnet tool install --tool-path $toolsDir wix
}

dotnet publish $project `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishReadyToRun=true `
    -o $publishDir

New-Item -ItemType Directory -Force $installerDir | Out-Null

& $wix eula accept wix7 | Out-Null
& $wix build (Join-Path $repoRoot "installer\Product.wxs") `
    -arch x64 `
    -d "ProductVersion=$Version" `
    -d "PublishDir=$publishDir" `
    -out (Join-Path $installerDir "WinDeskReminder-$Version-x64.msi")
