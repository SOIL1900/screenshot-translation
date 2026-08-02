[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = $PSScriptRoot
$SolutionPath = Join-Path $RepoRoot "ScreenshotTranslation.sln"
$AppProjectPath = Join-Path $RepoRoot "src\ScreenshotTranslation.App\ScreenshotTranslation.App.csproj"
$InstallerProjectPath = Join-Path $RepoRoot "installer\ScreenshotTranslation.Installer.wixproj"
$InstallerSourcePath = Join-Path $RepoRoot "installer\Package.wxs"
$PublishDirectory = Join-Path $RepoRoot "artifacts\publish"
$ArtifactsDirectory = Join-Path $RepoRoot "artifacts"
$InstallerBinDirectory = Join-Path $RepoRoot "installer\bin"
$InstallerObjDirectory = Join-Path $RepoRoot "installer\obj"

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Content
    )

    [System.IO.File]::WriteAllText(
        $Path,
        $Content,
        [System.Text.UTF8Encoding]::new($false))
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)]
        [string[]]$CommandArguments
    )

    & dotnet @CommandArguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($CommandArguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath $SolutionPath -PathType Leaf)) {
    throw "Solution file not found: $SolutionPath"
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The dotnet command was not found. Install the .NET 8 SDK first."
}

$OriginalAppProjectContent = [System.IO.File]::ReadAllText($AppProjectPath)
$OriginalInstallerSourceContent = [System.IO.File]::ReadAllText($InstallerSourcePath)

$AppVersionPattern = [regex]::new('<Version>([0-9]+\.[0-9]+\.[0-9]+)</Version>')
$AppVersionMatch = $AppVersionPattern.Match($OriginalAppProjectContent)
if (-not $AppVersionMatch.Success) {
    throw "Unable to read the app version from $AppProjectPath."
}

$CurrentVersion = [version]$AppVersionMatch.Groups[1].Value
$NextVersion = "{0}.{1}.{2}" -f `
    $CurrentVersion.Major,
    $CurrentVersion.Minor,
    ($CurrentVersion.Build + 1)

$InstallerVersionPattern = [regex]::new('Version="[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?"')
if (-not $InstallerVersionPattern.IsMatch($OriginalInstallerSourceContent)) {
    throw "Unable to read the MSI version from $InstallerSourcePath."
}

Write-Host "Current version: $CurrentVersion" -ForegroundColor DarkGray
Write-Host "Packaging version: $NextVersion" -ForegroundColor Cyan

try {
    $UpdatedAppProjectContent = $AppVersionPattern.Replace(
        $OriginalAppProjectContent,
        "<Version>$NextVersion</Version>",
        1)
    Write-Utf8NoBom -Path $AppProjectPath -Content $UpdatedAppProjectContent

    $UpdatedInstallerSourceContent = $InstallerVersionPattern.Replace(
        $OriginalInstallerSourceContent,
        "Version=`"$NextVersion`"",
        1)
    Write-Utf8NoBom -Path $InstallerSourcePath -Content $UpdatedInstallerSourceContent

    Write-Host "[1/7] Cleaning old packaging output..." -ForegroundColor Yellow
    Remove-Item -LiteralPath $PublishDirectory -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $InstallerBinDirectory -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $InstallerObjDirectory -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host "[2/7] Restoring dependencies..." -ForegroundColor Yellow
    Invoke-DotNet -CommandArguments @(
        "restore",
        $SolutionPath)

    Write-Host "[3/7] Building the Release configuration..." -ForegroundColor Yellow
    Invoke-DotNet -CommandArguments @(
        "build",
        $SolutionPath,
        "-c",
        "Release",
        "--no-restore")

    Write-Host "[4/7] Running automated tests..." -ForegroundColor Yellow
    Invoke-DotNet -CommandArguments @(
        "test",
        $SolutionPath,
        "-c",
        "Release",
        "--no-build")

    Write-Host "[5/7] Publishing the self-contained Windows x64 app..." -ForegroundColor Yellow
    Invoke-DotNet -CommandArguments @(
        "publish",
        $AppProjectPath,
        "-c",
        "Release",
        "-r",
        "win-x64",
        "--self-contained",
        "true",
        "-o",
        $PublishDirectory)

    Write-Host "[6/7] Building the MSI package..." -ForegroundColor Yellow
    Invoke-DotNet -CommandArguments @(
        "build",
        $InstallerProjectPath,
        "-c",
        "Release")

    $BuiltMsi = Get-ChildItem `
        -LiteralPath $InstallerBinDirectory `
        -Recurse `
        -File `
        -Filter "ScreenshotTranslation.Installer.msi" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $BuiltMsi) {
        throw "WiX completed, but ScreenshotTranslation.Installer.msi was not found."
    }

    Write-Host "[7/7] Copying the final installer and calculating SHA-256..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $ArtifactsDirectory -Force | Out-Null
    $OutputMsi = Join-Path $ArtifactsDirectory "ScreenshotTranslation-$NextVersion-x64.msi"
    Copy-Item -LiteralPath $BuiltMsi.FullName -Destination $OutputMsi -Force

    $OutputFile = Get-Item -LiteralPath $OutputMsi
    $OutputHash = Get-FileHash -LiteralPath $OutputMsi -Algorithm SHA256

    Write-Host ""
    Write-Host "Packaging succeeded." -ForegroundColor Green
    Write-Host "Version: $NextVersion" -ForegroundColor Green
    Write-Host "Installer: $($OutputFile.FullName)" -ForegroundColor Green
    Write-Host "Size: $($OutputFile.Length) bytes" -ForegroundColor Green
    Write-Host "SHA-256: $($OutputHash.Hash)" -ForegroundColor Green
}
catch {
    Write-Utf8NoBom -Path $AppProjectPath -Content $OriginalAppProjectContent
    Write-Utf8NoBom -Path $InstallerSourcePath -Content $OriginalInstallerSourceContent

    Write-Host ""
    Write-Host "Packaging failed. Version files were restored." -ForegroundColor Red
    throw
}
