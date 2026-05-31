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

if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

dotnet publish $project `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishReadyToRun=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishDir

Write-Host "Portable EXE: $(Join-Path $publishDir 'WinDeskReminder.exe')"
