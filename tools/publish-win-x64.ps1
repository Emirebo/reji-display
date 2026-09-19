# RejiDisplay v0.4.0 Deployment Package Build Script
# Creates a standalone published build for Windows x64 and packages it into artifacts/RejiDisplay-v0.4.0-win-x64.zip

param(
    [string]$Version = "v0.4.0",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$rootDir = Resolve-Path (Join-Path $scriptDir "..")
$projectFile = Join-Path $rootDir "src\RejiDisplay\RejiDisplay.csproj"
$artifactsDir = Join-Path $rootDir "artifacts"
$publishDir = Join-Path $artifactsDir "RejiDisplay-$Version-win-x64"
$zipFile = Join-Path $artifactsDir "RejiDisplay-$Version-win-x64.zip"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " Building RejiDisplay $Version Deployment Package ($Configuration)" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# 1. Clean previous artifacts directory if exists
if (Test-Path $publishDir) {
    Write-Host "[1/5] Cleaning existing publish directory: $publishDir" -ForegroundColor Yellow
    Remove-Item -Path $publishDir -Recurse -Force
}
if (Test-Path $zipFile) {
    Write-Host "[1/5] Removing existing ZIP archive: $zipFile" -ForegroundColor Yellow
    Remove-Item -Path $zipFile -Force
}

# 2. Run dotnet publish
Write-Host "[2/5] Running dotnet publish (win-x64, Framework-Dependent)..." -ForegroundColor Green
dotnet publish $projectFile -c $Configuration -r win-x64 --self-contained false -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
}

# 3. Verify critical executable and dependencies
Write-Host "[3/5] Verifying published binaries and native dependencies..." -ForegroundColor Green

$exePath = Join-Path $publishDir "RejiDisplay.exe"
$dllPath = Join-Path $publishDir "RejiDisplay.dll"
$wv2WpfPath = Join-Path $publishDir "Microsoft.Web.WebView2.Wpf.dll"
$wv2LoaderPath = Join-Path $publishDir "runtimes\win-x64\native\WebView2Loader.dll"

if (-not (Test-Path $exePath)) { Write-Error "Missing critical binary: RejiDisplay.exe" }
if (-not (Test-Path $dllPath)) { Write-Error "Missing critical binary: RejiDisplay.dll" }
if (-not (Test-Path $wv2WpfPath)) { Write-Error "Missing WebView2 WPF dependency: Microsoft.Web.WebView2.Wpf.dll" }
if (-not (Test-Path $wv2LoaderPath)) { Write-Error "Missing WebView2 native loader: runtimes\win-x64\native\WebView2Loader.dll" }

Write-Host "  ✓ Executable: RejiDisplay.exe" -ForegroundColor Gray
Write-Host "  ✓ Assembly: RejiDisplay.dll" -ForegroundColor Gray
Write-Host "  ✓ WebView2 WPF: Microsoft.Web.WebView2.Wpf.dll" -ForegroundColor Gray
Write-Host "  ✓ WebView2 Native Loader: runtimes\win-x64\native\WebView2Loader.dll" -ForegroundColor Gray

# 4. Create ZIP archive
Write-Host "[4/5] Creating release ZIP package: $zipFile" -ForegroundColor Green
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipFile -Force

# 5. Output Verification
if (Test-Path $zipFile) {
    $sizeMb = [math]::Round((Get-Item $zipFile).Length / 1MB, 2)
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host " SUCCESS: RejiDisplay $Version Package Created!" -ForegroundColor Green
    Write-Host " Package ZIP Path : $zipFile" -ForegroundColor Yellow
    Write-Host " Package File Size: $sizeMb MB" -ForegroundColor Yellow
    Write-Host "============================================================" -ForegroundColor Cyan
} else {
    Write-Error "Failed to produce release ZIP package."
}
